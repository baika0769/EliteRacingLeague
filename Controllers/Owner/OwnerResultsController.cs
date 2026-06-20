using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Owner.Rewards;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Controllers.Owner;

[Route("api/owner/results")]
[ApiController]
[Authorize(Roles = UserRoles.HorseOwner)]
public class OwnerResultsController : OwnerBaseController
{
    private static readonly string[] VisibleResultStatuses =
    {
        RaceResultStatuses.AdminApproved,
        RaceResultStatuses.Published
    };

    public OwnerResultsController(EliteRacingLeagueContext context) : base(context)
    {
    }

    [HttpGet]
    public async Task<IActionResult> GetResults(
        [FromQuery] int? season,
        [FromQuery] int? tournamentId,
        [FromQuery] int limit = 10)
    {
        var ownerId = GetCurrentUserId();

        if (ownerId == null)
        {
            return InvalidToken();
        }

        var ownerProfileError = await ValidateOwnerProfileAsync(ownerId.Value);

        if (ownerProfileError != null)
        {
            return ownerProfileError;
        }

        limit = Math.Clamp(limit, 1, 50);

        var query = _context.RaceResults
            .AsNoTracking()
            .Where(r =>
                r.Registration.OwnerId == ownerId.Value &&
                VisibleResultStatuses.Contains(r.Status));

        if (season.HasValue)
        {
            query = query.Where(r => r.Race.RaceDate.Year == season.Value);
        }

        if (tournamentId.HasValue)
        {
            query = query.Where(r => r.Race.TournamentId == tournamentId.Value);
        }

        var results = await query
            .OrderByDescending(r => r.Race.RaceDate)
            .ThenBy(r => r.FinishPosition)
            .Take(limit)
            .Select(r => new OwnerHorseResultResponse
            {
                ResultId = r.ResultId,
                RaceId = r.RaceId,
                RegistrationId = r.RegistrationId,
                RankPosition = r.FinishPosition,
                HorseName = r.Registration.Horse.HorseName,
                HorseBreed = r.Registration.Horse.Breed.BreedName,
                TournamentName = r.Race.Tournament.TournamentName,
                JockeyName = r.Registration.Jockey == null
                    ? null
                    : r.Registration.Jockey.JockeyNavigation.FullName,
                FinishTime = r.FinishTimeSeconds,
                Status = r.Status
            })
            .ToListAsync();

        return Ok(results);
    }

    [HttpGet("{resultId:int}")]
    public async Task<IActionResult> GetResultDetail(int resultId)
    {
        var ownerId = GetCurrentUserId();

        if (ownerId == null)
        {
            return InvalidToken();
        }

        var ownerProfileError = await ValidateOwnerProfileAsync(ownerId.Value);

        if (ownerProfileError != null)
        {
            return ownerProfileError;
        }

        var result = await _context.RaceResults
            .AsNoTracking()
            .Where(r =>
                r.ResultId == resultId &&
                r.Registration.OwnerId == ownerId.Value &&
                VisibleResultStatuses.Contains(r.Status))
            .Select(r => new OwnerHorseResultDetailResponse
            {
                ResultId = r.ResultId,
                RaceId = r.RaceId,
                RegistrationId = r.RegistrationId,
                TournamentName = r.Race.Tournament.TournamentName,
                RaceName = r.Race.RaceName,
                RaceDate = r.Race.RaceDate,
                HorseName = r.Registration.Horse.HorseName,
                HorseBreed = r.Registration.Horse.Breed.BreedName,
                JockeyName = r.Registration.Jockey == null
                    ? null
                    : r.Registration.Jockey.JockeyNavigation.FullName,
                RankPosition = r.FinishPosition,
                FinishTime = r.FinishTimeSeconds,
                Score = r.Score,
                ResultStatus = r.Status,
                PrizeAmount = _context.PrizeAwards
                    .Where(a =>
                        a.RaceId == r.RaceId &&
                        a.RegistrationId == r.RegistrationId)
                    .Select(a => (decimal?)a.PrizeAmount)
                    .FirstOrDefault(),
                RewardStatus = _context.PrizeAwards
                    .Where(a =>
                        a.RaceId == r.RaceId &&
                        a.RegistrationId == r.RegistrationId)
                    .Select(a => a.Status)
                    .FirstOrDefault(),
                PublishedAt = r.PublishedAt
            })
            .FirstOrDefaultAsync();

        if (result == null)
        {
            return NotFound(new
            {
                message = "Result not found or you do not have permission to view it."
            });
        }

        return Ok(result);
    }
}
