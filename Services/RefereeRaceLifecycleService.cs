using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Referee;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Services;

public class RefereeRaceLifecycleService
{
    private static readonly string[] ActiveRegistrationStatuses =
    {
        RaceRegistrationStatuses.Approved,
        RaceRegistrationStatuses.JockeyInvited,
        RaceRegistrationStatuses.ReadyToRace
    };

    private static readonly string[] ViolationAllowedRaceStatuses =
    {
        RaceStatuses.AssignedReferee,
        RaceStatuses.RefereeReady,
        RaceStatuses.Ongoing,
        RaceStatuses.Finished
    };

    private readonly EliteRacingLeagueContext _context;

    public RefereeRaceLifecycleService(EliteRacingLeagueContext context)
    {
        _context = context;
    }

    public Task<bool> RaceExistsAsync(int raceId)
    {
        return _context.Races
            .AsNoTracking()
            .AnyAsync(r => r.RaceId == raceId);
    }

    public async Task<RefereeRaceLifecycleResponse?> GetLifecycleAsync(int raceId, int refereeId)
    {
        var race = await _context.Races
            .AsNoTracking()
            .Where(r => r.RaceId == raceId)
            .Select(r => new
            {
                r.RaceId,
                r.TournamentId,
                r.Status,
                TournamentName = r.Tournament.TournamentName,
                TournamentStatus = r.Tournament.Status
            })
            .FirstOrDefaultAsync();

        if (race == null)
        {
            return null;
        }

        var assigned = await IsAssignedAsync(raceId, refereeId);

        if (!assigned)
        {
            return null;
        }

        var registrationIds = await _context.RaceRegistrations
            .AsNoTracking()
            .Where(r =>
                r.RaceId == raceId &&
                ActiveRegistrationStatuses.Contains(r.Status))
            .Select(r => r.RegistrationId)
            .ToListAsync();

        var totalRegistrations = registrationIds.Count;

        var inspections = registrationIds.Count == 0
            ? new List<string>()
            : await _context.PreRaceInspections
                .AsNoTracking()
                .Where(i =>
                    i.RaceId == raceId &&
                    registrationIds.Contains(i.RegistrationId))
                .Select(i => i.Status)
                .ToListAsync();

        var passedInspections = inspections.Count(s => s == PreRaceInspectionStatuses.Passed);
        var failedInspections = inspections.Count(s => s == PreRaceInspectionStatuses.Failed);
        var pendingInspections = Math.Max(0, totalRegistrations - passedInspections - failedInspections);

        var resultStatuses = await _context.RaceResults
            .AsNoTracking()
            .Where(r =>
                r.RaceId == raceId &&
                r.EnteredByRefereeId == refereeId)
            .Select(r => r.Status)
            .ToListAsync();

        var counts = new RefereeLifecycleCountsResponse
        {
            TotalRegistrations = totalRegistrations,
            PassedInspections = passedInspections,
            FailedInspections = failedInspections,
            PendingInspections = pendingInspections,
            DraftResults = resultStatuses.Count(s => s == RaceResultStatuses.Draft),
            RefereeConfirmedResults = resultStatuses.Count(s => s == RaceResultStatuses.RefereeConfirmed),
            AdminApprovedResults = resultStatuses.Count(s =>
                s == RaceResultStatuses.AdminApproved ||
                s == RaceResultStatuses.Published)
        };

        var eligibleRegistrations = Math.Max(0, totalRegistrations - failedInspections);
        var actions = BuildAllowedActions(race.Status, counts, eligibleRegistrations);

        return new RefereeRaceLifecycleResponse
        {
            RaceId = race.RaceId,
            TournamentId = race.TournamentId,
            TournamentName = race.TournamentName,
            RaceStatus = race.Status,
            TournamentStatus = race.TournamentStatus,
            CurrentStage = GetCurrentStage(race.Status),
            NextStage = GetNextStage(race.Status),
            AllowedActions = actions,
            Counts = counts,
            BlockingReason = GetBlockingReason(race.Status, counts, eligibleRegistrations)
        };
    }

