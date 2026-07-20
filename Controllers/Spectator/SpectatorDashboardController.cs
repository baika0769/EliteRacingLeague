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
        var activeSeason = await _leaderboardService.GetActiveSeasonAsync();
        var activeSeasonId = activeSeason?.SeasonId;

        var upcomingTournaments = await _context.Tournaments
            .AsNoTracking()
            .CountAsync(t =>
                (!activeSeasonId.HasValue || t.SeasonId == activeSeasonId.Value) &&
                t.Status != TournamentStatuses.Draft &&
                t.Status != TournamentStatuses.Cancelled &&
                t.Status != TournamentStatuses.Completed &&
                t.EndDate >= today);

        var predictionsSubmitted = activeSeasonId.HasValue
            ? await _context.RacePredictions
                .AsNoTracking()
                .CountAsync(p =>
                    p.SpectatorId == userId &&
                    p.Race.Tournament.SeasonId == activeSeasonId.Value &&
                    p.Status != RacePredictionStatuses.Cancelled)
            : 0;

        var rewardSummary = await _leaderboardService.GetRewardSummaryAsync(userId);
        var myRank = await _leaderboardService.GetMyRankAsync(userId);
        var bettingPoints = rewardSummary.BettingPoints;

        var featuredTournament = await _context.Tournaments
            .AsNoTracking()
            .Where(t =>
                (!activeSeasonId.HasValue || t.SeasonId == activeSeasonId.Value) &&
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
            activeSeasonId,
            activeSeasonName = activeSeason?.SeasonName,
            upcomingTournaments,
            predictionsSubmitted,
            bettingPoints,
            hasActiveSeason = rewardSummary.HasActiveSeason,
            baseOpeningPoints = rewardSummary.BaseOpeningPoints,
            carriedBonusPoints = rewardSummary.CarriedBonusPoints,
            openingTotalPoints = rewardSummary.OpeningTotalPoints,
            walletStatus = rewardSummary.WalletStatus,
            seasonScore = rewardSummary.RewardPoints,
            rewardPoints = rewardSummary.RewardPoints,
            netPoints = rewardSummary.NetPoints,
            totalStakePoints = rewardSummary.TotalStakePoints,
            totalPayoutPoints = rewardSummary.TotalPayoutPoints,
            myRank,
            featuredTournament
        });
    }
}