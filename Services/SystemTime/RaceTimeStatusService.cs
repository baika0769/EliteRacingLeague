using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Admin;
using Eliteracingleague.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Services.SystemTime;

public class RaceTimeStatusService : IRaceTimeStatusService
{
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

        var predictionsToLock = await _context.RacePredictions
            .Where(p =>
                p.Status == RacePredictionStatuses.Pending &&
                p.Race.PredictionDeadline.HasValue &&
                p.Race.PredictionDeadline.Value <= now &&
                p.Race.Status != RaceStatuses.Cancelled &&
                p.Race.Tournament.Status != TournamentStatuses.Cancelled)
            .ToListAsync(cancellationToken);

        foreach (var prediction in predictionsToLock)
        {
            prediction.Status = RacePredictionStatuses.Locked;
            prediction.LockedAt = now;
            prediction.UpdatedAt = now;
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
            predictionsToLock.Count > 0 ||
            tournamentsToCloseRegistration.Count > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        return new SyncTimeStatusesResponse
        {
            Message = "Time statuses synchronized.",
            EffectiveUtcNow = effectiveUtcNow,
            ExpiredInvitations = expiredInvitations.Count,
            UpdatedRaces = 0,
            UpdatedTournaments = tournamentsToCloseRegistration.Count
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
        // Không tự chuyển sang Ongoing theo giờ đua; Referee phải bấm Start.

        if (tournament.StartDate <= localToday)
        {
            return TournamentStatuses.ClosedRegistration;
        }

        return TournamentStatuses.OpenRegistration;
    }
}
