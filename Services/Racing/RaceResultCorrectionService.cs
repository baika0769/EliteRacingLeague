using System.Data;
using System.Text.Json;
using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.Models;
using Eliteracingleague.API.Services.Auditing;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Services.Racing;

public class RaceResultCorrectionService
{
    private readonly EliteRacingLeagueContext _context;
    private readonly RacePredictionSettlementService _settlementService;
    private readonly IAuditService _auditService;

    public RaceResultCorrectionService(
        EliteRacingLeagueContext context,
        RacePredictionSettlementService settlementService,
        IAuditService auditService)
    {
        _context = context;
        _settlementService = settlementService;
        _auditService = auditService;
    }

    public async Task<ResultCorrectionSummary> ReopenAsync(
        int raceId,
        int adminId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        var race = await _context.Races
            .Include(r => r.Tournament).ThenInclude(t => t.Season)
            .Include(r => r.RaceResults)
            .Include(r => r.PrizeAwards).ThenInclude(a => a.Payouts)
            .Include(r => r.RefereeReports)
            .Include(r => r.RefereeAssignments)
            .FirstOrDefaultAsync(r => r.RaceId == raceId, cancellationToken)
            ?? throw new InvalidOperationException("Race not found.");

        if (race.Status != RaceStatuses.Published)
            throw new InvalidOperationException("Only a published race can enter result correction.");

        if (race.Tournament.Season.Status != SeasonStatuses.Active)
            throw new InvalidOperationException("Published results cannot be reopened after season settlement starts.");

        var lockedPayout = race.PrizeAwards
            .SelectMany(a => a.Payouts)
            .FirstOrDefault(p => p.Status is PrizeAwardStatuses.UnderReview or PrizeAwardStatuses.Paid);
        if (lockedPayout != null)
            throw new InvalidOperationException("An owner or jockey payout is under review or paid. Resolve it before reopening results.");

        if (race.RaceResults.Count == 0)
            throw new InvalidOperationException("The race has no result to reopen.");

        var snapshot = race.RaceResults.Select(r => new
        {
            r.ResultId,
            r.RegistrationId,
            r.FinishTimeSeconds,
            r.FinishPosition,
            r.Score,
            r.OutcomeStatus,
            r.Status,
            r.AdminConfirmedBy,
            r.PublishedAt,
            r.Note,
            r.RevisionNumber
        }).ToList();

        var nextVersion = race.RaceResults.Max(r => r.RevisionNumber) + 1;
        var now = DateTime.UtcNow;

        foreach (var result in race.RaceResults)
        {
            _context.RaceResultRevisions.Add(new RaceResultRevision
            {
                RaceId = raceId,
                ResultId = result.ResultId,
                RegistrationId = result.RegistrationId,
                VersionNumber = nextVersion,
                ChangeType = AuditActionTypes.ReopenResult,
                SnapshotJson = JsonSerializer.Serialize(new
                {
                    result.ResultId,
                    result.RegistrationId,
                    result.FinishTimeSeconds,
                    result.FinishPosition,
                    result.Score,
                    result.OutcomeStatus,
                    result.Status,
                    result.AdminConfirmedBy,
                    result.PublishedAt,
                    result.Note
                }),
                Reason = reason.Trim(),
                ChangedByUserId = adminId,
                CreatedAt = now
            });
        }

        var settlement = await _settlementService.ReverseForResultCorrectionAsync(
            raceId, reason, cancellationToken);

        // A published prize is provisional until the correction is approved.
        // Remove ReadyToClaim/Rejected awards now; ApproveAllResults recreates
        // them from the corrected ranking. UnderReview/Paid awards are blocked
        // above and can never be silently removed.
        _context.PrizeAwards.RemoveRange(race.PrizeAwards);
        var oldRaceStatus = race.Status;

        var finalPostRaceReport = race.RefereeReports
            .Where(report => report.ReportType == RefereeReportTypes.PostRace)
            .OrderByDescending(report => report.ReportId)
            .FirstOrDefault();

        if (finalPostRaceReport != null)
        {
            finalPostRaceReport.Status = RefereeReportStatuses.Returned;
            finalPostRaceReport.ReturnReasonCategory = RefereeReportReturnReasonCategories.Other;
            finalPostRaceReport.ReturnReason = reason.Trim();
            finalPostRaceReport.ReviewedByAdminId = adminId;
            finalPostRaceReport.ReviewedAt = now;
            finalPostRaceReport.UpdatedAt = now;
        }

        foreach (var result in race.RaceResults)
        {
            result.Status = RaceResultStatuses.Returned;
            result.AdminConfirmedBy = null;
            result.PublishedAt = null;
            result.RevisionNumber = nextVersion;
            result.Note = string.IsNullOrWhiteSpace(result.Note)
                ? $"Reopened for correction: {reason.Trim()}"
                : $"{result.Note}\nReopened for correction: {reason.Trim()}";
            result.UpdatedAt = now;
        }

        race.Status = RaceStatuses.Finished;
        race.LifecycleVersion++;
        race.UpdatedAt = now;

        if (race.Tournament.Status == TournamentStatuses.Completed)
        {
            race.Tournament.Status = TournamentStatuses.Ongoing;
            race.Tournament.UpdatedAt = now;
        }

        var standings = await _context.TournamentStandings
            .Where(s => s.TournamentId == race.TournamentId)
            .ToListAsync(cancellationToken);
        _context.TournamentStandings.RemoveRange(standings);

        var participants = await _context.RaceRegistrations.AsNoTracking()
            .Where(r => r.RaceId == raceId)
            .Select(r => new { r.OwnerId, r.JockeyId })
            .ToListAsync(cancellationToken);

        var affectedUsers = participants
            .SelectMany(r => r.JockeyId.HasValue
                ? new[] { r.OwnerId, r.JockeyId.Value }
                : new[] { r.OwnerId })
            .Distinct()
            .ToList();

        affectedUsers.AddRange(race.RefereeAssignments.Select(a => a.RefereeId));
        foreach (var userId in affectedUsers.Distinct())
        {
            _context.Notifications.Add(new Notification
            {
                UserId = userId,
                Title = "Race Result Reopened",
                Message = $"Results for {race.RaceName} were reopened for correction. Reason: {reason.Trim()}",
                IsRead = false,
                CreatedAt = now,
                ActionType = "RaceResultCorrection",
                ActionUrl = "/results",
                RelatedType = "Race",
                RelatedId = raceId
            });
        }

        await _auditService.WriteAsync(adminId, AuditActionTypes.ReopenResult,
            "Race", raceId.ToString(),
            new { Status = oldRaceStatus, Results = snapshot },
            new
            {
                Status = race.Status,
                Revision = nextVersion,
                ReportStatus = finalPostRaceReport?.Status,
                ReversedPrizeAwards = race.PrizeAwards.Count
            }, reason,
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new ResultCorrectionSummary(
            raceId,
            race.RaceResults.Count,
            settlement.PredictionsAffected,
            settlement.PayoutPointsReversed,
            nextVersion);
    }
}

public sealed record ResultCorrectionSummary(
    int RaceId,
    int ResultsReopened,
    int PredictionsReset,
    int PayoutPointsReversed,
    int RevisionNumber);
