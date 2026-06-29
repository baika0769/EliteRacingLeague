using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eliteracingleague.API.Controllers.Spectator;

[Authorize(Roles = UserRoles.Spectator)]
[ApiController]
[Route("api/spectator/leaderboard")]
public class SpectatorLeaderboardController : ControllerBase
{
    private readonly SpectatorLeaderboardService _leaderboardService;

    public SpectatorLeaderboardController(SpectatorLeaderboardService leaderboardService)
    {
        _leaderboardService = leaderboardService;
    }

    [HttpGet("horses")]
    public async Task<IActionResult> GetHorseLeaderboard()
    {
        var leaderboard = await _leaderboardService.GetHorseLeaderboardAsync();
        return Ok(leaderboard);
    }

    [HttpGet("predictors")]
    public async Task<IActionResult> GetPredictorLeaderboard()
    {
        var leaderboard = await _leaderboardService.GetPredictorLeaderboardAsync();
        return Ok(leaderboard);
    }
}
