using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Admin;
using Eliteracingleague.API.Services.Racing;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Services.SystemTime;

public class RaceTimeStatusService : IRaceTimeStatusService
{
    private static readonly string[] RegistrationClosedTournamentStatuses =
    {
        TournamentStatuses.ClosedRegistration,
        TournamentStatuses.Ongoing
    };

    private readonly EliteRacingLeagueContext _context;
    private readonly IDateTimeProvider _dateTimeProvider;

    public RaceTimeStatusService(
        EliteRacingLeagueContext context,
        IDateTimeProvider dateTimeProvider)
    {
        _context = context;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<SyncTimeStatusesResponse> SyncAsync(
        CancellationToken cancellationToken = default)
    {
        var effectiveUtcNow = _dateTimeProvider.UtcNow;
        var localNow = _dateTimeProvider.GetLocalNow(
            _dateTimeProvider.TimeZoneId);
        var localToday = DateOnly.FromDateTime(localNow);

        var predictionsToLock = await _context.RacePredictions
            .Where(prediction =>
                prediction.Status == RacePredictionStatuses.Pending &&
                prediction.Race.PredictionDeadline.HasValue &&
                prediction.Race.PredictionDeadline.Value <= localNow &&
                prediction.Race.Status != RaceStatuses.Cancelled &&
                prediction.Race.Tournament.Status != TournamentStatuses.Cancelled)
            .ToListAsync(cancellationToken);

        foreach (var prediction in predictionsToLock)
        {
            prediction.Status = RacePredictionStatuses.Locked;
            prediction.LockedAt = effectiveUtcNow;
            prediction.UpdatedAt = effectiveUtcNow;
        }

        /*
         * Registration lifecycle:
         * - Registration and jockey selection are allowed through the deadline date.
         * - From the following day, the tournament becomes ClosedRegistration.
         * - Closing registration expires all pending jockey invitations and cancels
         *   registrations that never confirmed an official jockey.
         * - A Scheduled race becomes AssignedReferee only after registration closes
         *   and an active referee assignment exists.
         */
        var tournamentsToSynchronize = await _context.Tournaments
            .Include(tournament => tournament.Races)
                .ThenInclude(race => race.RefereeAssignments)
            .Where(tournament =>
                (tournament.Status == TournamentStatuses.OpenRegistration &&
                 tournament.StartDate < localToday) ||
                (RegistrationClosedTournamentStatuses.Contains(tournament.Status) &&
                 tournament.Races.Any(race =>
                     race.Status == RaceStatuses.Scheduled &&
                     race.RefereeAssignments.Any(assignment =>
                         assignment.Status == RefereeAssignmentStatuses.Assigned))))
            .ToListAsync(cancellationToken);

        var alreadyClosedTournamentIds = await _context.Tournaments
            .AsNoTracking()
            .Where(tournament =>
                RegistrationClosedTournamentStatuses.Contains(tournament.Status))
            .Select(tournament => tournament.TournamentId)
            .ToListAsync(cancellationToken);

        var closedTournamentIds = new HashSet<int>(alreadyClosedTournamentIds);
        var updatedTournaments = 0;
        var updatedRaces = 0;

        foreach (var tournament in tournamentsToSynchronize)
        {
            if (tournament.Status == TournamentStatuses.OpenRegistration &&
                tournament.StartDate < localToday)
            {
                tournament.Status = TournamentStatuses.ClosedRegistration;
                tournament.UpdatedAt = effectiveUtcNow;
                updatedTournaments++;
            }

            if (!RegistrationClosedTournamentStatuses.Contains(tournament.Status))
            {
                continue;
            }

            closedTournamentIds.Add(tournament.TournamentId);

            foreach (var race in tournament.Races)
            {
                var hasActiveRefereeAssignment = race.RefereeAssignments.Any(assignment =>
                    assignment.Status == RefereeAssignmentStatuses.Assigned);

                if (race.Status == RaceStatuses.Scheduled &&
                    hasActiveRefereeAssignment)
                {
                    race.Status = RaceStatuses.AssignedReferee;
                    race.UpdatedAt = effectiveUtcNow;
                    updatedRaces++;
                }
            }
        }

        var closureResult = await RegistrationClosureHelper.ApplyAsync(
            _context,
            closedTournamentIds,
            effectiveUtcNow,
            cancellationToken);

        if (closureResult.HasChanges ||
            predictionsToLock.Count > 0 ||
            updatedTournaments > 0 ||
            updatedRaces > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        return new SyncTimeStatusesResponse
        {
            Message = "Time statuses synchronized.",
            EffectiveUtcNow = effectiveUtcNow,
            ExpiredInvitations = closureResult.ExpiredInvitations,
            UpdatedRaces = updatedRaces,
            UpdatedTournaments = updatedTournaments
        };
    }

    public async Task<SyncTimeStatusesResponse> RecalculateTournamentStatusesAsync(
        CancellationToken cancellationToken = default)
    {
        var effectiveUtcNow = _dateTimeProvider.UtcNow;
        var localNow = _dateTimeProvider.GetLocalNow(
            _dateTimeProvider.TimeZoneId);
        var localToday = DateOnly.FromDateTime(localNow);

        var tournaments = await _context.Tournaments
            .Include(tournament => tournament.Races)
                .ThenInclude(race => race.RefereeAssignments)
            .Include(tournament => tournament.Races)
                .ThenInclude(race => race.PreRaceInspections)
            .Where(tournament =>
                tournament.Status == TournamentStatuses.OpenRegistration ||
                tournament.Status == TournamentStatuses.ClosedRegistration)
            .ToListAsync(cancellationToken);

        var updatedTournaments = 0;
        var updatedRaces = 0;
        var closedTournamentIds = new HashSet<int>();

        foreach (var tournament in tournaments)
        {
            // Registration closure is one-way because it expires invitations and
            // cancels incomplete registrations. Moving the clock backward must not
            // reopen a tournament that has already been closed.
            var desiredTournamentStatus =
                tournament.Status == TournamentStatuses.ClosedRegistration ||
                tournament.StartDate < localToday
                    ? TournamentStatuses.ClosedRegistration
                    : TournamentStatuses.OpenRegistration;

            if (tournament.Status != desiredTournamentStatus)
            {
                tournament.Status = desiredTournamentStatus;
                tournament.UpdatedAt = effectiveUtcNow;
                updatedTournaments++;
            }

            if (desiredTournamentStatus == TournamentStatuses.ClosedRegistration)
            {
                closedTournamentIds.Add(tournament.TournamentId);
            }

            foreach (var race in tournament.Races)
            {
                var hasActiveRefereeAssignment = race.RefereeAssignments.Any(assignment =>
                    assignment.Status == RefereeAssignmentStatuses.Assigned);

                if (desiredTournamentStatus == TournamentStatuses.ClosedRegistration &&
                    race.Status == RaceStatuses.Scheduled &&
                    hasActiveRefereeAssignment)
                {
                    race.Status = RaceStatuses.AssignedReferee;
                    race.UpdatedAt = effectiveUtcNow;
                    updatedRaces++;
                    continue;
                }
            }
        }

        var closureResult = await RegistrationClosureHelper.ApplyAsync(
            _context,
            closedTournamentIds,
            effectiveUtcNow,
            cancellationToken);

        if (closureResult.HasChanges ||
            updatedTournaments > 0 ||
            updatedRaces > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        return new SyncTimeStatusesResponse
        {
            Message = "Tournament and race statuses recalculated using current real time.",
            EffectiveUtcNow = effectiveUtcNow,
            ExpiredInvitations = closureResult.ExpiredInvitations,
            UpdatedRaces = updatedRaces,
            UpdatedTournaments = updatedTournaments
        };
    }
}
