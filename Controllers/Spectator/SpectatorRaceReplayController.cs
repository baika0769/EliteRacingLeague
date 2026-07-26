using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Services.Racing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eliteracingleague.API.Controllers.Spectator;

[Authorize(Roles = UserRoles.Spectator)]
[ApiController]
[Route("api/spectator/races")]
public class SpectatorRaceReplayController : ControllerBase
{
    private readonly IRaceReplayService _raceReplayService;

    public SpectatorRaceReplayController(IRaceReplayService raceReplayService)
    {
        _raceReplayService = raceReplayService;
    }

    [HttpGet("{raceId:int}/replay")]
    public async Task<IActionResult> GetRaceReplay(
        int raceId,
        CancellationToken cancellationToken)
    {
        var result = await _raceReplayService.GetReplayAsync(
            raceId,
            cancellationToken);

        return result.Status switch
        {
            RaceReplayLookupStatus.RaceNotFound =>
                NotFound(new { message = "Race not found or has been cancelled." }),

            RaceReplayLookupStatus.RaceNotPublished =>
                BadRequest(new
                {
                    message = "Replay is only available after admin approves this race's results.",
                    raceStatus = result.RaceStatus,
                    tournamentStatus = result.TournamentStatus
                }),

            RaceReplayLookupStatus.NoPublishedResults =>
                NotFound(new { message = "No official approved results found for this race." }),

            _ when result.Replay != null => Ok(result.Replay),

            _ => NotFound(new { message = "Replay is unavailable." })
        };
    }
}
