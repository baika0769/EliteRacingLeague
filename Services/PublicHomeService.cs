using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Public;
using Eliteracingleague.API.Services.Public;
using Eliteracingleague.API.Services.SystemTime;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Services;

public class PublicHomeService
{
    private static readonly string[] ActiveTournamentStatuses =
    {
        TournamentStatuses.OpenRegistration,
        TournamentStatuses.ClosedRegistration,
        TournamentStatuses.Ongoing
    };

    private readonly EliteRacingLeagueContext _context;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly RacePublicStateService _racePublicStateService;

    public PublicHomeService(
        EliteRacingLeagueContext context,
        IDateTimeProvider dateTimeProvider,
        RacePublicStateService racePublicStateService)
    {
        _context = context;
        _dateTimeProvider = dateTimeProvider;
        _racePublicStateService = racePublicStateService;
    }

    public async Task<HomePageResponse> GetHomePageAsync(
        int upcomingLimit = 6,
        CancellationToken cancellationToken = default)
    {
        var localNow = _dateTimeProvider.GetLocalNow(_dateTimeProvider.TimeZoneId);

        var currentSeason = await _context.Seasons
            .AsNoTracking()
            .Where(item => item.Status == SeasonStatuses.Active)
            .OrderByDescending(item => item.StartDate)
            .ThenByDescending(item => item.SeasonId)
            .Select(item => new PublicSeasonResponse
            {
                SeasonId = item.SeasonId,
                SeasonName = item.SeasonName,
                StartDate = item.StartDate,
                EndDate = item.EndDate,
                Status = item.Status
            })
            .FirstOrDefaultAsync(cancellationToken);

        var upcomingTournaments = await GetUpcomingTournamentsAsync(
            upcomingLimit,
            cancellationToken);

        var latestResult = await GetLatestResultAsync(cancellationToken);
        var statistics = await GetStatisticsAsync(cancellationToken);

        return new HomePageResponse
        {
            ServerClock = new PublicServerClockResponse
            {
                UtcNow = _dateTimeProvider.UtcNow,
                LocalNow = localNow,
                TimeZoneId = _dateTimeProvider.TimeZoneId,
                IsOverridden = _dateTimeProvider.IsOverridden
            },
            CurrentSeason = currentSeason,
            FeaturedRace = BuildFeaturedRace(upcomingTournaments.FirstOrDefault()),
            UpcomingTournaments = upcomingTournaments,
            LatestResult = latestResult,
            Statistics = statistics
        };
    }

    public async Task<List<PublicTournamentResponse>> GetUpcomingTournamentsAsync(
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 200);
        var localNow = _dateTimeProvider.GetLocalNow(_dateTimeProvider.TimeZoneId);

        var candidateRaces = await _context.Races
            .AsNoTracking()
            .Where(race =>
                race.Tournament.Status != TournamentStatuses.Draft &&
                race.Tournament.Status != TournamentStatuses.Cancelled &&
                race.Tournament.Status != TournamentStatuses.Completed &&
                race.Tournament.Season.Status != SeasonStatuses.Cancelled &&
                race.Status != RaceStatuses.Cancelled &&
                race.RaceDate >= localNow)
            .OrderBy(race => race.RaceDate)
            .ThenByDescending(race => race.Tournament.PrizePool)
            .Select(race => new UpcomingRaceRow
            {
                RaceId = race.RaceId,
                RaceName = race.RaceName,
                RaceDate = race.RaceDate,
                PredictionDeadline = race.PredictionDeadline,
                DistanceMeters = race.DistanceMeters,
                RaceLocation = race.Location,
                RaceStatus = race.Status,
                RaceMaxHorses = race.MaxHorses,

                TournamentId = race.TournamentId,
                TournamentName = race.Tournament.TournamentName,
                Description = race.Tournament.Description,
                TournamentLocation = race.Tournament.Location,
                StartDate = race.Tournament.StartDate,
                EndDate = race.Tournament.EndDate,
                TournamentMaxHorses = race.Tournament.MaxHorses,
                PrizePool = race.Tournament.PrizePool,
                ImageUrl = race.Tournament.ImageUrl,
                TournamentStatus = race.Tournament.Status,

                SeasonId = race.Tournament.SeasonId,
                SeasonName = race.Tournament.Season.SeasonName,
                SeasonStatus = race.Tournament.Season.Status,

                ReservedHorseCount = race.RaceRegistrations.Count(registration =>
                    RaceRegistrationCountingRules.ReservedStatuses.Contains(registration.Status)),
                ConfirmedHorseCount = race.RaceRegistrations.Count(registration =>
                    RaceRegistrationCountingRules.ConfirmedStatuses.Contains(registration.Status)),
                ReadyHorseCount = race.RaceRegistrations.Count(registration =>
                    RaceRegistrationCountingRules.ReadyStatuses.Contains(registration.Status)),
                ReplayAvailable = race.Status == RaceStatuses.Published &&
                    race.RaceResults.Any(result => result.Status == RaceResultStatuses.Published)
            })
            .ToListAsync(cancellationToken);

