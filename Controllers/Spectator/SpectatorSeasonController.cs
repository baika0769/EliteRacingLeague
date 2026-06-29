using Eliteracingleague.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eliteracingleague.API.Controllers.Spectator;

[Authorize]
[ApiController]
[Route("api/spectator/season")]
public class SpectatorSeasonController : ControllerBase
{
    private readonly SpectatorLeaderboardService _leaderboardService;

    public SpectatorSeasonController(SpectatorLeaderboardService leaderboardService)
    {
        _leaderboardService = leaderboardService;
    }

    [HttpGet("current")]
    public async Task<IActionResult> GetCurrentSeason()
    {
        var season = await _leaderboardService.GetCurrentSeasonResponseAsync();

        if (season == null)
        {
            return NotFound(new
            {
                message = "No active season found."
            });
        }

        return Ok(season);
    }
}