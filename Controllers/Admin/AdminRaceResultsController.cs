using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Admin;
using Eliteracingleague.API.Extensions;
using Eliteracingleague.API.Models;
using Eliteracingleague.API.Services;
using Eliteracingleague.API.Services.Auditing;
using Eliteracingleague.API.Services.Racing;
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

    public AdminRaceResultsController(
        EliteRacingLeagueContext context,
        PredictionEvaluationService predictionEvaluationService,
        TournamentStandingService standingService,
        RaceResultValidationService validationService,
        IAuditService auditService)
    {
        _context = context;
        _predictionEvaluationService = predictionEvaluationService;
        _standingService = standingService;
        _validationService = validationService;
        _auditService = auditService;
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
        if (!User.TryGetUserId(out var adminId)) return Unauthorized();

        var race = await _context.Races
            .Include(r => r.Tournament).ThenInclude(t => t.Races)
            .FirstOrDefaultAsync(r => r.RaceId == raceId, cancellationToken);
        if (race == null)
            return NotFound(new AdminActionResponse { Message = "Race not found", Id = raceId });

        if (race.Status == RaceStatuses.Published)
            return Conflict(new AdminActionResponse
            {
                Message = "This race is already published. Use the result-correction endpoint before changing it.",
                Id = raceId,
                Status = race.Status
            });
        if (race.Status != RaceStatuses.ResultPending)
            return BadRequest(new AdminActionResponse
            {
                Message = "Only a race in ResultPending can be published.",
                Id = raceId,
                Status = race.Status
            });
        if (race.Tournament.Status == TournamentStatuses.Cancelled)
            return BadRequest(new AdminActionResponse { Message = "A cancelled tournament cannot publish results.", Id = raceId });

        var postRaceReports = await _context.RefereeReports
            .AsNoTracking()
            .Where(r =>
                r.RaceId == raceId &&
                r.ReportType == RefereeReportTypes.PostRace)
            .Select(r => new
            {
                r.ReportId,
                r.Status,
                r.RefereeId
            })
            .ToListAsync(cancellationToken);

        if (postRaceReports.Count == 0)
        {
            return BadRequest(new
            {
                code = "POST_RACE_REPORT_MISSING",
                message = "The final referee report must be submitted and approved before publishing race results.",
                raceId
            });
        }

        var unapprovedReport = postRaceReports.FirstOrDefault(r =>
            r.Status != RefereeReportStatuses.Approved);

        if (unapprovedReport != null)
        {
            return Conflict(new
            {
                code = "POST_RACE_REPORT_NOT_APPROVED",
                message = unapprovedReport.Status == RefereeReportStatuses.Returned
                    ? "The final referee report was returned and must be revised and resubmitted before publishing race results."
                    : "The final referee report is still waiting for admin approval.",
                raceId,
                reportId = unapprovedReport.ReportId,
                reportStatus = unapprovedReport.Status
            });
        }

        var registrations = await _context.RaceRegistrations
            .Where(r => r.RaceId == raceId &&
                        r.Status != RaceRegistrationStatuses.Cancelled &&
                        r.Status != RaceRegistrationStatuses.Rejected &&
                        r.Status != RaceRegistrationStatuses.Withdrawn)
            .ToListAsync(cancellationToken);
        if (registrations.Count == 0)
            return BadRequest(new AdminActionResponse { Message = "No eligible registrations were found.", Id = raceId });

        var registrationIds = registrations.Select(r => r.RegistrationId).ToHashSet();
        var results = await _context.RaceResults
            .Where(r => r.RaceId == raceId)
            .ToListAsync(cancellationToken);

        var dsqIds = (await _context.RaceViolations.AsNoTracking()
            .Where(v => v.RaceId == raceId &&
                        v.Action == RaceViolationActions.Disqualified &&
                        registrationIds.Contains(v.RegistrationId))
            .Select(v => v.RegistrationId)
            .Distinct()
            .ToListAsync(cancellationToken)).ToHashSet();

        foreach (var result in results.Where(r => dsqIds.Contains(r.RegistrationId)))
        {
            result.OutcomeStatus = RaceOutcomeStatuses.Disqualified;
            result.FinishPosition = null;
        }

        var validationErrors = _validationService.ValidateForPublication(results, registrationIds, dsqIds);
        if (validationErrors.Count > 0)
            return BadRequest(new { code = "INVALID_RACE_RESULTS", message = "Race results are not publishable.", errors = validationErrors });

        var prizeRules = await _context.PrizeRules.Where(r => r.RaceId == raceId)
            .ToDictionaryAsync(r => r.RankPosition, cancellationToken);
        var existingAwards = await _context.PrizeAwards.Where(a => a.RaceId == raceId).ToListAsync(cancellationToken);
        var lockedAward = existingAwards.FirstOrDefault(a =>
            a.Status is PrizeAwardStatuses.UnderReview or PrizeAwardStatuses.Paid);
        if (lockedAward != null)
            return Conflict(new AdminActionResponse
            {
                Message = "A prize is under review or already paid. Resolve it before republishing results.",
                Id = lockedAward.PrizeAwardId,
                Status = lockedAward.Status
            });

        var rankingSeed = results
            .Where(r => r.OutcomeStatus == RaceOutcomeStatuses.Finished)
            .OrderBy(r => r.FinishPosition)
            .ThenBy(r => r.FinishTimeSeconds)
            .ThenBy(r => r.ResultId)
            .ToList();

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            _context.PrizeAwards.RemoveRange(existingAwards);
            var now = DateTime.UtcNow;
            var registrationById = registrations.ToDictionary(r => r.RegistrationId);
            var officialRank = 0;

            foreach (var result in results)
            {
                if (result.OutcomeStatus == RaceOutcomeStatuses.Finished)
                    result.FinishPosition = ++officialRank;
                else
                    result.FinishPosition = null;

                result.AdminConfirmedBy = adminId;
                result.Status = RaceResultStatuses.Published;
                result.PublishedAt = now;
                result.UpdatedAt = now;

                var registration = registrationById[result.RegistrationId];
                registration.Status = RaceRegistrationStatuses.Completed;

                if (result.OutcomeStatus != RaceOutcomeStatuses.Finished ||
                    !result.FinishPosition.HasValue ||
                    !prizeRules.TryGetValue(result.FinishPosition.Value, out var prizeRule))
                    continue;

                _context.PrizeAwards.Add(new PrizeAward
                {
                    RaceId = raceId,
                    RegistrationId = result.RegistrationId,
                    OwnerId = registration.OwnerId,
                    JockeyId = registration.JockeyId,
                    RankPosition = result.FinishPosition.Value,
                    PrizeAmount = prizeRule.PrizeAmount,
                    Status = PrizeAwardStatuses.ReadyToClaim,
                    CreatedAt = now
                });
            }

            race.Status = RaceStatuses.Published;
            race.LifecycleVersion++;
            race.UpdatedAt = now;
            if (race.Tournament.Status is TournamentStatuses.ClosedRegistration or TournamentStatuses.OpenRegistration)
                race.Tournament.Status = TournamentStatuses.Ongoing;
            race.Tournament.UpdatedAt = now;

            await _auditService.WriteAsync(adminId, AuditActionTypes.Approve,
                "RaceResult", raceId.ToString(),
                new { RaceStatus = RaceStatuses.ResultPending },
                new { RaceStatus = RaceStatuses.Published, ResultCount = results.Count },
                "Bulk result publication", cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        PredictionEvaluationResult? evaluation = null;
        string? evaluationError = null;
        try
        {
            evaluation = await _predictionEvaluationService.EvaluateRacePredictionsAsync(raceId, cancellationToken);
        }
        catch (Exception ex)
        {
            evaluationError = ex.Message;
        }

        IReadOnlyList<TournamentStanding>? standings = null;
        string? standingsError = null;
        try
        {
            standings = await _standingService.RecalculateAsync(race.TournamentId, false, cancellationToken);
        }
        catch (Exception ex)
        {
            standingsError = ex.Message;
        }

        return Ok(new
        {
            message = "Race results were published. Prediction settlement and provisional tournament standings were requested.",
            raceId,
            status = RaceStatuses.Published,
            publishedResults = results.Count,
            predictionEvaluation = evaluation == null
                ? new { success = false, message = evaluationError ?? "Evaluation did not run.", evaluated = 0, payoutPoints = 0 }
                : new { success = evaluation.Success, message = evaluation.Message, evaluated = evaluation.NewlyEvaluated, payoutPoints = evaluation.TotalPayoutPoints },
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
        if (!User.TryGetUserId(out var adminId)) return Unauthorized();
        var result = await _context.RaceResults.FirstOrDefaultAsync(r => r.ResultId == id, cancellationToken);
        if (result == null) return NotFound(new AdminActionResponse { Message = "Race result not found", Id = id });
        if (result.Status is RaceResultStatuses.AdminApproved or RaceResultStatuses.Published)
            return Conflict(new AdminActionResponse { Message = "Published results require the correction workflow.", Id = id, Status = result.Status });

        var oldStatus = result.Status;
        var now = DateTime.UtcNow;
        result.Status = RaceResultStatuses.Returned;
        result.Note = string.IsNullOrWhiteSpace(request?.Reason) ? "Returned by admin" : request!.Reason.Trim();
        result.UpdatedAt = now;

        var race = await _context.Races.FirstOrDefaultAsync(r => r.RaceId == result.RaceId, cancellationToken);
        if (race?.Status == RaceStatuses.ResultPending)
        {
            race.Status = RaceStatuses.Finished;
            race.UpdatedAt = now;
        }

        await _auditService.WriteAsync(adminId, AuditActionTypes.Reject, "RaceResult", id.ToString(),
            new { Status = oldStatus }, new { Status = result.Status }, result.Note, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(new AdminActionResponse { Message = "Race result returned successfully", Id = id, Status = result.Status });
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
        _context.RaceResults.AsNoTracking().Select(r => new AdminRaceResultResponse
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
