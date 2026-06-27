using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Admin;
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
        var now = _dateTimeProvider.UtcNow;
        var localToday = DateOnly.FromDateTime(_dateTimeProvider.GetLocalNow(_dateTimeProvider.TimeZoneId));

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
                t.StartDate <= localToday)
            .ToListAsync(cancellationToken);

        foreach (var tournament in tournamentsToStart)
        {
            tournament.Status = TournamentStatuses.Ongoing;
            tournament.UpdatedAt = now;
        }

        if (expiredInvitations.Count > 0 || racesToStart.Count > 0 || tournamentsToStart.Count > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        return new SyncTimeStatusesResponse
        {
            Message = "Time statuses synchronized.",
            EffectiveUtcNow = now,
            ExpiredInvitations = expiredInvitations.Count,
            UpdatedRaces = racesToStart.Count,
            UpdatedTournaments = tournamentsToStart.Count
        };
    }
}
