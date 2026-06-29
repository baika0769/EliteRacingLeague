using Eliteracingleague.API.Services.Leaderboards;
using Microsoft.AspNetCore.Mvc;

namespace Eliteracingleague.API.Controllers;

[ApiController]
[Route("api")]
public class LeaderboardsController : ControllerBase
{
    private readonly ILeaderboardService _leaderboardService;

    public LeaderboardsController(ILeaderboardService leaderboardService)
    {
        _leaderboardService = leaderboardService;
    }

    [HttpGet("leaderboards/owners")]
    public async Task<IActionResult> GetOwnerLeaderboard(
        [FromQuery] int? seasonId,
        [FromQuery] int? year,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var items = await _leaderboardService.GetOwnerLeaderboardAsync(seasonId, year, limit, cancellationToken);
        return Ok(new { items });
    }

    [HttpGet("leaderboards/jockeys")]
    public async Task<IActionResult> GetJockeyLeaderboard(
        [FromQuery] int? seasonId,
        [FromQuery] int? year,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var items = await _leaderboardService.GetJockeyLeaderboardAsync(seasonId, year, limit, cancellationToken);
        return Ok(new { items });
    }
}
