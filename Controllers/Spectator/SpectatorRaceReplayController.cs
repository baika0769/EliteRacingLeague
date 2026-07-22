using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Spectator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Controllers.Spectator;

[Authorize(Roles = UserRoles.Spectator)]
[ApiController]
[Route("api/spectator/races")]
public class SpectatorRaceReplayController : ControllerBase
{
    private readonly EliteRacingLeagueContext _context;

    private static readonly string[] RunnerColors =
    {
        "#ef4444",
        "#3b82f6",
        "#22c55e",
        "#f59e0b",
        "#a855f7",
        "#f97316",
        "#14b8a6",
        "#ec4899"
    };

    public SpectatorRaceReplayController(EliteRacingLeagueContext context)
    {
        _context = context;
    }

    [HttpGet("{raceId:int}/replay")]
    public async Task<IActionResult> GetRaceReplay(int raceId)
    {
        var race = await _context.Races
            .AsNoTracking()
            .Where(r =>
                r.RaceId == raceId &&
                r.Status != RaceStatuses.Cancelled &&
                r.Tournament.Status != TournamentStatuses.Cancelled)
            .Select(r => new
            {
                r.RaceId,
                r.TournamentId,
                r.RaceName,
                r.DistanceMeters,
                RaceStatus = r.Status,
                TournamentStatus = r.Tournament.Status,
                r.Tournament.TournamentName
            })
            .FirstOrDefaultAsync();

        if (race == null)
        {
            return NotFound(new { message = "Race not found or has been cancelled." });
        }

        // Replay only needs THIS race's own results to be approved/published. Requiring
        // the whole tournament to be marked Completed was wrong: tournament completion
        // is a separate, manually-triggered "finalize standings" admin action that can
        // happen long after (or independently of) any single race's results going out,
        // so a race with officially published results was getting stuck with no replay
        // until someone finalized every other race in the same tournament too.
        if (race.RaceStatus != RaceStatuses.Published)
        {
            return BadRequest(new
            {
                message = "Replay is only available after admin approves this race's results.",
                raceStatus = race.RaceStatus,
                tournamentStatus = race.TournamentStatus
            });
        }

        var approvedResults = await _context.RaceResults
            .AsNoTracking()
            .Where(r =>
                r.RaceId == raceId &&
                r.Status == RaceResultStatuses.Published)
            .Select(r => new
            {
                r.ResultId,
                r.RegistrationId,
                r.FinishPosition,
                r.FinishTimeSeconds,
                r.OutcomeStatus,
                r.Note,
                r.PublishedAt,
                r.Registration.HorseId,
                r.Registration.Horse.HorseName,
                HorseImageUrl = r.Registration.Horse.ImageUrl,
                r.Registration.OwnerId,
                OwnerName = r.Registration.Owner.Owner.FullName,
                r.Registration.JockeyId,
                JockeyName = r.Registration.Jockey == null
                    ? null
                    : r.Registration.Jockey.JockeyNavigation.FullName
            })
            .OrderBy(r => r.FinishPosition ?? int.MaxValue)
            .ThenBy(r => r.FinishTimeSeconds ?? decimal.MaxValue)
            .ThenBy(r => r.RegistrationId)
            .ToListAsync();

        if (approvedResults.Count == 0)
        {
            return NotFound(new { message = "No official approved results found for this race." });
        }

        var seed = unchecked((race.RaceId * 1000003) ^ race.TournamentId);

        var runners = approvedResults
            .Select((r, index) =>
            {
                var finishMs = r.FinishTimeSeconds.HasValue
                    ? Math.Max(1000, (int)Math.Round(r.FinishTimeSeconds.Value * 1000m, MidpointRounding.AwayFromZero))
                    : (int?)null;

                return new SpectatorRaceReplayRunnerResponse
                {
                    ResultId = r.ResultId,
                    RegistrationId = r.RegistrationId,
                    HorseId = r.HorseId,
                    HorseName = r.HorseName,
                    HorseImageUrl = r.HorseImageUrl,
                    OwnerId = r.OwnerId,
                    OwnerName = r.OwnerName,
                    JockeyId = r.JockeyId,
                    JockeyName = r.JockeyName,
                    Rank = r.FinishPosition,
                    FinishTimeSeconds = r.FinishTimeSeconds,
                    FinishTimeMs = finishMs,
                    OutcomeStatus = r.OutcomeStatus,
                    Note = r.Note,
                    Lane = index + 1,
                    Color = RunnerColors[index % RunnerColors.Length]
                };
            })
            .ToList();

        var totalDurationMs = runners
            .Where(r => r.FinishTimeMs.HasValue)
            .Select(r => r.FinishTimeMs!.Value)
            .DefaultIfEmpty(11000)
            .Max() + 1500;

        return Ok(new SpectatorRaceReplayResponse
        {
            RaceId = race.RaceId,
            TournamentId = race.TournamentId,
            RaceName = race.RaceName,
            TournamentName = race.TournamentName,
            DistanceMeters = race.DistanceMeters,
            RaceStatus = race.RaceStatus,
            TournamentStatus = race.TournamentStatus,
            Seed = seed,
            TotalDurationMs = totalDurationMs,
            OfficialAt = approvedResults.Max(r => r.PublishedAt),
            Runners = runners
        });
    }
}
