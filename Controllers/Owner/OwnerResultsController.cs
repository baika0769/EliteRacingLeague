using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Owner.Rewards;
using Eliteracingleague.API.DTOs.Owner.Results;
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
                PrizeAmount = _context.PrizePayouts
                    .Where(p =>
                        p.PrizeAward.RaceId == r.RaceId &&
                        p.PrizeAward.RegistrationId == r.RegistrationId &&
                        p.RecipientUserId == ownerId.Value &&
                        p.RecipientType == PrizePayoutRecipientTypes.Owner)
                    .Select(p => (decimal?)p.Amount)
                    .FirstOrDefault(),
                RewardStatus = _context.PrizePayouts
                    .Where(p =>
                        p.PrizeAward.RaceId == r.RaceId &&
                        p.PrizeAward.RegistrationId == r.RegistrationId &&
                        p.RecipientUserId == ownerId.Value &&
                        p.RecipientType == PrizePayoutRecipientTypes.Owner)
                    .Select(p => p.Status)
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

    [HttpGet("{resultId:int}/horse-performance")]
    public async Task<IActionResult> GetHorsePerformance(int resultId)
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

        var performanceInfo = await _context.RaceResults
            .AsNoTracking()
            .Where(r =>
                r.ResultId == resultId &&
                r.Registration.OwnerId == ownerId.Value &&
                VisibleResultStatuses.Contains(r.Status))
            .Select(r => new
            {
                r.Registration.HorseId,
                HorseName = r.Registration.Horse.HorseName,
                BreedName = r.Registration.Horse.Breed.BreedName,
                r.Registration.Horse.ImageUrl,
                r.Registration.Horse.HealthCertificateImageUrl,
                r.Registration.Horse.Age,
                r.Registration.Horse.WeightKg,
                OwnerName = r.Registration.Owner.Owner.FullName,
                AssignedJockeyName = r.Registration.Jockey == null
                    ? null
                    : r.Registration.Jockey.JockeyNavigation.FullName,
                Award = r.Registration.Horse.AchievementSummary
            })
            .FirstOrDefaultAsync();

        if (performanceInfo == null)
        {
            return NotFound(new
            {
                message = "Result not found or you do not have permission to view it."
            });
        }

        var raceHistory = await _context.RaceResults
            .AsNoTracking()
            .Where(r =>
                r.Registration.HorseId == performanceInfo.HorseId &&
                r.Registration.OwnerId == ownerId.Value &&
                VisibleResultStatuses.Contains(r.Status))
            .OrderByDescending(r => r.Race.RaceDate)
            .ThenByDescending(r => r.ResultId)
            .Select(r => new OwnerHorseRaceHistoryResponse
            {
                RaceId = r.RaceId,
                ResultId = r.ResultId,
                TournamentName = r.Race.Tournament.TournamentName,
                RaceDate = r.Race.RaceDate,
                Track = r.Race.Location,
                DistanceMeters = r.Race.DistanceMeters,
                JockeyName = r.Registration.Jockey == null
                    ? null
                    : r.Registration.Jockey.JockeyNavigation.FullName,
                Position = r.FinishPosition,
                FinishTime = r.FinishTimeSeconds,
                Status = r.Status
            })
            .ToListAsync();

        var currentWinStreak = 0;

        foreach (var race in raceHistory)
        {
            if (race.Position != 1)
            {
                break;
            }

            currentWinStreak++;
        }

        var bestTime = raceHistory
            .Where(r => r.FinishTime.HasValue)
            .Select(r => r.FinishTime)
            .Min();

        var response = new OwnerHorsePerformanceResponse
        {
            Horse = new OwnerHorsePerformanceInfoResponse
            {
                HorseId = performanceInfo.HorseId,
                HorseName = performanceInfo.HorseName,
                BreedName = performanceInfo.BreedName,
                ImageUrl = performanceInfo.ImageUrl,
                HealthCertificateImageUrl = performanceInfo.HealthCertificateImageUrl,
                Age = performanceInfo.Age,
                WeightKg = performanceInfo.WeightKg,
                OwnerName = performanceInfo.OwnerName,
                AssignedJockeyName = performanceInfo.AssignedJockeyName
            },
            Achievements = new OwnerHorseAchievementResponse
            {
                ChampionTitles = raceHistory.Count(r => r.Position == 1),
                BestTime = bestTime,
                CurrentWinStreak = currentWinStreak,
                Award = performanceInfo.Award
            },
            RaceHistory = raceHistory
        };

        return Ok(response);
    }
}
