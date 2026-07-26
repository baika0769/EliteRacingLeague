using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Spectator;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Services.Racing;

public enum RaceReplayLookupStatus
{
    Available,
    RaceNotFound,
    RaceNotPublished,
    NoPublishedResults
}

public sealed class RaceReplayLookupResult
{
    public RaceReplayLookupStatus Status { get; init; }
    public string? RaceStatus { get; init; }
    public string? TournamentStatus { get; init; }
    public SpectatorRaceReplayResponse? Replay { get; init; }
}

public interface IRaceReplayService
{
    Task<RaceReplayLookupResult> GetReplayAsync(
        int raceId,
        CancellationToken cancellationToken = default);
}

public sealed class RaceReplayService : IRaceReplayService
{
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

    private readonly EliteRacingLeagueContext _context;

    public RaceReplayService(EliteRacingLeagueContext context)
    {
        _context = context;
    }

    public async Task<RaceReplayLookupResult> GetReplayAsync(
        int raceId,
        CancellationToken cancellationToken = default)
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
            .FirstOrDefaultAsync(cancellationToken);

        if (race == null)
        {
            return new RaceReplayLookupResult
            {
                Status = RaceReplayLookupStatus.RaceNotFound
            };
        }

        if (race.RaceStatus != RaceStatuses.Published)
        {
            return new RaceReplayLookupResult
            {
                Status = RaceReplayLookupStatus.RaceNotPublished,
                RaceStatus = race.RaceStatus,
                TournamentStatus = race.TournamentStatus
            };
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
            .ToListAsync(cancellationToken);

        if (approvedResults.Count == 0)
        {
            return new RaceReplayLookupResult
            {
                Status = RaceReplayLookupStatus.NoPublishedResults,
                RaceStatus = race.RaceStatus,
                TournamentStatus = race.TournamentStatus
            };
        }

        var seed = unchecked((race.RaceId * 1000003) ^ race.TournamentId);

        var runners = approvedResults
            .Select((result, index) =>
            {
                var finishMs = result.FinishTimeSeconds.HasValue
                    ? Math.Max(
                        1000,
                        (int)Math.Round(
                            result.FinishTimeSeconds.Value * 1000m,
                            MidpointRounding.AwayFromZero))
                    : (int?)null;

                return new SpectatorRaceReplayRunnerResponse
                {
                    ResultId = result.ResultId,
                    RegistrationId = result.RegistrationId,
                    HorseId = result.HorseId,
                    HorseName = result.HorseName,
                    HorseImageUrl = result.HorseImageUrl,
                    OwnerId = result.OwnerId,
                    OwnerName = result.OwnerName,
                    JockeyId = result.JockeyId,
                    JockeyName = result.JockeyName,
                    Rank = result.FinishPosition,
                    FinishTimeSeconds = result.FinishTimeSeconds,
                    FinishTimeMs = finishMs,
                    OutcomeStatus = result.OutcomeStatus,
                    Note = result.Note,
                    Lane = index + 1,
                    Color = RunnerColors[index % RunnerColors.Length]
                };
            })
            .ToList();

        var totalDurationMs = runners
            .Where(runner => runner.FinishTimeMs.HasValue)
            .Select(runner => runner.FinishTimeMs!.Value)
            .DefaultIfEmpty(11000)
            .Max() + 1500;

        return new RaceReplayLookupResult
        {
            Status = RaceReplayLookupStatus.Available,
            RaceStatus = race.RaceStatus,
            TournamentStatus = race.TournamentStatus,
            Replay = new SpectatorRaceReplayResponse
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
                OfficialAt = approvedResults.Max(result => result.PublishedAt),
                Runners = runners
            }
        };
    }
}
