using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Referee;
using Eliteracingleague.API.Services.SystemTime;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Services;

public class RefereeRaceLifecycleService
{
    private static readonly string[] ActiveRegistrationStatuses =
    {
        RaceRegistrationStatuses.ReadyToRace
    };

    private static readonly string[] ViolationAllowedRaceStatuses =
    {
        RaceStatuses.AssignedReferee,
        RaceStatuses.RefereeReady,
        RaceStatuses.Ongoing,
        RaceStatuses.Finished,
        RaceStatuses.ResultPending
    };

    private readonly EliteRacingLeagueContext _context;
    private readonly IDateTimeProvider _dateTimeProvider;

    public RefereeRaceLifecycleService(
        EliteRacingLeagueContext context,
        IDateTimeProvider dateTimeProvider)
    {
        _context = context;
        _dateTimeProvider = dateTimeProvider;
    }

    public Task<bool> RaceExistsAsync(int raceId)
    {
        return _context.Races
            .AsNoTracking()
            .AnyAsync(r => r.RaceId == raceId);
    }

    public async Task<RefereeRaceLifecycleResponse?> GetLifecycleAsync(
        int raceId,
        int refereeId)
    {
        var race = await _context.Races
            .AsNoTracking()
            .Where(r => r.RaceId == raceId)
            .Select(r => new
            {
                r.RaceId,
                r.TournamentId,
                r.RaceDate,
                r.Status,
                TournamentName = r.Tournament.TournamentName,
                TournamentStatus = r.Tournament.Status,
                SeasonStatus = r.Tournament.Season.Status
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

        var passedInspections = inspections.Count(s =>
            s == PreRaceInspectionStatuses.Passed);
        var failedInspections = inspections.Count(s =>
            s == PreRaceInspectionStatuses.Failed);
        var pendingInspections = Math.Max(
            0,
            totalRegistrations - passedInspections - failedInspections);

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
            DraftResults = resultStatuses.Count(s =>
                s == RaceResultStatuses.Draft),
            RefereeConfirmedResults = resultStatuses.Count(s =>
                s == RaceResultStatuses.RefereeConfirmed),
            AdminApprovedResults = resultStatuses.Count(s =>
                s == RaceResultStatuses.AdminApproved ||
                s == RaceResultStatuses.Published)
        };

        var eligibleRegistrations = Math.Max(
            0,
            totalRegistrations - failedInspections);
        var localNow = _dateTimeProvider.GetLocalNow(
            _dateTimeProvider.TimeZoneId);

        var actions = BuildAllowedActions(
            race.Status,
            race.TournamentStatus,
            race.SeasonStatus,
            race.RaceDate,
            localNow,
            counts,
            eligibleRegistrations);

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
            BlockingReason = GetBlockingReason(
                race.Status,
                race.TournamentStatus,
                race.SeasonStatus,
                race.RaceDate,
                localNow,
                counts,
                eligibleRegistrations)
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
        string tournamentStatus,
        string seasonStatus,
        DateTime raceDate,
        DateTime localNow,
        RefereeLifecycleCountsResponse counts,
        int eligibleRegistrations)
    {
        var hasRegistrations = counts.TotalRegistrations > 0;
        var inspectionsComplete = hasRegistrations &&
            counts.PendingInspections == 0;
        var hasEligibleRegistrations = eligibleRegistrations > 0;
        var seasonIsActive = seasonStatus == SeasonStatuses.Active;
        var tournamentIsOperational = tournamentStatus != TournamentStatuses.Cancelled &&
            tournamentStatus != TournamentStatuses.Completed;
        var registrationIsClosed = tournamentStatus == TournamentStatuses.ClosedRegistration ||
            tournamentStatus == TournamentStatuses.Ongoing;
        var scheduledTimeReached = localNow >= raceDate;

        return new RefereeAllowedActionsResponse
        {
            CanInspect = seasonIsActive &&
                tournamentIsOperational &&
                raceStatus == RaceStatuses.AssignedReferee,

            CanSubmitPreRaceReport = seasonIsActive &&
                tournamentIsOperational &&
                raceStatus == RaceStatuses.AssignedReferee &&
                inspectionsComplete,

            CanMarkReady = seasonIsActive &&
                tournamentIsOperational &&
                registrationIsClosed &&
                raceStatus == RaceStatuses.AssignedReferee &&
                inspectionsComplete &&
                hasEligibleRegistrations,

            CanStartRace = seasonIsActive &&
                tournamentIsOperational &&
                registrationIsClosed &&
                scheduledTimeReached &&
                raceStatus == RaceStatuses.RefereeReady &&
                inspectionsComplete &&
                hasEligibleRegistrations,

            // Once a race has started, the referee must still be able to finish it
            // even if the surrounding season data later becomes inconsistent.
            CanFinishRace = raceStatus == RaceStatuses.Ongoing,

            CanEnterResults = raceStatus == RaceStatuses.Finished,

            CanConfirmResults = raceStatus == RaceStatuses.Finished &&
                counts.DraftResults > 0 &&
                counts.DraftResults + counts.RefereeConfirmedResults >= eligibleRegistrations &&
                hasEligibleRegistrations,

            CanSubmitPostRaceReport = raceStatus is RaceStatuses.Finished or
                RaceStatuses.ResultPending,

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
        string tournamentStatus,
        string seasonStatus,
        DateTime raceDate,
        DateTime localNow,
        RefereeLifecycleCountsResponse counts,
        int eligibleRegistrations)
    {
        if (raceStatus == RaceStatuses.Cancelled ||
            tournamentStatus == TournamentStatuses.Cancelled)
        {
            return "Race or tournament is cancelled.";
        }

        if (raceStatus == RaceStatuses.ResultPending)
        {
            return "Race is waiting for admin approval.";
        }

        if (raceStatus == RaceStatuses.Published ||
            tournamentStatus == TournamentStatuses.Completed)
        {
            return "Race result has been published.";
        }

        if (seasonStatus != SeasonStatuses.Active &&
            raceStatus is RaceStatuses.Scheduled or
                RaceStatuses.AssignedReferee or
                RaceStatuses.RefereeReady)
        {
            return $"Season is {seasonStatus}. Pre-race actions are unavailable.";
        }

        if (raceStatus == RaceStatuses.RefereeReady &&
            tournamentStatus != TournamentStatuses.ClosedRegistration &&
            tournamentStatus != TournamentStatuses.Ongoing)
        {
            return "Registration must be closed before the race can start.";
        }

        if (raceStatus == RaceStatuses.RefereeReady &&
            localNow < raceDate)
        {
            return $"Race cannot start before the scheduled time {raceDate:yyyy-MM-dd HH:mm}.";
        }

        if (counts.TotalRegistrations == 0 &&
            raceStatus is RaceStatuses.AssignedReferee or
                RaceStatuses.RefereeReady or
                RaceStatuses.Finished)
        {
            return "No eligible registrations found.";
        }

        if (counts.PendingInspections > 0 &&
            raceStatus is RaceStatuses.AssignedReferee or
                RaceStatuses.RefereeReady)
        {
            return "There are pending inspections.";
        }

        if (eligibleRegistrations == 0 &&
            raceStatus is RaceStatuses.AssignedReferee or
                RaceStatuses.RefereeReady or
                RaceStatuses.Finished)
        {
            return "No eligible registrations found.";
        }

        return null;
    }
}