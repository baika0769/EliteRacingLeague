using System.Security.Claims;
using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.Services;
using Eliteracingleague.API.Services.SystemTime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Controllers.Spectator;

[Authorize]
[ApiController]
[Route("api/spectator/dashboard")]
public class SpectatorDashboardController : ControllerBase
{
    private readonly EliteRacingLeagueContext _context;
    private readonly SpectatorLeaderboardService _leaderboardService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public SpectatorDashboardController(
        EliteRacingLeagueContext context,
        SpectatorLeaderboardService leaderboardService,
        IDateTimeProvider dateTimeProvider)
    {
        _context = context;
        _leaderboardService = leaderboardService;
        _dateTimeProvider = dateTimeProvider;
    }

    private int GetUserId()
        => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    [HttpGet]
    public async Task<IActionResult> GetDashboard()
    {
        var userId = GetUserId();
        var today = DateOnly.FromDateTime(_dateTimeProvider.UtcNow);

        var upcomingTournaments = await _context.Tournaments
            .AsNoTracking()
            .CountAsync(t =>
                t.Status != TournamentStatuses.Draft &&
                t.Status != TournamentStatuses.Cancelled &&
                t.Status != TournamentStatuses.Completed &&
                t.EndDate >= today);

        var predictionsSubmitted = await _context.RacePredictions
            .AsNoTracking()
            .CountAsync(p =>
                p.SpectatorId == userId &&
                p.Status != RacePredictionStatuses.Cancelled);

        var rewardSummary = await _leaderboardService.GetRewardSummaryAsync(userId);
        var myRank = await _leaderboardService.GetMyRankAsync(userId);

        var featuredTournament = await _context.Tournaments
            .AsNoTracking()
            .Where(t =>
                t.Status != TournamentStatuses.Draft &&
                t.Status != TournamentStatuses.Cancelled &&
                t.Status != TournamentStatuses.Completed &&
                t.EndDate >= today)
            .OrderBy(t => t.StartDate)
            .ThenByDescending(t => t.PrizePool)
            .Select(t => new
            {
                tournamentId = t.TournamentId,
                tournamentName = t.TournamentName,
                location = t.Location,
                prizePool = t.PrizePool,
                status = t.Status,
                race = _context.Races
                    .Where(r =>
                        r.TournamentId == t.TournamentId &&
                        r.Status != RaceStatuses.Cancelled)
                    .OrderBy(r => r.RaceDate)
                    .Select(r => new
                    {
                        raceId = r.RaceId,
                        raceName = r.RaceName,
                        raceDate = r.RaceDate,
                        distanceMeters = r.DistanceMeters,
                        maxHorses = r.MaxHorses,
                        status = r.Status
                    })
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync();

        return Ok(new
        {
            upcomingTournaments,
            predictionsSubmitted,
            rewardPoints = rewardSummary.RewardPoints,
            myRank,
            featuredTournament
        });
    }
}