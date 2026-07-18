using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Controllers.Admin;

[Authorize(Roles = UserRoles.Admin)]
[ApiController]
[Route("api/admin/dashboard")]
public class AdminDashboardController : ControllerBase
{
    private readonly EliteRacingLeagueContext _context;

    public AdminDashboardController(EliteRacingLeagueContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetDashboard(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var adminIdText = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        _ = int.TryParse(adminIdText, out var adminId);

        var usersByRole = await _context.Users.AsNoTracking()
            .GroupBy(u => u.Role)
            .Select(g => new { Role = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Role, x => x.Count, cancellationToken);

        var response = new AdminDashboardResponse
        {
            TotalUsers = await _context.Users.CountAsync(cancellationToken),
            ActiveUsers = await _context.Users.CountAsync(u => u.Status == UserStatuses.Active, cancellationToken),
            TotalSpectators = usersByRole.GetValueOrDefault(UserRoles.Spectator),
            TotalOwners = usersByRole.GetValueOrDefault(UserRoles.HorseOwner),
            TotalJockeys = usersByRole.GetValueOrDefault(UserRoles.Jockey),
            TotalReferees = usersByRole.GetValueOrDefault(UserRoles.RaceReferee),
            TotalHorses = await _context.Horses.CountAsync(cancellationToken),
            TotalSeasons = await _context.Seasons.CountAsync(cancellationToken),
            ActiveSeasons = await _context.Seasons.CountAsync(s => s.Status == SeasonStatuses.Active, cancellationToken),
            TotalRaces = await _context.Races.CountAsync(cancellationToken),
            UpcomingRaces = await _context.Races.CountAsync(r => r.RaceDate > now && r.Status != RaceStatuses.Cancelled && r.Status != RaceStatuses.Published, cancellationToken),
            OngoingRaces = await _context.Races.CountAsync(r => r.Status == RaceStatuses.Ongoing, cancellationToken),
            TotalTournaments = await _context.Tournaments.CountAsync(cancellationToken),
            ActiveTournaments = await _context.Tournaments.CountAsync(t => t.Status == TournamentStatuses.OpenRegistration || t.Status == TournamentStatuses.ClosedRegistration || t.Status == TournamentStatuses.Ongoing, cancellationToken),
            PendingRegistrations = await _context.RaceRegistrations.CountAsync(r => r.Status == RaceRegistrationStatuses.Pending, cancellationToken),
            PendingResults = await _context.RaceResults.CountAsync(r => r.Status == RaceResultStatuses.RefereeConfirmed, cancellationToken),
            PendingSeasonRewards = await _context.SeasonRewards.CountAsync(r => r.Status == SeasonRewardStatuses.Claimed || r.Status == SeasonRewardStatuses.Approved || r.Status == SeasonRewardStatuses.Preparing, cancellationToken),
            PendingPrizeClaims = await _context.PrizeAwards.CountAsync(r => r.Status == PrizeAwardStatuses.UnderReview, cancellationToken),
            TotalPredictions = await _context.RacePredictions.CountAsync(cancellationToken),
            TotalStakePoints = await _context.RacePredictions.SumAsync(p => (long)p.StakePoints, cancellationToken),
            TotalPayoutPoints = await _context.RacePredictions.SumAsync(p => (long)p.PointsAwarded, cancellationToken),
            UnreadAdminNotifications = adminId == 0 ? 0 : await _context.Notifications.CountAsync(n => n.UserId == adminId && !n.IsRead, cancellationToken),
            NextRaces = await _context.Races.AsNoTracking()
                .Where(r => r.RaceDate > now && r.Status != RaceStatuses.Cancelled && r.Status != RaceStatuses.Published)
                .OrderBy(r => r.RaceDate)
                .Take(5)
                .Select(r => new DashboardRaceItem
                {
                    RaceId = r.RaceId,
                    RaceName = r.RaceName,
                    TournamentName = r.Tournament.TournamentName,
                    RaceDate = r.RaceDate,
                    Status = r.Status,
                    RegisteredCount = r.RaceRegistrations.Count(x => x.Status != RaceRegistrationStatuses.Rejected && x.Status != RaceRegistrationStatuses.Cancelled && x.Status != RaceRegistrationStatuses.Withdrawn)
                }).ToListAsync(cancellationToken),
            RecentActivities = await _context.AuditLogs.AsNoTracking()
                .OrderByDescending(a => a.CreatedAt)
                .Take(10)
                .Select(a => new DashboardActivityItem
                {
                    AuditLogId = a.AuditLogId,
                    Action = a.Action,
                    EntityType = a.EntityType,
                    EntityId = a.EntityId,
                    UserName = a.User == null ? null : a.User.FullName,
                    Reason = a.Reason,
                    CreatedAt = a.CreatedAt
                }).ToListAsync(cancellationToken)
        };

        return Ok(response);
    }
}
