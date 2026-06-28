using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Admin;
using Eliteracingleague.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Services.SystemTime;

public class RaceTimeStatusService : IRaceTimeStatusService
{
    private static readonly string[] RaceStatusesBeforeStart =
    {
        RaceStatuses.Scheduled,
        RaceStatuses.AssignedReferee,
        RaceStatuses.RefereeReady
    };

    private static readonly string[] TournamentStatusesBeforeStart =
    {
        TournamentStatuses.OpenRegistration,
        TournamentStatuses.ClosedRegistration
    };

    private static readonly string[] RecalculatableTournamentStatuses =
    {
        TournamentStatuses.OpenRegistration,
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

    public async Task<SyncTimeStatusesResponse> SyncAsync(CancellationToken cancellationToken = default)
    {
        var effectiveUtcNow = _dateTimeProvider.UtcNow;
        var now = _dateTimeProvider.GetLocalNow(_dateTimeProvider.TimeZoneId);
        var localToday = DateOnly.FromDateTime(now);

        var expiredInvitations = await _context.JockeyInvitations
            .Include(i => i.Registration)
                .ThenInclude(r => r.Race)
            .Where(i =>
                i.Status == InvitationStatuses.Pending &&
                i.Registration.Race.JockeySelectionDeadline.HasValue &&
                i.Registration.Race.JockeySelectionDeadline.Value <= now)
            .ToListAsync(cancellationToken);

        foreach (var invitation in expiredInvitations)
        {
            invitation.Status = InvitationStatuses.Expired;
            invitation.RespondedAt = now;
        }

        var racesToStart = await _context.Races
            .Where(r =>
                RaceStatusesBeforeStart.Contains(r.Status) &&
                r.RaceDate <= now)
            .ToListAsync(cancellationToken);

        foreach (var race in racesToStart)
        {
            race.Status = RaceStatuses.Ongoing;
            race.UpdatedAt = now;
        }

        var tournamentsToStart = await _context.Tournaments
            .Where(t =>
                TournamentStatusesBeforeStart.Contains(t.Status) &&
                t.Race != null &&
                t.Race.RaceDate <= now)
            .ToListAsync(cancellationToken);

        foreach (var tournament in tournamentsToStart)
        {
            tournament.Status = TournamentStatuses.Ongoing;
            tournament.UpdatedAt = now;
        }

        var tournamentsToCloseRegistration = await _context.Tournaments
            .Where(t =>
                t.Status == TournamentStatuses.OpenRegistration &&
                t.StartDate <= localToday &&
                (t.Race == null || t.Race.RaceDate > now))
            .ToListAsync(cancellationToken);

        foreach (var tournament in tournamentsToCloseRegistration)
        {
            tournament.Status = TournamentStatuses.ClosedRegistration;
            tournament.UpdatedAt = now;
        }

        if (expiredInvitations.Count > 0 ||
            racesToStart.Count > 0 ||
            tournamentsToStart.Count > 0 ||
            tournamentsToCloseRegistration.Count > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        return new SyncTimeStatusesResponse
        {
            Message = "Time statuses synchronized.",
            EffectiveUtcNow = effectiveUtcNow,
            ExpiredInvitations = expiredInvitations.Count,
            UpdatedRaces = racesToStart.Count,
            UpdatedTournaments = tournamentsToStart.Count + tournamentsToCloseRegistration.Count
        };
    }

    public async Task<SyncTimeStatusesResponse> RecalculateTournamentStatusesAsync(CancellationToken cancellationToken = default)
    {
        var effectiveUtcNow = _dateTimeProvider.UtcNow;
        var now = _dateTimeProvider.GetLocalNow(_dateTimeProvider.TimeZoneId);
        var localToday = DateOnly.FromDateTime(now);

        var tournaments = await _context.Tournaments
            .Include(t => t.Race)
            .Where(t => RecalculatableTournamentStatuses.Contains(t.Status))
            .ToListAsync(cancellationToken);

        var updatedTournaments = 0;

        foreach (var tournament in tournaments)
        {
            var desiredStatus = GetDesiredTournamentStatus(tournament, now, localToday);

            if (tournament.Status == desiredStatus)
            {
                continue;
            }

            tournament.Status = desiredStatus;
            tournament.UpdatedAt = now;
            updatedTournaments++;
        }

        if (updatedTournaments > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        return new SyncTimeStatusesResponse
        {
            Message = "Tournament statuses recalculated using current real time.",
            EffectiveUtcNow = effectiveUtcNow,
            ExpiredInvitations = 0,
            UpdatedRaces = 0,
            UpdatedTournaments = updatedTournaments
        };
    }

    private static string GetDesiredTournamentStatus(
        Tournament tournament,
        DateTime now,
        DateOnly localToday)
    {
        if (tournament.Race != null && tournament.Race.RaceDate <= now)
        {
            return TournamentStatuses.Ongoing;
        }

        if (tournament.StartDate <= localToday)
        {
            return TournamentStatuses.ClosedRegistration;
        }

        return TournamentStatuses.OpenRegistration;
    }
}
