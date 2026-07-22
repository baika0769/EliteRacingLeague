using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Admin;
using Eliteracingleague.API.Extensions;
using Eliteracingleague.API.Models;
using Eliteracingleague.API.Services;
using Eliteracingleague.API.Services.Auditing;
using Eliteracingleague.API.Services.Notifications;
using Eliteracingleague.API.Services.Racing;
using Eliteracingleague.API.Services.Rewards;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Controllers.Admin;

[Authorize(Roles = UserRoles.Admin)]
[ApiController]
[Route("api/admin/results")]
public class AdminRaceResultsController : ControllerBase
{
    private readonly EliteRacingLeagueContext _context;
    private readonly PredictionEvaluationService _predictionEvaluationService;
    private readonly TournamentStandingService _standingService;
    private readonly RaceResultValidationService _validationService;
    private readonly IAuditService _auditService;
    private readonly INotificationService _notificationService;
    private readonly PrizePayoutService _prizePayoutService;
    private readonly ILogger<AdminRaceResultsController> _logger;

    public AdminRaceResultsController(
        EliteRacingLeagueContext context,
        PredictionEvaluationService predictionEvaluationService,
        TournamentStandingService standingService,
        RaceResultValidationService validationService,
        IAuditService auditService,
        INotificationService notificationService,
        PrizePayoutService prizePayoutService,
        ILogger<AdminRaceResultsController> logger)
    {
        _context = context;
        _predictionEvaluationService = predictionEvaluationService;
        _standingService = standingService;
        _validationService = validationService;
        _auditService = auditService;
        _notificationService = notificationService;
        _prizePayoutService = prizePayoutService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetResults(CancellationToken cancellationToken) =>
        Ok(await ResultQuery().OrderByDescending(r => r.ResultId).ToListAsync(cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetResultById(int id, CancellationToken cancellationToken)
    {
        var result = await ResultQuery().FirstOrDefaultAsync(r => r.ResultId == id, cancellationToken);
        return result == null
            ? NotFound(new AdminActionResponse { Message = "Race result not found", Id = id })
            : Ok(result);
    }

    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingResults(CancellationToken cancellationToken) =>
        Ok(await ResultQuery()
            .Where(r => r.Status == RaceResultStatuses.RefereeConfirmed)
            .OrderBy(r => r.RaceId)
            .ThenBy(r => r.FinishPosition)
            .ToListAsync(cancellationToken));

    [HttpPut("{id:int}/approve")]
    public async Task<IActionResult> ApproveResult(int id, CancellationToken cancellationToken)
    {
        var result = await _context.RaceResults.AsNoTracking()
            .FirstOrDefaultAsync(r => r.ResultId == id, cancellationToken);
        if (result == null)
            return NotFound(new AdminActionResponse { Message = "Race result not found", Id = id });

        return BadRequest(new AdminActionResponse
        {
            Message = "Individual result approval is disabled. Approve all results of the race atomically.",
            Id = result.ResultId,
            Status = result.Status,
            Note = $"Use PUT /api/admin/results/race/{result.RaceId}/approve-all"
        });
    }

    [HttpPut("race/{raceId:int}/approve-all")]
    public async Task<IActionResult> ApproveAllResults(
        int raceId,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var adminId))
        {
            return Unauthorized();
        }

        var race = await _context.Races
            .Include(r => r.Tournament)
                .ThenInclude(t => t.Races)
            .FirstOrDefaultAsync(r => r.RaceId == raceId, cancellationToken);

        if (race == null)
        {
            return NotFound(new AdminActionResponse
            {
                Message = "Race not found",
                Id = raceId
            });
        }

        if (race.Status == RaceStatuses.Published)
        {
            var alreadyPublishedResults = await _context.RaceResults
                .AsNoTracking()
                .CountAsync(r =>
                    r.RaceId == raceId &&
                    r.Status == RaceResultStatuses.Published,
                    cancellationToken);

            var alreadyApprovedReport = await _context.RefereeReports
                .AsNoTracking()
                .AnyAsync(r =>
                    r.RaceId == raceId &&
                    r.ReportType == RefereeReportTypes.PostRace &&
                    r.Status == RefereeReportStatuses.Approved,
                    cancellationToken);

            // Idempotent success: supports FE versions that still call the
            // report approval endpoint and then approve-all immediately after.
            if (alreadyPublishedResults > 0 && alreadyApprovedReport)
            {
                var expectedAwardRanks = await _context.RaceResults
                    .AsNoTracking()
                    .Where(result =>
                        result.RaceId == raceId &&
                        result.Status == RaceResultStatuses.Published &&
                        result.OutcomeStatus == RaceOutcomeStatuses.Finished &&
                        result.FinishPosition.HasValue &&
                        result.FinishPosition.Value >= 1 &&
                        result.FinishPosition.Value <= 3)
                    .Select(result => result.FinishPosition!.Value)
                    .Distinct()
                    .OrderBy(rank => rank)
                    .ToListAsync(cancellationToken);

                var generatedAwardRanks = await _context.PrizeAwards
                    .AsNoTracking()
                    .Where(award => award.RaceId == raceId)
                    .Select(award => award.RankPosition)
                    .Distinct()
                    .ToListAsync(cancellationToken);

                var missingAwardRanks = expectedAwardRanks
                    .Except(generatedAwardRanks)
                    .OrderBy(rank => rank)
                    .ToList();

                if ((race.Tournament.PrizePool ?? 0m) > 0m &&
                    missingAwardRanks.Count > 0)
                {
                    return Conflict(new
                    {
                        code = "PUBLISHED_RACE_PRIZE_DATA_MISSING",
                        message = "The race is already published, but prize awards/payouts are missing for one or more winning ranks. Configure the prize rules and run the supplied repair script for this race.",
                        raceId,
                        tournamentId = race.TournamentId,
                        tournamentPrizePool = race.Tournament.PrizePool,
                        missingAwardRanks
                    });
                }

                return Ok(new
                {
                    message = "This post-race submission was already approved and published.",
                    raceId,
                    status = RaceStatuses.Published,
                    reportStatus = RefereeReportStatuses.Approved,
                    resultStatus = RaceResultStatuses.Published,
                    publishedResults = alreadyPublishedResults,
                    alreadyCompleted = true
                });
            }

            return Conflict(new AdminActionResponse
            {
                Message = "The race is Published but its report/results are not synchronized. Use the correction workflow.",
                Id = raceId,
                Status = race.Status
            });
        }

        if (race.Status != RaceStatuses.ResultPending)
        {
            return BadRequest(new AdminActionResponse
            {
                Message = "Only a race in ResultPending can be approved and published.",
                Id = raceId,
                Status = race.Status
            });
        }

        if (race.Tournament.Status == TournamentStatuses.Cancelled)
        {
            return BadRequest(new AdminActionResponse
            {
                Message = "A cancelled tournament cannot publish results.",
                Id = raceId
            });
        }

        // Only the latest final report is used for this approval.
        var finalReport = await _context.RefereeReports
            .Where(r =>
                r.RaceId == raceId &&
                r.ReportType == RefereeReportTypes.PostRace)
            .OrderByDescending(r => r.ReportId)
            .FirstOrDefaultAsync(cancellationToken);

        if (finalReport == null)
        {
            return BadRequest(new
            {
                code = "POST_RACE_REPORT_MISSING",
                message = "The final referee report must be submitted before approval.",
                raceId
            });
        }

        if (finalReport.Status == RefereeReportStatuses.Returned)
        {
            return Conflict(new
            {
                code = "POST_RACE_REPORT_RETURNED",
                message = "The final referee report was returned and must be resubmitted before approval.",
                raceId,
                reportId = finalReport.ReportId,
                reportStatus = finalReport.Status
            });
        }

        if (finalReport.Status is not RefereeReportStatuses.Submitted and
            not RefereeReportStatuses.Approved)
        {
            return Conflict(new
            {
                code = "POST_RACE_REPORT_INVALID_STATUS",
                message = "The final referee report is not ready for approval.",
                raceId,
                reportId = finalReport.ReportId,
                reportStatus = finalReport.Status
            });
        }

        var registrations = await _context.RaceRegistrations
            .Where(r =>
                r.RaceId == raceId &&
                (r.Status == RaceRegistrationStatuses.ReadyToRace ||
                 r.Status == RaceRegistrationStatuses.Completed) &&
                _context.PreRaceInspections.Any(inspection =>
                    inspection.RaceId == raceId &&
                    inspection.RegistrationId == r.RegistrationId &&
                    inspection.Status == PreRaceInspectionStatuses.Passed))
            .ToListAsync(cancellationToken);

        if (registrations.Count == 0)
        {
            return BadRequest(new AdminActionResponse
            {
                Message = "No horses passed pre-race inspection, so there are no post-race results to approve.",
                Id = raceId
            });
        }

        var registrationIds = registrations
            .Select(r => r.RegistrationId)
            .ToHashSet();

        var results = await _context.RaceResults
            .Where(r =>
                r.RaceId == raceId &&
                registrationIds.Contains(r.RegistrationId))
            .OrderBy(r => r.ResultId)
            .ToListAsync(cancellationToken);

        var missingRegistrationIds = registrationIds
            .Except(results.Select(r => r.RegistrationId))
            .OrderBy(id => id)
            .ToList();

        if (missingRegistrationIds.Count > 0)
        {
            return BadRequest(new
            {
                code = "MISSING_RACE_RESULTS",
                message = "Every horse that passed pre-race inspection must have a result before approval.",
                expectedResults = registrationIds.Count,
                actualResults = results.Count,
                missingRegistrationIds
            });
        }

        var disqualifiedIds = (await _context.RaceViolations
            .AsNoTracking()
            .Where(v =>
                v.RaceId == raceId &&
                v.Action == RaceViolationActions.Disqualified &&
                registrationIds.Contains(v.RegistrationId))
            .Select(v => v.RegistrationId)
            .Distinct()
            .ToListAsync(cancellationToken))
            .ToHashSet();

        foreach (var result in results.Where(r =>
                     disqualifiedIds.Contains(r.RegistrationId)))
        {
            result.OutcomeStatus = RaceOutcomeStatuses.Disqualified;
            result.FinishPosition = null;
            result.FinishTimeSeconds = null;
        }

        var validationErrors = _validationService
            .ValidateForPublication(results, registrationIds, disqualifiedIds)
            .ToList();

        if (validationErrors.Count > 0)
        {
            return BadRequest(new
            {
                code = "INVALID_RACE_RESULTS",
                message = "Race results are not publishable. No data was changed.",
                errors = validationErrors
            });
        }

        var prizeRules = await _context.PrizeRules
            .Where(r => r.RaceId == raceId)
            .ToDictionaryAsync(r => r.RankPosition, cancellationToken);

        var tournamentPrizePool = race.Tournament.PrizePool ?? 0m;
        if (tournamentPrizePool > 0m)
        {
            var requiredRanks = new[] { 1, 2, 3 };
            var missingPrizeRuleRanks = requiredRanks
                .Where(rank => !prizeRules.TryGetValue(rank, out var rule) || rule.PrizeAmount <= 0m)
                .ToList();
            var allocatedPrize = prizeRules.Values.Sum(rule => rule.PrizeAmount);

            if (missingPrizeRuleRanks.Count > 0 || allocatedPrize != tournamentPrizePool)
            {
                return Conflict(new
                {
                    code = "PRIZE_RULES_REQUIRED",
                    message = "This race has a prize pool but does not have a complete and matching Gold/Silver/Bronze prize distribution. Configure prize rules before publishing results.",
                    raceId,
                    tournamentId = race.TournamentId,
                    tournamentPrizePool,
                    allocatedPrize,
                    missingPrizeRuleRanks
                });
            }
        }

        var existingAwards = await _context.PrizeAwards
            .Include(a => a.Payouts)
            .Where(a => a.RaceId == raceId)
            .ToListAsync(cancellationToken);

        var lockedPayout = existingAwards
            .SelectMany(a => a.Payouts)
            .FirstOrDefault(p =>
                p.Status is PrizeAwardStatuses.UnderReview or PrizeAwardStatuses.Paid);

        if (lockedPayout != null)
        {
            return Conflict(new AdminActionResponse
            {
                Message = "An owner or jockey payout is under review or already paid. Resolve it before approving again.",
                Id = lockedPayout.PrizePayoutId,
                Status = lockedPayout.Status
            });
        }

        var registrationById = registrations.ToDictionary(r => r.RegistrationId);
        var now = DateTime.UtcNow;
        var stage = "starting approval";

        await using var transaction = await _context.Database
            .BeginTransactionAsync(cancellationToken);

        try
        {
            // Delete stale awards first and flush the DELETE before any new INSERT.
            // This avoids UQ_prize_awards_race_rank conflicts on retries/corrections.
            stage = "deleting old prize awards";
            if (existingAwards.Count > 0)
            {
                _context.PrizeAwards.RemoveRange(existingAwards);
                await _context.SaveChangesAsync(cancellationToken);
            }

            stage = "approving final report and race results";

            finalReport.Status = RefereeReportStatuses.Approved;
            finalReport.ReviewedByAdminId = adminId;
            finalReport.ReviewedAt = now;
            finalReport.UpdatedAt = now;

            foreach (var result in results)
            {
                // Public pages, prediction settlement, replay and standings all
                // read only Published race results.
                result.AdminConfirmedBy = adminId;
                result.Status = RaceResultStatuses.Published;
                result.PublishedAt = now;
                result.UpdatedAt = now;

                var registration = registrationById[result.RegistrationId];
                registration.Status = RaceRegistrationStatuses.Completed;
            }

            race.Status = RaceStatuses.Published;
            race.LifecycleVersion++;
            race.UpdatedAt = now;

            var tournamentCompleted = race.Tournament.Races.All(r =>
                r.RaceId == raceId ||
                r.Status is RaceStatuses.Published or RaceStatuses.Cancelled);

            race.Tournament.Status = tournamentCompleted
                ? TournamentStatuses.Completed
                : TournamentStatuses.Ongoing;
            race.Tournament.UpdatedAt = now;

            // Flush the core workflow statuses separately from prize creation.
            await _context.SaveChangesAsync(cancellationToken);

            stage = "creating prize awards";

            foreach (var result in results)
            {
                if (result.OutcomeStatus != RaceOutcomeStatuses.Finished ||
                    !result.FinishPosition.HasValue)
                {
                    continue;
                }

                if (!prizeRules.TryGetValue(result.FinishPosition.Value, out var prizeRule))
                {
                    if (result.FinishPosition.Value <= 3 && tournamentPrizePool > 0m)
                    {
                        throw new InvalidOperationException(
                            $"Prize rule for rank {result.FinishPosition.Value} is missing.");
                    }

                    continue;
                }

                var registration = registrationById[result.RegistrationId];

                var prizeAward = new PrizeAward
                {
                    RaceId = raceId,
                    RegistrationId = result.RegistrationId,
                    OwnerId = registration.OwnerId,
                    JockeyId = registration.JockeyId,
                    RankPosition = result.FinishPosition.Value,
                    PrizeAmount = prizeRule.PrizeAmount,
                    Status = PrizeAwardStatuses.ReadyToClaim,
                    CreatedAt = now
                };

                _prizePayoutService.CreateRecipientPayouts(prizeAward, now);
                _context.PrizeAwards.Add(prizeAward);
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            await transaction.RollbackAsync(cancellationToken);

            return Conflict(new
            {
                code = "POST_RACE_APPROVAL_DATABASE_CONFLICT",
                message = $"Approval failed while {stage}.",
                detail = ex.GetBaseException().Message,
                raceId,
                reportId = finalReport.ReportId
            });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);

            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                code = "POST_RACE_APPROVAL_FAILED",
                message = $"Approval failed while {stage}.",
                detail = ex.Message,
                raceId,
                reportId = finalReport.ReportId
            });
        }

