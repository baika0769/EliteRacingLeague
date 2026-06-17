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

    public SpectatorRewardsController(EliteRacingLeagueContext context)
    {
        _context = context;
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

        var pointHistory = await _context.RacePredictions
            .Where(p => p.SpectatorId == userId && p.PointsAwarded > 0)
            .OrderByDescending(p => p.EvaluatedAt)
            .Select(p => new
            {
                predictionId = p.PredictionId,
                raceName = p.Race.RaceName,
                points = p.PointsAwarded,
                rewardAmount = p.RewardAmount,
                rewardStatus = p.RewardStatus,
                evaluatedAt = p.EvaluatedAt
            })
            .ToListAsync();

        return Ok(new
        {
            correctPredictions,
            rewardPoints,
            predictionAccuracy = accuracy,
            pointHistory
        });
    }
}