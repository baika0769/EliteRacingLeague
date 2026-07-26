using Eliteracingleague.API.DTOs.Public;
using Eliteracingleague.API.Services.Racing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eliteracingleague.API.Controllers.Public;

[AllowAnonymous]
[ApiController]
[Route("api/public/races")]
public class PublicRaceReplayController : ControllerBase
{
    private readonly IRaceReplayService _raceReplayService;

    public PublicRaceReplayController(IRaceReplayService raceReplayService)
    {
        _raceReplayService = raceReplayService;
    }

    [HttpGet("{raceId:int}/replay")]
    public async Task<IActionResult> GetReplay(
        int raceId,
        CancellationToken cancellationToken)
    {
        var result = await _raceReplayService.GetReplayAsync(
            raceId,
            cancellationToken);

        if (result.Status == RaceReplayLookupStatus.RaceNotFound)
        {
            return NotFound(new { message = "Race not found or has been cancelled." });
        }

        if (result.Status == RaceReplayLookupStatus.RaceNotPublished)
        {
            return BadRequest(new
            {
                message = "Public replay is only available after official results are published.",
                raceStatus = result.RaceStatus,
                tournamentStatus = result.TournamentStatus
            });
        }

        if (result.Status == RaceReplayLookupStatus.NoPublishedResults ||
            result.Replay == null)
        {
            return NotFound(new { message = "No official published results found for this race." });
        }

        var replay = result.Replay;

        return Ok(new PublicRaceReplayResponse
        {
            RaceId = replay.RaceId,
            TournamentId = replay.TournamentId,
            RaceName = replay.RaceName,
            TournamentName = replay.TournamentName,
            DistanceMeters = replay.DistanceMeters,
            RaceStatus = replay.RaceStatus,
            TournamentStatus = replay.TournamentStatus,
            Seed = replay.Seed,
            TotalDurationMs = replay.TotalDurationMs,
            OfficialAt = replay.OfficialAt,
            Runners = replay.Runners.Select(runner => new PublicRaceReplayRunnerResponse
            {
                RegistrationId = runner.RegistrationId,
                HorseId = runner.HorseId,
                HorseName = runner.HorseName,
                HorseImageUrl = runner.HorseImageUrl,
                OwnerName = runner.OwnerName,
                JockeyId = runner.JockeyId,
                JockeyName = runner.JockeyName,
                Rank = runner.Rank,
                FinishTimeSeconds = runner.FinishTimeSeconds,
                FinishTimeMs = runner.FinishTimeMs,
                OutcomeStatus = runner.OutcomeStatus,
                Lane = runner.Lane,
                Color = runner.Color
            }).ToList()
        });
    }
}
