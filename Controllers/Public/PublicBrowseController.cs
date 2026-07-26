using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Public;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Controllers.Public;

[AllowAnonymous]
[ApiController]
[Route("api/public")]
public class PublicBrowseController : ControllerBase
{
    private const int DefaultPageSize = 12;
    private const int MaxPageSize = 50;

    private readonly EliteRacingLeagueContext _context;

    public PublicBrowseController(EliteRacingLeagueContext context)
    {
        _context = context;
    }

    [HttpGet("results")]
    public async Task<IActionResult> GetResults(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        [FromQuery] int? seasonId = null,
        CancellationToken cancellationToken = default)
    {
        (page, pageSize) = NormalizePaging(page, pageSize);

        var query = _context.Races
            .AsNoTracking()
            .Where(race =>
                race.Status == RaceStatuses.Published &&
                race.Tournament.Status != TournamentStatuses.Cancelled &&
                race.RaceResults.Any(result =>
                    result.Status == RaceResultStatuses.Published));

        if (seasonId.HasValue)
        {
            query = query.Where(race => race.Tournament.SeasonId == seasonId.Value);
        }

        var totalItems = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderByDescending(race => race.RaceResults
                .Where(result => result.Status == RaceResultStatuses.Published)
                .Max(result => (DateTime?)result.PublishedAt))
            .ThenByDescending(race => race.RaceDate)
            .ThenByDescending(race => race.RaceId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(race => new
            {
                race.RaceId,
                race.RaceName,
                race.TournamentId,
                race.Tournament.TournamentName,
                race.Tournament.SeasonId,
                race.Tournament.Season.SeasonName,
                race.RaceDate,
                PublishedAt = race.RaceResults
                    .Where(result => result.Status == RaceResultStatuses.Published)
                    .Max(result => (DateTime?)result.PublishedAt),
                race.DistanceMeters,
                race.Location,
                TournamentImageUrl = race.Tournament.ImageUrl
            })
            .ToListAsync(cancellationToken);

        var raceIds = rows.Select(row => row.RaceId).ToList();

        var standings = raceIds.Count == 0
            ? new List<ResultStandingRow>()
            : await _context.RaceResults
                .AsNoTracking()
                .Where(result =>
                    raceIds.Contains(result.RaceId) &&
                    result.Status == RaceResultStatuses.Published &&
                    result.OutcomeStatus == RaceOutcomeStatuses.Finished &&
                    result.FinishPosition.HasValue &&
                    !_context.RaceViolations.Any(violation =>
                        violation.RaceId == result.RaceId &&
                        violation.RegistrationId == result.RegistrationId &&
                        violation.Action == RaceViolationActions.Disqualified))
                .Select(result => new ResultStandingRow
                {
                    RaceId = result.RaceId,
                    Position = result.FinishPosition!.Value,
                    HorseId = result.Registration.HorseId,
                    HorseName = result.Registration.Horse.HorseName,
                    HorseImageUrl = result.Registration.Horse.ImageUrl,
                    JockeyName = result.Registration.Jockey == null
                        ? null
                        : result.Registration.Jockey.JockeyNavigation.FullName,
                    OwnerName = result.Registration.Owner.Owner.FullName,
                    FinishTimeSeconds = result.FinishTimeSeconds
                })
                .ToListAsync(cancellationToken);

        var topThreeByRace = standings
            .GroupBy(item => item.RaceId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(item => item.Position)
                    .ThenBy(item => item.FinishTimeSeconds)
                    .Take(3)
                    .Select(item => new PublicStandingResponse
                    {
                        Position = item.Position,
                        HorseId = item.HorseId,
                        HorseName = item.HorseName,
                        HorseImageUrl = item.HorseImageUrl,
                        JockeyName = item.JockeyName,
                        OwnerName = item.OwnerName,
                        FinishTimeSeconds = item.FinishTimeSeconds
                    })
                    .ToList());

        var items = rows.Select(row => new PublicResultListItemResponse
        {
            RaceId = row.RaceId,
            RaceName = row.RaceName,
            TournamentId = row.TournamentId,
            TournamentName = row.TournamentName,
            SeasonId = row.SeasonId,
            SeasonName = row.SeasonName,
            RaceDate = row.RaceDate,
            PublishedAt = row.PublishedAt,
            DistanceMeters = row.DistanceMeters,
            Location = row.Location,
            TournamentImageUrl = row.TournamentImageUrl,
            ReplayAvailable = true,
            TopThree = topThreeByRace.GetValueOrDefault(row.RaceId) ?? new List<PublicStandingResponse>()
        }).ToList();

        return Ok(new PublicPagedResponse<PublicResultListItemResponse>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = CalculateTotalPages(totalItems, pageSize)
        });
    }

    [HttpGet("replays")]
    public async Task<IActionResult> GetReplays(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        [FromQuery] int? seasonId = null,
        CancellationToken cancellationToken = default)
    {
        (page, pageSize) = NormalizePaging(page, pageSize);

        var query = _context.Races
            .AsNoTracking()
            .Where(race =>
                race.Status == RaceStatuses.Published &&
                race.Tournament.Status != TournamentStatuses.Cancelled &&
                race.RaceResults.Any(result =>
                    result.Status == RaceResultStatuses.Published));

        if (seasonId.HasValue)
        {
            query = query.Where(race => race.Tournament.SeasonId == seasonId.Value);
        }

        var totalItems = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(race => race.RaceResults
                .Where(result => result.Status == RaceResultStatuses.Published)
                .Max(result => (DateTime?)result.PublishedAt))
            .ThenByDescending(race => race.RaceDate)
            .ThenByDescending(race => race.RaceId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(race => new PublicReplayListItemResponse
            {
                RaceId = race.RaceId,
                RaceName = race.RaceName,
                TournamentId = race.TournamentId,
                TournamentName = race.Tournament.TournamentName,
                SeasonId = race.Tournament.SeasonId,
                SeasonName = race.Tournament.Season.SeasonName,
                RaceDate = race.RaceDate,
                PublishedAt = race.RaceResults
                    .Where(result => result.Status == RaceResultStatuses.Published)
                    .Max(result => (DateTime?)result.PublishedAt),
                DistanceMeters = race.DistanceMeters,
                Location = race.Location,
                ThumbnailUrl = race.Tournament.ImageUrl,
                RunnerCount = race.RaceResults.Count(result =>
                    result.Status == RaceResultStatuses.Published)
            })
            .ToListAsync(cancellationToken);

        return Ok(new PublicPagedResponse<PublicReplayListItemResponse>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = CalculateTotalPages(totalItems, pageSize)
        });
    }

    private static (int Page, int PageSize) NormalizePaging(int page, int pageSize)
    {
        var normalizedPage = Math.Max(1, page);
        var normalizedPageSize = pageSize <= 0
            ? DefaultPageSize
            : Math.Min(pageSize, MaxPageSize);

        return (normalizedPage, normalizedPageSize);
    }

    private static int CalculateTotalPages(int totalItems, int pageSize)
    {
        return totalItems == 0
            ? 0
            : (int)Math.Ceiling(totalItems / (double)pageSize);
    }

    private sealed class ResultStandingRow
    {
        public int RaceId { get; init; }
        public int Position { get; init; }
        public int HorseId { get; init; }
        public string HorseName { get; init; } = string.Empty;
        public string? HorseImageUrl { get; init; }
        public string? JockeyName { get; init; }
        public string OwnerName { get; init; } = string.Empty;
        public decimal? FinishTimeSeconds { get; init; }
    }
}
