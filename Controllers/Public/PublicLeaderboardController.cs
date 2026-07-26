using Eliteracingleague.API.DTOs.Public;
using Eliteracingleague.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eliteracingleague.API.Controllers.Public;

[AllowAnonymous]
[ApiController]
[Route("api/public/leaderboards")]
public class PublicLeaderboardController : ControllerBase
{
    private readonly SpectatorLeaderboardService _spectatorLeaderboardService;

    public PublicLeaderboardController(
        SpectatorLeaderboardService spectatorLeaderboardService)
    {
        _spectatorLeaderboardService = spectatorLeaderboardService;
    }

    [HttpGet("spectators")]
    public async Task<IActionResult> GetSpectators(
        [FromQuery] int? seasonId = null,
        [FromQuery] int limit = 10)
    {
        var safeLimit = Math.Clamp(limit, 1, 100);

        var leaderboard = seasonId.HasValue
            ? await _spectatorLeaderboardService.GetPredictorLeaderboardAsync(
                safeLimit,
                seasonId.Value)
            : await _spectatorLeaderboardService.GetPredictorLeaderboardAsync(
                safeLimit);

        var items = leaderboard.Select(item =>
            new PublicSpectatorLeaderboardItemResponse
            {
                Rank = item.Rank,
                SpectatorId = item.SpectatorId,
                DisplayName = item.SpectatorName,
                RewardPoints = item.Points,
                CorrectPredictions = item.CorrectPredictions,
                TotalPredictions = item.TotalPredictions,
                AccuracyPercentage = item.Accuracy
            }).ToList();

        return Ok(new { items });
    }
}