        return candidateRaces
            .GroupBy(item => item.TournamentId)
            .Select(group => group
                .OrderBy(item => item.RaceDate)
                .ThenBy(item => item.RaceId)
                .First())
            .OrderBy(item => item.RaceDate)
            .ThenByDescending(item => item.PrizePool)
            .Take(safeLimit)
            .Select(item => BuildTournamentResponse(item, localNow))
            .ToList();
    }

    private async Task<PublicLatestResultResponse?> GetLatestResultAsync(
        CancellationToken cancellationToken)
    {
        var latestRace = await _context.Races
            .AsNoTracking()
            .Where(item =>
                item.Status == RaceStatuses.Published &&
                item.Tournament.Status != TournamentStatuses.Cancelled &&
                item.RaceResults.Any(result =>
                    result.Status == RaceResultStatuses.Published &&
                    result.OutcomeStatus == RaceOutcomeStatuses.Finished &&
                    result.FinishPosition.HasValue))
            .Select(item => new
            {
                item.RaceId,
                item.RaceName,
                item.TournamentId,
                item.Tournament.TournamentName,
                PublishedAt = item.RaceResults
                    .Where(result => result.Status == RaceResultStatuses.Published)
                    .Max(result => (DateTime?)result.PublishedAt)
            })
            .OrderByDescending(item => item.PublishedAt)
            .ThenByDescending(item => item.RaceId)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestRace == null)
        {
            return null;
        }

        var standings = await _context.RaceResults
            .AsNoTracking()
            .Where(item =>
                item.RaceId == latestRace.RaceId &&
                item.Status == RaceResultStatuses.Published &&
                item.OutcomeStatus == RaceOutcomeStatuses.Finished &&
                item.FinishPosition.HasValue &&
                !_context.RaceViolations.Any(violation =>
                    violation.RaceId == item.RaceId &&
                    violation.RegistrationId == item.RegistrationId &&
                    violation.Action == RaceViolationActions.Disqualified))
            .OrderBy(item => item.FinishPosition)
            .ThenBy(item => item.FinishTimeSeconds)
            .ThenBy(item => item.ResultId)
            .Take(3)
            .Select(item => new PublicStandingResponse
            {
                Position = item.FinishPosition!.Value,
                HorseId = item.Registration.HorseId,
                HorseName = item.Registration.Horse.HorseName,
                HorseImageUrl = item.Registration.Horse.ImageUrl,
                JockeyName = item.Registration.Jockey == null
                    ? null
                    : item.Registration.Jockey.JockeyNavigation.FullName,
                OwnerName = item.Registration.Horse.Owner.Owner.FullName,
                FinishTimeSeconds = item.FinishTimeSeconds
            })
            .ToListAsync(cancellationToken);

        return new PublicLatestResultResponse
        {
            TournamentId = latestRace.TournamentId,
            TournamentName = latestRace.TournamentName,
            RaceId = latestRace.RaceId,
            RaceName = latestRace.RaceName,
            PublishedAt = latestRace.PublishedAt,
            ReplayAvailable = true,
            Standings = standings
        };
    }

    private async Task<PublicStatisticsResponse> GetStatisticsAsync(
        CancellationToken cancellationToken)
    {
        var response = new PublicStatisticsResponse
        {
            ActiveSeasons = await _context.Seasons
                .AsNoTracking()
                .CountAsync(item => item.Status == SeasonStatuses.Active, cancellationToken),

            ActiveTournaments = await _context.Tournaments
                .AsNoTracking()
                .CountAsync(item => ActiveTournamentStatuses.Contains(item.Status), cancellationToken),

            ActiveHorses = await _context.Horses
                .AsNoTracking()
                .CountAsync(item => item.IsActive, cancellationToken),

            ActiveJockeys = await _context.Jockeys
                .AsNoTracking()
                .CountAsync(item =>
                    item.IsActive &&
                    item.JockeyNavigation.Status == UserStatuses.Active,
                    cancellationToken),

            PublishedRaces = await _context.Races
                .AsNoTracking()
                .CountAsync(item => item.Status == RaceStatuses.Published, cancellationToken),

            TotalPredictions = await _context.RacePredictions
                .AsNoTracking()
                .CountAsync(item => item.Status != RacePredictionStatuses.Cancelled, cancellationToken),

            TotalSpectators = await _context.Users
                .AsNoTracking()
                .CountAsync(item =>
                    item.Role == UserRoles.Spectator &&
                    item.Status == UserStatuses.Active,
                    cancellationToken)
        };

        return response;
    }

    private PublicTournamentResponse BuildTournamentResponse(
        UpcomingRaceRow item,
        DateTime localNow)
    {
        var maxHorses = item.RaceMaxHorses > 0
            ? item.RaceMaxHorses
            : Math.Max(0, item.TournamentMaxHorses);

        var state = _racePublicStateService.Build(
            item.SeasonStatus,
            item.TournamentStatus,
            item.RaceStatus,
            item.RaceDate,
            item.PredictionDeadline,
            item.StartDate,
            maxHorses,
            item.ReservedHorseCount,
            item.ConfirmedHorseCount,
            item.ReadyHorseCount,
            item.ReplayAvailable,
            localNow);

        return new PublicTournamentResponse
        {
            TournamentId = item.TournamentId,
            TournamentName = item.TournamentName,
            Description = item.Description,
            Location = item.TournamentLocation,
            StartDate = item.StartDate,
            EndDate = item.EndDate,
            RegistrationDeadline = item.StartDate,
            PrizePool = item.PrizePool,
            ImageUrl = item.ImageUrl,
            Status = item.TournamentStatus,
            SeasonId = item.SeasonId,
            SeasonName = item.SeasonName,
            RegisteredHorseCount = state.ReservedCount,
            MaxHorses = state.MaxHorses,
            ReservedHorseCount = state.ReservedCount,
            ConfirmedHorseCount = state.ConfirmedCount,
            ReadyHorseCount = state.ReadyCount,
            AvailableSlots = state.AvailableSlots,
            RegistrationState = state.RegistrationState,
            PredictionState = state.PredictionState,
            ReplayAvailable = state.ReplayAvailable,
            Race = new PublicRaceResponse
            {
                RaceId = item.RaceId,
                RaceName = item.RaceName,
                RaceDate = item.RaceDate,
                PredictionDeadline = item.PredictionDeadline,
                RegistrationDeadline = item.StartDate,
                DistanceMeters = item.DistanceMeters,
                Location = item.RaceLocation,
                Status = item.RaceStatus,
                MaxHorses = state.MaxHorses,
                ReservedHorseCount = state.ReservedCount,
                ConfirmedHorseCount = state.ConfirmedCount,
                ReadyHorseCount = state.ReadyCount,
                AvailableSlots = state.AvailableSlots,
                RegistrationState = state.RegistrationState,
                PredictionState = state.PredictionState,
                ReplayAvailable = state.ReplayAvailable
            }
        };
    }

    private static PublicFeaturedRaceResponse? BuildFeaturedRace(
        PublicTournamentResponse? tournament)
    {
        if (tournament?.Race == null)
        {
            return null;
        }

        return new PublicFeaturedRaceResponse
        {
            TournamentId = tournament.TournamentId,
            TournamentName = tournament.TournamentName,
            TournamentImageUrl = tournament.ImageUrl,
            PrizePool = tournament.PrizePool,
            SeasonId = tournament.SeasonId,
            SeasonName = tournament.SeasonName,
            RaceId = tournament.Race.RaceId,
            RaceName = tournament.Race.RaceName,
            RaceDate = tournament.Race.RaceDate,
            PredictionDeadline = tournament.Race.PredictionDeadline,
            RegistrationDeadline = tournament.RegistrationDeadline,
            Location = tournament.Race.Location ?? tournament.Location,
            DistanceMeters = tournament.Race.DistanceMeters,
            MaxHorses = tournament.MaxHorses,
            ReservedHorseCount = tournament.ReservedHorseCount,
            ConfirmedHorseCount = tournament.ConfirmedHorseCount,
            ReadyHorseCount = tournament.ReadyHorseCount,
            AvailableSlots = tournament.AvailableSlots,
            TournamentStatus = tournament.Status,
            RaceStatus = tournament.Race.Status,
            RegistrationState = tournament.RegistrationState,
            PredictionState = tournament.PredictionState,
            ReplayAvailable = tournament.ReplayAvailable
        };
    }

    private sealed class UpcomingRaceRow
    {
        public int RaceId { get; init; }
        public string RaceName { get; init; } = string.Empty;
        public DateTime RaceDate { get; init; }
        public DateTime? PredictionDeadline { get; init; }
        public int DistanceMeters { get; init; }
        public string? RaceLocation { get; init; }
        public string RaceStatus { get; init; } = string.Empty;
        public int RaceMaxHorses { get; init; }

        public int TournamentId { get; init; }
        public string TournamentName { get; init; } = string.Empty;
        public string? Description { get; init; }
        public string TournamentLocation { get; init; } = string.Empty;
        public DateOnly StartDate { get; init; }
        public DateOnly EndDate { get; init; }
        public int TournamentMaxHorses { get; init; }
        public decimal? PrizePool { get; init; }
        public string? ImageUrl { get; init; }
        public string TournamentStatus { get; init; } = string.Empty;

        public int SeasonId { get; init; }
        public string SeasonName { get; init; } = string.Empty;
        public string SeasonStatus { get; init; } = string.Empty;

        public int ReservedHorseCount { get; init; }
        public int ConfirmedHorseCount { get; init; }
        public int ReadyHorseCount { get; init; }
        public bool ReplayAvailable { get; init; }
    }
}
