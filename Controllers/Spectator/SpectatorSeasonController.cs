using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Controllers.Spectator;

[Authorize(Roles = UserRoles.Spectator)]
[ApiController]
[Route("api/spectator/season")]
public class SpectatorSeasonController : ControllerBase
{
    private readonly EliteRacingLeagueContext _context;
    private readonly SpectatorLeaderboardService _leaderboardService;

    public SpectatorSeasonController(
        EliteRacingLeagueContext context,
        SpectatorLeaderboardService leaderboardService)
    {
        _context = context;
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

    [HttpGet("current/reward-rules")]
    public async Task<IActionResult> GetCurrentSeasonRewardRules()
    {
        var season = await _context.Seasons
            .AsNoTracking()
            .Where(s => s.Status == SeasonStatuses.Active)
            .OrderByDescending(s => s.StartDate)
            .ThenByDescending(s => s.SeasonId)
            .Select(s => new
            {
                s.SeasonId,
                s.SeasonName,
                s.Status,
                s.StartDate,
                s.EndDate,
                s.PointsPerCorrectPrediction,
                RewardRules = s.SeasonRewardRules
                    .OrderBy(r => r.RankPosition)
                    .Select(r => new
                    {
                        r.SeasonRewardRuleId,
                        r.RankPosition,
                        r.RewardName,
                        r.RewardDescription,
                        r.BonusPoints
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync();

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