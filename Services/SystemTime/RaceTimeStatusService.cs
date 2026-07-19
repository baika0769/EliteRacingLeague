using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Admin;
using Eliteracingleague.API.Models;
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

        var expiredInvitations = await _context.JockeyInvitations
            .Include(i => i.Registration)
                .ThenInclude(r => r.Race)
            .Include(i => i.Registration)
                .ThenInclude(r => r.JockeyInvitations)
            .Where(i =>
                i.Status == InvitationStatuses.Pending &&
                ((i.ExpiresAt.HasValue && i.ExpiresAt.Value <= localNow) ||
                 (i.Registration.Race.JockeySelectionDeadline.HasValue &&
                  i.Registration.Race.JockeySelectionDeadline.Value <= localNow)))
            .ToListAsync(cancellationToken);

        foreach (var invitation in expiredInvitations)
        {
            invitation.Status = InvitationStatuses.Expired;
            invitation.RespondedAt = effectiveUtcNow;
            invitation.ResponseNote = "Invitation expired automatically.";

            var registration = invitation.Registration;
            if (registration.Status == RaceRegistrationStatuses.JockeyInvited &&
                !registration.JockeyInvitations.Any(other =>
                    other.InvitationId != invitation.InvitationId &&
                    other.Status == InvitationStatuses.Pending))
            {
                registration.Status = RaceRegistrationStatuses.Approved;
            }
        }

        var predictionsToLock = await _context.RacePredictions
            .Where(p =>
                p.Status == RacePredictionStatuses.Pending &&
                p.Race.PredictionDeadline.HasValue &&
                p.Race.PredictionDeadline.Value <= localNow &&
                p.Race.Status != RaceStatuses.Cancelled &&
                p.Race.Tournament.Status != TournamentStatuses.Cancelled)
            .ToListAsync(cancellationToken);

        foreach (var prediction in predictionsToLock)
        {
            prediction.Status = RacePredictionStatuses.Locked;
            prediction.LockedAt = effectiveUtcNow;
            prediction.UpdatedAt = effectiveUtcNow;
        }

        /*
         * Registration lifecycle:
         * - Registration is allowed through the whole deadline date.
         * - From the following day, OpenRegistration becomes ClosedRegistration.
         * - A Scheduled race only becomes AssignedReferee after registration is closed
         *   and an active referee assignment exists.
         */
        var tournamentsToSynchronize = await _context.Tournaments
            .Include(t => t.Races)
                .ThenInclude(r => r.RefereeAssignments)
            .Where(t =>
                (t.Status == TournamentStatuses.OpenRegistration &&
                 t.StartDate < localToday) ||
                (RegistrationClosedTournamentStatuses.Contains(t.Status) &&
                 t.Races.Any(r =>
                     r.Status == RaceStatuses.Scheduled &&
                     r.RefereeAssignments.Any(a =>
                         a.Status == RefereeAssignmentStatuses.Assigned))))
            .ToListAsync(cancellationToken);

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

            foreach (var race in tournament.Races)
            {
                var hasActiveRefereeAssignment = race.RefereeAssignments.Any(a =>
                    a.Status == RefereeAssignmentStatuses.Assigned);

                if (race.Status == RaceStatuses.Scheduled &&
                    hasActiveRefereeAssignment)
                {
                    race.Status = RaceStatuses.AssignedReferee;
                    race.UpdatedAt = effectiveUtcNow;
                    updatedRaces++;
                }
            }
        }

        if (expiredInvitations.Count > 0 ||
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
            ExpiredInvitations = expiredInvitations.Count,
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
            .Include(t => t.Races)
                .ThenInclude(r => r.RefereeAssignments)
            .Include(t => t.Races)
                .ThenInclude(r => r.PreRaceInspections)
            .Where(t =>
                t.Status == TournamentStatuses.OpenRegistration ||
                t.Status == TournamentStatuses.ClosedRegistration)
            .ToListAsync(cancellationToken);

        var updatedTournaments = 0;
        var updatedRaces = 0;

        foreach (var tournament in tournaments)
        {
            var desiredTournamentStatus = tournament.StartDate < localToday
                ? TournamentStatuses.ClosedRegistration
                : TournamentStatuses.OpenRegistration;

            if (tournament.Status != desiredTournamentStatus)
            {
                tournament.Status = desiredTournamentStatus;
                tournament.UpdatedAt = effectiveUtcNow;
                updatedTournaments++;
            }

            foreach (var race in tournament.Races)
            {
                var hasActiveRefereeAssignment = race.RefereeAssignments.Any(a =>
                    a.Status == RefereeAssignmentStatuses.Assigned);

                if (desiredTournamentStatus == TournamentStatuses.ClosedRegistration &&
                    race.Status == RaceStatuses.Scheduled &&
                    hasActiveRefereeAssignment)
                {
                    race.Status = RaceStatuses.AssignedReferee;
                    race.UpdatedAt = effectiveUtcNow;
                    updatedRaces++;
                    continue;
                }

                // Clear Override may move the clock back before the deadline.
                // Only safely revert an untouched pre-race state.
                if (desiredTournamentStatus == TournamentStatuses.OpenRegistration &&
                    race.Status == RaceStatuses.AssignedReferee &&
                    race.PreRaceInspections.Count == 0)
                {
                    race.Status = RaceStatuses.Scheduled;
                    race.UpdatedAt = effectiveUtcNow;
                    updatedRaces++;
                }
            }
        }

        if (updatedTournaments > 0 || updatedRaces > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        return new SyncTimeStatusesResponse
        {
            Message = "Tournament and race statuses recalculated using current real time.",
            EffectiveUtcNow = effectiveUtcNow,
            ExpiredInvitations = 0,
            UpdatedRaces = updatedRaces,
            UpdatedTournaments = updatedTournaments
        };
    }
}
