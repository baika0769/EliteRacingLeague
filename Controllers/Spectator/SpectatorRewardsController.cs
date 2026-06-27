using System.Security.Claims;
using Eliteracingleague.API.Data;
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
    private readonly Eliteracingleague.API.Services.SpectatorLeaderboardService _leaderboardService;

    public SpectatorRewardsController(
        EliteRacingLeagueContext context,
        Eliteracingleague.API.Services.SpectatorLeaderboardService leaderboardService)
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

        var totalPredictions = await _context.RacePredictions
            .CountAsync(p => p.SpectatorId == userId);

        var correctPredictions = await _context.RacePredictions
            .CountAsync(p => p.SpectatorId == userId && p.IsCorrect == true);

        var rewardPoints = await _context.RacePredictions
            .Where(p => p.SpectatorId == userId)
            .SumAsync(p => p.PointsAwarded);

        var accuracy = totalPredictions == 0
            ? 0
            : Math.Round((decimal)correctPredictions / totalPredictions * 100, 2);

        var myRank = await _leaderboardService.GetMyRankAsync(userId);
        var totalDays = await _leaderboardService.GetActiveSeasonTotalDaysAsync();

        var pointHistory = await _context.RacePredictions
            .Where(p => p.SpectatorId == userId && p.PointsAwarded > 0)
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
            correctPredictions,
            rewardPoints,
            predictionAccuracy = accuracy,
            myRank,
            totalDays,
            pointHistory
        });
    }
}
