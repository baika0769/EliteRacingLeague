using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Public;
using Eliteracingleague.API.Services.Public;
using Eliteracingleague.API.Services.SystemTime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Controllers.Public;

[ApiController]
[Route("api/public")]
[AllowAnonymous]
public class PublicDetailsController : ControllerBase
{
    private readonly EliteRacingLeagueContext _context;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly RacePublicStateService _racePublicStateService;

    public PublicDetailsController(
        EliteRacingLeagueContext context,
        IDateTimeProvider dateTimeProvider,
        RacePublicStateService racePublicStateService)
    {
        _context = context;
        _dateTimeProvider = dateTimeProvider;
        _racePublicStateService = racePublicStateService;
    }

    [HttpGet("tournaments/{id:int}")]
    public async Task<IActionResult> Tournament(
        int id,
        CancellationToken cancellationToken)
    {
        var tournament = await _context.Tournaments
            .AsNoTracking()
            .Where(item =>
                item.TournamentId == id &&
                item.Status != TournamentStatuses.Draft)
            .Select(item => new
            {
                item.TournamentId,
                item.TournamentName,
                item.Description,
                item.Location,
                item.Status,
                item.SeasonId,
                item.Season.SeasonName,
                SeasonStatus = item.Season.Status,
                item.StartDate,
                item.EndDate,
                item.PrizePool,
                item.ImageUrl
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (tournament == null)
        {
            return NotFound(new { message = "Tournament not found." });
        }

        var localNow = _dateTimeProvider.GetLocalNow(_dateTimeProvider.TimeZoneId);

        var raceRows = await _context.Races
            .AsNoTracking()
            .Where(item => item.TournamentId == id)
            .OrderBy(item => item.RaceDate)
            .ThenBy(item => item.RaceId)
            .Select(item => new RaceDetailRow
            {
                RaceId = item.RaceId,
                RaceName = item.RaceName,
                RaceDate = item.RaceDate,
                PredictionDeadline = item.PredictionDeadline,
                DistanceMeters = item.DistanceMeters,
                Location = item.Location,
                Status = item.Status,
                MaxHorses = item.MaxHorses,
                ReservedCount = item.RaceRegistrations.Count(registration =>
                    RaceRegistrationCountingRules.ReservedStatuses.Contains(registration.Status)),
                ConfirmedCount = item.RaceRegistrations.Count(registration =>
                    RaceRegistrationCountingRules.ConfirmedStatuses.Contains(registration.Status)),
                ReadyCount = item.RaceRegistrations.Count(registration =>
                    RaceRegistrationCountingRules.ReadyStatuses.Contains(registration.Status)),
                ReplayAvailable = item.Status == RaceStatuses.Published &&
                    item.RaceResults.Any(result => result.Status == RaceResultStatuses.Published)
            })
            .ToListAsync(cancellationToken);

        var races = raceRows.Select(row =>
        {
            var state = _racePublicStateService.Build(
                tournament.SeasonStatus,
                tournament.Status,
                row.Status,
                row.RaceDate,
                row.PredictionDeadline,
                tournament.StartDate,
                row.MaxHorses,
                row.ReservedCount,
                row.ConfirmedCount,
                row.ReadyCount,
                row.ReplayAvailable,
                localNow);

            return new PublicRaceSummaryResponse
            {
                RaceId = row.RaceId,
                RaceName = row.RaceName,
                RaceDate = row.RaceDate,
                PredictionDeadline = row.PredictionDeadline,
                RegistrationDeadline = tournament.StartDate,
                DistanceMeters = row.DistanceMeters,
                Location = row.Location,
                Status = row.Status,
                RegisteredCount = state.ReservedCount,
                MaxHorses = state.MaxHorses,
                ReservedCount = state.ReservedCount,
                ConfirmedCount = state.ConfirmedCount,
                ReadyCount = state.ReadyCount,
                AvailableSlots = state.AvailableSlots,
                RegistrationState = state.RegistrationState,
                PredictionState = state.PredictionState,
                ReplayAvailable = state.ReplayAvailable
            };
        }).ToList();

        var standings = await _context.TournamentStandings
            .AsNoTracking()
            .Where(item => item.TournamentId == id)
            .OrderBy(item => item.FinalRank)
            .ThenBy(item => item.HorseId)
            .Select(item => new PublicTournamentStandingResponse
            {
                FinalRank = item.FinalRank,
                HorseId = item.HorseId,
                HorseName = item.Horse.HorseName,
                HorseImageUrl = item.Horse.ImageUrl,
                OwnerName = item.Owner.Owner.FullName,
                JockeyName = item.Jockey == null
                    ? null
                    : item.Jockey.JockeyNavigation.FullName,
                TotalPoints = item.TotalPoints,
                Wins = item.Wins,
                CompletedRaces = item.CompletedRaces
            })
            .ToListAsync(cancellationToken);

        return Ok(new PublicTournamentDetailResponse
        {
            TournamentId = tournament.TournamentId,
            TournamentName = tournament.TournamentName,
            Description = tournament.Description,
            Location = tournament.Location,
            Status = tournament.Status,
            SeasonId = tournament.SeasonId,
            SeasonName = tournament.SeasonName,
            StartDate = tournament.StartDate,
            EndDate = tournament.EndDate,
            RegistrationDeadline = tournament.StartDate,
            PrizePool = tournament.PrizePool,
            ImageUrl = tournament.ImageUrl,
            Races = races,
            Standings = standings
        });
    }

    [HttpGet("races/{id:int}")]
    public async Task<IActionResult> Race(
        int id,
        CancellationToken cancellationToken)
    {
        var race = await _context.Races
            .AsNoTracking()
            .Where(item =>
                item.RaceId == id &&
                item.Tournament.Status != TournamentStatuses.Draft)
            .Select(item => new
            {
                item.RaceId,
                item.RaceName,
                item.RaceDate,
                item.PredictionDeadline,
                item.DistanceMeters,
                item.Location,
                RaceStatus = item.Status,
                item.MaxHorses,

                item.TournamentId,
                item.Tournament.TournamentName,
                TournamentStatus = item.Tournament.Status,
                RegistrationDeadline = item.Tournament.StartDate,
                item.Tournament.SeasonId,
                item.Tournament.Season.SeasonName,
                SeasonStatus = item.Tournament.Season.Status,

                ReservedCount = item.RaceRegistrations.Count(registration =>
                    RaceRegistrationCountingRules.ReservedStatuses.Contains(registration.Status)),
                ConfirmedCount = item.RaceRegistrations.Count(registration =>
                    RaceRegistrationCountingRules.ConfirmedStatuses.Contains(registration.Status)),
                ReadyCount = item.RaceRegistrations.Count(registration =>
                    RaceRegistrationCountingRules.ReadyStatuses.Contains(registration.Status)),
                ReplayAvailable = item.Status == RaceStatuses.Published &&
                    item.RaceResults.Any(result => result.Status == RaceResultStatuses.Published)
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (race == null)
        {
            return NotFound(new { message = "Race not found." });
        }

        var localNow = _dateTimeProvider.GetLocalNow(_dateTimeProvider.TimeZoneId);
        var state = _racePublicStateService.Build(
            race.SeasonStatus,
            race.TournamentStatus,
            race.RaceStatus,
            race.RaceDate,
            race.PredictionDeadline,
            race.RegistrationDeadline,
            race.MaxHorses,
            race.ReservedCount,
            race.ConfirmedCount,
            race.ReadyCount,
            race.ReplayAvailable,
            localNow);

        var participants = await _context.RaceRegistrations
            .AsNoTracking()
            .Where(item =>
                item.RaceId == id &&
                RaceRegistrationCountingRules.VisibleParticipantStatuses.Contains(item.Status))
            .OrderBy(item => item.Horse.HorseName)
            .ThenBy(item => item.RegistrationId)
            .Select(item => new PublicParticipantResponse
            {
                RegistrationId = item.RegistrationId,
                HorseId = item.HorseId,
                HorseName = item.Horse.HorseName,
                HorseImageUrl = item.Horse.ImageUrl,
                OwnerName = item.Owner.Owner.FullName,
                JockeyName = item.Jockey == null
                    ? null
                    : item.Jockey.JockeyNavigation.FullName,
                RegistrationStatus = item.Status
            })
            .ToListAsync(cancellationToken);

        var results = await _context.RaceResults
            .AsNoTracking()
            .Where(item =>
                item.RaceId == id &&
                item.Status == RaceResultStatuses.Published)
            .OrderBy(item => item.FinishPosition ?? int.MaxValue)
            .ThenBy(item => item.ResultId)
            .Select(item => new PublicRaceResultResponse
            {
                RegistrationId = item.RegistrationId,
                HorseId = item.Registration.HorseId,
                HorseName = item.Registration.Horse.HorseName,
                HorseImageUrl = item.Registration.Horse.ImageUrl,
                OwnerName = item.Registration.Owner.Owner.FullName,
                JockeyName = item.Registration.Jockey == null
                    ? null
                    : item.Registration.Jockey.JockeyNavigation.FullName,
                FinishPosition = item.FinishPosition,
                FinishTimeSeconds = item.FinishTimeSeconds,
                OutcomeStatus = item.OutcomeStatus
            })
            .ToListAsync(cancellationToken);

        return Ok(new PublicRaceDetailResponse
        {
            RaceId = race.RaceId,
            RaceName = race.RaceName,
            RaceDate = race.RaceDate,
            PredictionDeadline = race.PredictionDeadline,
            RegistrationDeadline = race.RegistrationDeadline,
            DistanceMeters = race.DistanceMeters,
            Location = race.Location,
            Status = race.RaceStatus,
            RegisteredCount = state.ReservedCount,
            MaxHorses = state.MaxHorses,
            ReservedCount = state.ReservedCount,
            ConfirmedCount = state.ConfirmedCount,
            ReadyCount = state.ReadyCount,
            AvailableSlots = state.AvailableSlots,
            RegistrationState = state.RegistrationState,
            PredictionState = state.PredictionState,
            ReplayAvailable = state.ReplayAvailable,
            TournamentId = race.TournamentId,
            TournamentName = race.TournamentName,
            SeasonId = race.SeasonId,
            SeasonName = race.SeasonName,
            Participants = participants,
            Results = results
        });
    }

    [HttpGet("horses/{id:int}")]
    public async Task<IActionResult> Horse(
        int id,
        CancellationToken cancellationToken)
    {
        var horse = await _context.Horses
            .AsNoTracking()
            .Where(item => item.HorseId == id && item.IsActive)
            .Select(item => new
            {
                item.HorseId,
                item.HorseName,
                item.ImageUrl,
                item.Age,
                item.WeightKg,
                item.HeightCm,
                item.HealthStatus,
                item.AchievementSummary,
                BreedName = item.Breed.BreedName,
                item.OwnerId,
                OwnerName = item.Owner.Owner.FullName,
                PublishedResults = item.RaceRegistrations.Count(registration =>
                    registration.RaceResult != null &&
                    registration.RaceResult.Status == RaceResultStatuses.Published),
                Wins = item.RaceRegistrations.Count(registration =>
                    registration.RaceResult != null &&
                    registration.RaceResult.Status == RaceResultStatuses.Published &&
                    registration.RaceResult.FinishPosition == 1)
            })
            .FirstOrDefaultAsync(cancellationToken);

        return horse == null
            ? NotFound(new { message = "Horse not found." })
            : Ok(horse);
    }

    [HttpGet("jockeys/{id:int}")]
    public async Task<IActionResult> Jockey(
        int id,
        CancellationToken cancellationToken)
    {
        var jockey = await _context.Jockeys
            .AsNoTracking()
            .Where(item => item.JockeyId == id)
            .Select(item => new
            {
                item.JockeyId,
                FullName = item.JockeyNavigation.FullName,
                item.YearsOfExperience,
                item.HealthStatus,
                item.ProfileImageUrl,
                IsActive = item.IsActive &&
                    item.JockeyNavigation.Status == UserStatuses.Active,
                PublishedRaces = item.RaceRegistrations.Count(registration =>
                    registration.RaceResult != null &&
                    registration.RaceResult.Status == RaceResultStatuses.Published),
                Wins = item.RaceRegistrations.Count(registration =>
                    registration.RaceResult != null &&
                    registration.RaceResult.Status == RaceResultStatuses.Published &&
                    registration.RaceResult.FinishPosition == 1)
            })
            .FirstOrDefaultAsync(cancellationToken);

        return jockey == null
            ? NotFound(new { message = "Jockey not found." })
            : Ok(jockey);
    }

    [HttpGet("owners/{id:int}")]
    public async Task<IActionResult> Owner(
        int id,
        CancellationToken cancellationToken)
    {
        var owner = await _context.HorseOwners
            .AsNoTracking()
            .Where(item => item.OwnerId == id)
            .Select(item => new
            {
                item.OwnerId,
                FullName = item.Owner.FullName,
                ActiveHorses = item.Horses.Count(horse => horse.IsActive),
                PublishedRaces = item.RaceRegistrations.Count(registration =>
                    registration.RaceResult != null &&
                    registration.RaceResult.Status == RaceResultStatuses.Published),
                Wins = item.RaceRegistrations.Count(registration =>
                    registration.RaceResult != null &&
                    registration.RaceResult.Status == RaceResultStatuses.Published &&
                    registration.RaceResult.FinishPosition == 1)
            })
            .FirstOrDefaultAsync(cancellationToken);

        return owner == null
            ? NotFound(new { message = "Owner not found." })
            : Ok(owner);
    }

    private sealed class RaceDetailRow
    {
        public int RaceId { get; init; }
        public string RaceName { get; init; } = string.Empty;
        public DateTime RaceDate { get; init; }
        public DateTime? PredictionDeadline { get; init; }
        public int DistanceMeters { get; init; }
        public string? Location { get; init; }
        public string Status { get; init; } = string.Empty;
        public int MaxHorses { get; init; }
        public int ReservedCount { get; init; }
        public int ConfirmedCount { get; init; }
        public int ReadyCount { get; init; }
        public bool ReplayAvailable { get; init; }
    }
}
