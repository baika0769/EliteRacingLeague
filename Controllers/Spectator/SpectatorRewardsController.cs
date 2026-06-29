using System.Security.Claims;
using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Controllers.Spectator;

[Authorize]
[ApiController]
[Route("api/spectator/rewards")]
public class SpectatorRewardsController : ControllerBase
{
    private readonly EliteRacingLeagueContext _context;
    private readonly SpectatorLeaderboardService _leaderboardService;

    public SpectatorRewardsController(
        EliteRacingLeagueContext context,
        SpectatorLeaderboardService leaderboardService)
    {
        _context = context;
        _leaderboardService = leaderboardService;
    }

    private int GetUserId()
        => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    [HttpGet]
    public async Task<IActionResult> GetRewards()
    {
        var userId = GetUserId();

        var rewardSummary = await _leaderboardService.GetRewardSummaryAsync(userId);
        var myRank = await _leaderboardService.GetMyRankAsync(userId);
        var totalDays = await _leaderboardService.GetActiveSeasonTotalDaysAsync();

        var pointHistory = await _context.RacePredictions
            .AsNoTracking()
            .Where(p =>
                p.SpectatorId == userId &&
                p.Status != RacePredictionStatuses.Cancelled &&
                p.PointsAwarded > 0)
            .OrderByDescending(p => p.EvaluatedAt ?? p.UpdatedAt ?? p.CreatedAt)
            .Select(p => new
            {
                predictionId = p.PredictionId,
                tournamentId = p.Race.TournamentId,
                tournamentName = p.Race.Tournament.TournamentName,
                raceName = p.Race.RaceName,
                points = p.PointsAwarded,
                rewardAmount = p.RewardAmount,
                rewardStatus = p.RewardStatus,
                evaluatedAt = p.EvaluatedAt,
                awardedAt = p.EvaluatedAt ?? p.UpdatedAt ?? p.CreatedAt
            })
            .ToListAsync();

        return Ok(new
        {
            rewardPoints = rewardSummary.RewardPoints,
            correctPredictions = rewardSummary.CorrectPredictions,
            predictionAccuracy = rewardSummary.PredictionAccuracy,
            myRank,
            totalDays,
            pointHistory
        });
    }
}