        // Audit and notification are secondary effects. They must not roll back a
        // successfully published race if one of them temporarily fails. Each effect
        // runs in its own try/catch so a single failure (e.g. one bad notification
        // query) can't silently wipe out every other notification that was queued
        // before it - previously all of these shared one try/catch around a single
        // SaveChangesAsync, so any exception anywhere in the block meant NOTHING in
        // it got persisted, including notifications that had already succeeded.
        var secondaryWarnings = new List<string>();

        async Task RunSecondaryEffectAsync(string label, Func<Task> action)
        {
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Post-race approval secondary effect failed: {Label} (raceId={RaceId}, reportId={ReportId})",
                    label, raceId, finalReport.ReportId);
                secondaryWarnings.Add($"{label}: {ex.Message}");
            }
        }

        await RunSecondaryEffectAsync("audit log", () => _auditService.WriteAsync(
            adminId,
            AuditActionTypes.Approve,
            "RaceResult",
            raceId.ToString(),
            new
            {
                RaceStatus = RaceStatuses.ResultPending,
                ResultStatus = RaceResultStatuses.RefereeConfirmed,
                ReportStatus = RefereeReportStatuses.Submitted
            },
            new
            {
                RaceStatus = RaceStatuses.Published,
                ResultStatus = RaceResultStatuses.Published,
                ReportStatus = RefereeReportStatuses.Approved,
                ResultCount = results.Count,
                TournamentStatus = race.Tournament.Status
            },
            "Atomic final report approval and race result publication",
            cancellationToken));

        await RunSecondaryEffectAsync("referee approval notification", () => _notificationService.CreateForUserAsync(
            finalReport.RefereeId,
            "Final report and race results approved",
            $"Your final report and all results for race {race.RaceName} were approved and published.",
            "PostRaceSubmissionApproved",
            $"/referee/races/post-race?raceId={raceId}&reportId={finalReport.ReportId}",
            "RefereeReport",
            finalReport.ReportId,
            cancellationToken,
            preventDuplicates: true));

        await RunSecondaryEffectAsync("spectator race results notification", () => _notificationService.CreateForRaceSpectatorsAsync(
            raceId,
            "Official Race Results Published",
            $"Official results for {race.RaceName} are now available. Open Results to see the published outcome; prediction settlement notifications will follow.",
            "SpectatorRaceResultsPublished",
            "/spectator/results",
            "Race",
            raceId,
            cancellationToken,
            preventDuplicates: true));

        await RunSecondaryEffectAsync("admin race results notification", () => _notificationService.CreateForAdminsAsync(
            "Official Race Results Published",
            $"Final report and {results.Count} result(s) for {race.RaceName} were approved and published successfully.",
            "AdminRaceResultsPublished",
            $"/admin/results?raceId={raceId}",
            "Race",
            raceId,
            cancellationToken,
            preventDuplicates: true));

        if (race.Tournament.Status == TournamentStatuses.Completed)
        {
            await RunSecondaryEffectAsync("tournament completed referee notification", () => _notificationService.CreateForTournamentRefereesAsync(
                race.TournamentId,
                "Tournament Completed",
                $"{race.Tournament.TournamentName} has been completed and all official results are published.",
                "RefereeTournamentCompleted",
                "/referee/races",
                "Tournament",
                race.TournamentId,
                cancellationToken,
                preventDuplicates: true));

            await RunSecondaryEffectAsync("tournament completed spectator notification", () => _notificationService.CreateForTournamentSpectatorsAsync(
                race.TournamentId,
                "Tournament Completed",
                $"{race.Tournament.TournamentName} is complete. Final results and prediction outcomes are available.",
                "SpectatorTournamentCompleted",
                "/spectator/results",
                "Tournament",
                race.TournamentId,
                cancellationToken,
                preventDuplicates: true));

            await RunSecondaryEffectAsync("tournament completed admin notification", () => _notificationService.CreateForAdminsAsync(
                "Tournament Completed",
                $"{race.Tournament.TournamentName} is now completed. All races are published or cancelled.",
                "AdminTournamentCompleted",
                "/admin/races",
                "Tournament",
                race.TournamentId,
                cancellationToken,
                preventDuplicates: true));
        }

        await RunSecondaryEffectAsync("save secondary effects", () => _context.SaveChangesAsync(cancellationToken));

        string? secondaryWarning = secondaryWarnings.Count > 0
            ? string.Join(" | ", secondaryWarnings)
            : null;

        PredictionEvaluationResult? evaluation = null;
        string? evaluationError = null;

        try
        {
            evaluation = await _predictionEvaluationService
                .EvaluateRacePredictionsAsync(raceId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Prediction evaluation failed for raceId={RaceId}", raceId);
            evaluationError = ex.Message;
        }

        IReadOnlyList<TournamentStanding>? standings = null;
        string? standingsError = null;

        try
        {
            standings = await _standingService
                .RecalculateAsync(race.TournamentId, false, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tournament standing recalculation failed for tournamentId={TournamentId}", race.TournamentId);
            standingsError = ex.Message;
        }

        return Ok(new
        {
            message = "Final report and all race results were approved successfully.",
            raceId,
            status = RaceStatuses.Published,
            reportStatus = RefereeReportStatuses.Approved,
            resultStatus = RaceResultStatuses.Published,
            publishedResults = results.Count,
            tournamentStatus = race.Tournament.Status,
            warning = secondaryWarning,
            predictionEvaluation = evaluation == null
                ? new
                {
                    success = false,
                    message = evaluationError ?? "Evaluation did not run.",
                    evaluated = 0,
                    payoutPoints = 0
                }
                : new
                {
                    success = evaluation.Success,
                    message = evaluation.Message,
                    evaluated = evaluation.NewlyEvaluated,
                    payoutPoints = evaluation.TotalPayoutPoints
                },
            tournamentStandings = new
            {
                success = standings != null,
                count = standings?.Count ?? 0,
                message = standingsError
            },
            tournamentReadyToFinalize = race.Tournament.Races.All(r =>
                r.Status is RaceStatuses.Published or RaceStatuses.Cancelled)
        });
    }

    [HttpPut("{id:int}/reject")]
    public async Task<IActionResult> RejectResult(
        int id,
        [FromBody] RejectRaceResultRequest? request,
        CancellationToken cancellationToken)
    {
        var result = await _context.RaceResults
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.ResultId == id, cancellationToken);

        if (result == null)
        {
            return NotFound(new AdminActionResponse
            {
                Message = "Race result not found",
                Id = id
            });
        }

        return BadRequest(new
        {
            code = "USE_ATOMIC_POST_RACE_RETURN",
            message = "A single result cannot be returned separately. Return the final post-race report so the report, all results, and race status are rolled back together.",
            resultId = result.ResultId,
            raceId = result.RaceId,
            currentStatus = result.Status,
            endpoint = "PUT /api/admin/reports/{finalReportId}/return",
            reason = request?.Reason
        });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteResult(int id, CancellationToken cancellationToken)
    {
        var result = await _context.RaceResults.FirstOrDefaultAsync(r => r.ResultId == id, cancellationToken);
        if (result == null) return NotFound(new AdminActionResponse { Message = "Race result not found", Id = id });
        if (result.Status is not RaceResultStatuses.Draft and not RaceResultStatuses.Returned)
            return Conflict(new AdminActionResponse
            {
                Message = "Only Draft or Returned results can be deleted. Published results require correction.",
                Id = id,
                Status = result.Status
            });

        _context.RaceResults.Remove(result);
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(new AdminActionResponse { Message = "Race result deleted successfully", Id = id });
    }

    private IQueryable<AdminRaceResultResponse> ResultQuery() =>
        _context.RaceResults
            .AsNoTracking()
            .Where(r => _context.PreRaceInspections.Any(inspection =>
                inspection.RaceId == r.RaceId &&
                inspection.RegistrationId == r.RegistrationId &&
                inspection.Status == PreRaceInspectionStatuses.Passed))
            .Select(r => new AdminRaceResultResponse
        {
            ResultId = r.ResultId,
            RaceId = r.RaceId,
            RegistrationId = r.RegistrationId,
            FinishTimeSeconds = r.FinishTimeSeconds,
            FinishPosition = r.FinishPosition,
            Score = r.Score,
            OutcomeStatus = r.OutcomeStatus,
            Status = r.Status,
            RevisionNumber = r.RevisionNumber,
            EnteredByRefereeId = r.EnteredByRefereeId,
            AdminConfirmedBy = r.AdminConfirmedBy,
            PublishedAt = r.PublishedAt,
            Note = r.Note,
            CreatedAt = r.CreatedAt,
            UpdatedAt = r.UpdatedAt
        });
}

public sealed class RejectRaceResultRequest
{
    public string? Reason { get; set; }
}