    private Task<bool> IsAssignedAsync(int raceId, int refereeId)
    {
        return _context.RefereeAssignments
            .AsNoTracking()
            .AnyAsync(a =>
                a.RaceId == raceId &&
                a.RefereeId == refereeId &&
                a.Status == RefereeAssignmentStatuses.Assigned);
    }

    private static RefereeAllowedActionsResponse BuildAllowedActions(
        string raceStatus,
        RefereeLifecycleCountsResponse counts,
        int eligibleRegistrations)
    {
        var hasRegistrations = counts.TotalRegistrations > 0;
        var inspectionsComplete = hasRegistrations && counts.PendingInspections == 0;
        var hasEligibleRegistrations = eligibleRegistrations > 0;

        return new RefereeAllowedActionsResponse
        {
            CanInspect = raceStatus == RaceStatuses.AssignedReferee,
            CanSubmitPreRaceReport = raceStatus == RaceStatuses.AssignedReferee &&
                inspectionsComplete,
            CanMarkReady = raceStatus == RaceStatuses.AssignedReferee &&
                inspectionsComplete &&
                hasEligibleRegistrations,
            CanStartRace = raceStatus == RaceStatuses.RefereeReady &&
                inspectionsComplete &&
                hasEligibleRegistrations,
            CanFinishRace = raceStatus == RaceStatuses.Ongoing,
            CanEnterResults = raceStatus == RaceStatuses.Finished,
            CanConfirmResults = raceStatus == RaceStatuses.Finished &&
                counts.DraftResults > 0 &&
                counts.DraftResults + counts.RefereeConfirmedResults >= eligibleRegistrations &&
                hasEligibleRegistrations,
            CanSubmitPostRaceReport = raceStatus is RaceStatuses.Finished or RaceStatuses.ResultPending,
            CanReportViolation = ViolationAllowedRaceStatuses.Contains(raceStatus)
        };
    }

    private static string GetCurrentStage(string raceStatus)
    {
        return raceStatus switch
        {
            RaceStatuses.Scheduled => "Setup",
            RaceStatuses.AssignedReferee => "PreRaceInspection",
            RaceStatuses.RefereeReady => "ReadyToStart",
            RaceStatuses.Ongoing => "LiveRace",
            RaceStatuses.Finished => "PostRaceResults",
            RaceStatuses.ResultPending => "WaitingAdminApproval",
            RaceStatuses.Published => "Published",
            RaceStatuses.Cancelled => "Cancelled",
            _ => "Unknown"
        };
    }

    private static string? GetNextStage(string raceStatus)
    {
        return raceStatus switch
        {
            RaceStatuses.Scheduled => RaceStatuses.AssignedReferee,
            RaceStatuses.AssignedReferee => RaceStatuses.RefereeReady,
            RaceStatuses.RefereeReady => RaceStatuses.Ongoing,
            RaceStatuses.Ongoing => RaceStatuses.Finished,
            RaceStatuses.Finished => RaceStatuses.ResultPending,
            RaceStatuses.ResultPending => RaceStatuses.Published,
            RaceStatuses.Published => null,
            RaceStatuses.Cancelled => null,
            _ => null
        };
    }

    private static string? GetBlockingReason(
        string raceStatus,
        RefereeLifecycleCountsResponse counts,
        int eligibleRegistrations)
    {
        if (raceStatus == RaceStatuses.Cancelled)
        {
            return "Race is cancelled.";
        }

        if (raceStatus == RaceStatuses.ResultPending)
        {
            return "Race is waiting for admin approval.";
        }

        if (raceStatus == RaceStatuses.Published)
        {
            return "Race result has been published.";
        }

        if (counts.TotalRegistrations == 0 &&
            raceStatus is RaceStatuses.AssignedReferee or RaceStatuses.RefereeReady or RaceStatuses.Finished)
        {
            return "No eligible registrations found.";
        }

        if (counts.PendingInspections > 0 &&
            raceStatus is RaceStatuses.AssignedReferee or RaceStatuses.RefereeReady)
        {
            return "There are pending inspections.";
        }

        if (eligibleRegistrations == 0 &&
            raceStatus is RaceStatuses.AssignedReferee or RaceStatuses.RefereeReady or RaceStatuses.Finished)
        {
            return "No eligible registrations found.";
        }

        return null;
    }
}
