using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Spectator;
using Eliteracingleague.API.Services.SystemTime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Controllers.Spectator;

[Authorize(Roles = UserRoles.Spectator)]
[ApiController]
[Route("api/spectator/tournaments")]
public class SpectatorTournamentsController : ControllerBase
{
    private readonly EliteRacingLeagueContext _context;
    private readonly IDateTimeProvider _dateTimeProvider;

    private static readonly string[] VisibleRegistrationStatuses =
    {
        RaceRegistrationStatuses.ReadyToRace
    };

    public SpectatorTournamentsController(
        EliteRacingLeagueContext context,
        IDateTimeProvider dateTimeProvider)
    {
        _context = context;
        _dateTimeProvider = dateTimeProvider;
    }

    private bool TryGetUserId(out int userId)
        => int.TryParse(
            User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            out userId);

    private static (bool CanPredict, string? Reason) GetPredictionAvailability(
        string seasonStatus,
        string tournamentStatus,
        string? raceStatus,
        DateTime? raceDate,
        DateTime? predictionDeadline,
        bool hasPredicted,
        DateTime localNow)
    {
        if (hasPredicted)
        {
            return (false, "You have already submitted a prediction for this tournament.");
        }

        if (seasonStatus != SeasonStatuses.Active)
        {
            return (false, $"Prediction is unavailable because the season is {seasonStatus}.");
        }

        if (tournamentStatus != TournamentStatuses.OpenRegistration &&
            tournamentStatus != TournamentStatuses.ClosedRegistration)
        {
            return (false, "Prediction is unavailable for the current tournament status.");
        }

        if (string.IsNullOrWhiteSpace(raceStatus))
        {
            return (false, "Race information is unavailable.");
        }

        if (RaceStatuses.IsClosedForPrediction(raceStatus))
        {
            return (false, "Prediction period has ended.");
        }

        if (!raceDate.HasValue)
        {
            return (false, "Race date is unavailable.");
        }

        if (localNow >= raceDate.Value)
        {
            return (false, "The race has already started or passed.");
        }

        if (!predictionDeadline.HasValue)
        {
            return (false, "Prediction deadline is unavailable.");
        }

        if (localNow >= predictionDeadline.Value)
        {
            return (false, "Prediction deadline has passed.");
        }

        return (true, null);
    }

    [HttpGet]
    public async Task<IActionResult> GetTournaments()
    {
        if (!TryGetUserId(out var spectatorId))
        {
            return Unauthorized(new
            {
                message = "Token không hợp lệ hoặc thiếu UserId."
            });
        }

        var localNow = _dateTimeProvider.GetLocalNow(_dateTimeProvider.TimeZoneId);

        var rawTournaments = await _context.Tournaments
            .AsNoTracking()
            .Where(t =>
                t.Status != TournamentStatuses.Draft &&
                t.Status != TournamentStatuses.Cancelled)
            .OrderByDescending(t => t.EndDate)
            .ThenByDescending(t => t.TournamentId)
            .Select(t => new
            {
                tournamentId = t.TournamentId,
                tournamentName = t.TournamentName,
                description = t.Description,
                location = t.Location,
                startDate = t.StartDate,
                endDate = t.EndDate,
                prizePool = t.PrizePool,
                imageUrl = t.ImageUrl,
                status = t.Status,
                seasonId = t.SeasonId,
                seasonName = t.Season.SeasonName,
                seasonStatus = t.Season.Status,
                hasPredicted = _context.RacePredictions.Any(p =>
                    p.SpectatorId == spectatorId &&
                    p.Status != RacePredictionStatuses.Cancelled &&
                    p.Race.TournamentId == t.TournamentId),
                myPrediction = _context.RacePredictions
                    .Where(p =>
                        p.SpectatorId == spectatorId &&
                        p.Status != RacePredictionStatuses.Cancelled &&
                        p.Race.TournamentId == t.TournamentId)
                    .OrderByDescending(p => p.PredictedAt)
                    .Select(p => new
                    {
                        predictedHorseId = p.PredictedRegistration.HorseId,
                        predictedHorseName = p.PredictedRegistration.Horse.HorseName,
                        predictionStatus = p.Status,
                        isCorrect = p.IsCorrect,
                        stakePoints = p.StakePoints,
                        payoutPoints = p.PointsAwarded,
                        pointsAwarded = p.PointsAwarded,
                        netPoints = p.Status == RacePredictionStatuses.Evaluated
                            ? p.PointsAwarded - p.StakePoints
                            : -p.StakePoints,
                        predictedAt = p.PredictedAt
                    })
                    .FirstOrDefault(),
                race = _context.Races
                    .Where(r =>
                        r.TournamentId == t.TournamentId &&
                        r.Status != RaceStatuses.Cancelled)
                    .OrderBy(r => r.RaceDate)
                    .Select(r => new
                    {
                        raceId = r.RaceId,
                        raceName = r.RaceName,
                        raceDate = r.RaceDate,
                        predictionDeadline = r.PredictionDeadline,
                        distanceMeters = r.DistanceMeters,
                        maxHorses = r.MaxHorses,
                        status = r.Status
                    })
                    .FirstOrDefault()
            })
            .ToListAsync();

        var tournaments = rawTournaments.Select(t =>
        {
            var availability = GetPredictionAvailability(
                t.seasonStatus,
                t.status,
                t.race?.status,
                t.race?.raceDate,
                t.race?.predictionDeadline,
                t.hasPredicted,
                localNow);

            return new
            {
                t.tournamentId,
                t.tournamentName,
                t.description,
                t.location,
                t.startDate,
                t.endDate,
                t.prizePool,
                t.imageUrl,
                t.status,
                t.seasonId,
                t.seasonName,
                t.seasonStatus,
                t.hasPredicted,
                t.myPrediction,
                canPredict = availability.CanPredict,
                predictionUnavailableReason = availability.Reason,
                serverLocalNow = localNow,
                t.race
            };
        }).ToList();

        return Ok(tournaments);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetTournamentDetail(int id)
    {
        if (!TryGetUserId(out var spectatorId))
        {
            return Unauthorized(new
            {
                message = "Token không hợp lệ hoặc thiếu UserId."
            });
        }

        var localNow = _dateTimeProvider.GetLocalNow(_dateTimeProvider.TimeZoneId);

        var tournament = await _context.Tournaments
            .AsNoTracking()
            .Where(t =>
                t.TournamentId == id &&
                t.Status != TournamentStatuses.Draft &&
                t.Status != TournamentStatuses.Cancelled)
            .Select(t => new
            {
                tournamentId = t.TournamentId,
                tournamentName = t.TournamentName,
                description = t.Description,
                location = t.Location,
                startDate = t.StartDate,
                endDate = t.EndDate,
                prizePool = t.PrizePool,
                imageUrl = t.ImageUrl,
                rules = t.Rules,
                status = t.Status,
                seasonId = t.SeasonId,
                seasonName = t.Season.SeasonName,
                seasonStatus = t.Season.Status,
                hasPredicted = _context.RacePredictions.Any(p =>
                    p.SpectatorId == spectatorId &&
                    p.Status != RacePredictionStatuses.Cancelled &&
                    p.Race.TournamentId == t.TournamentId),
                race = _context.Races
                    .Where(r =>
                        r.TournamentId == t.TournamentId &&
                        r.Status != RaceStatuses.Cancelled)
                    .OrderBy(r => r.RaceDate)
                    .Select(r => new
                    {
                        raceId = r.RaceId,
                        raceName = r.RaceName,
                        raceDate = r.RaceDate,
                        predictionDeadline = r.PredictionDeadline,
                        distanceMeters = r.DistanceMeters,
                        maxHorses = r.MaxHorses,
                        status = r.Status
                    })
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync();

        if (tournament == null)
        {
            return NotFound(new
            {
                message = "Tournament not found."
            });
        }

        var availability = GetPredictionAvailability(
            tournament.seasonStatus,
            tournament.status,
            tournament.race?.status,
            tournament.race?.raceDate,
            tournament.race?.predictionDeadline,
            tournament.hasPredicted,
            localNow);

        return Ok(new
        {
            tournament.tournamentId,
            tournament.tournamentName,
            tournament.description,
            tournament.location,
            tournament.startDate,
            tournament.endDate,
            tournament.prizePool,
            tournament.imageUrl,
            tournament.rules,
            tournament.status,
            tournament.seasonId,
            tournament.seasonName,
            tournament.seasonStatus,
            tournament.hasPredicted,
            canPredict = availability.CanPredict,
            predictionUnavailableReason = availability.Reason,
            serverLocalNow = localNow,
            tournament.race
        });
    }

    [HttpGet("{id:int}/horses")]
    public async Task<IActionResult> GetTournamentHorses(int id)
    {
        var tournamentExists = await _context.Tournaments
            .AsNoTracking()
            .AnyAsync(t =>
                t.TournamentId == id &&
                t.Status != TournamentStatuses.Draft &&
                t.Status != TournamentStatuses.Cancelled);

        if (!tournamentExists)
        {
            return NotFound(new
            {
                message = "Tournament not found."
            });
        }

        var race = await _context.Races
            .AsNoTracking()
            .Where(r =>
                r.TournamentId == id &&
                r.Status != RaceStatuses.Cancelled)
            .OrderBy(r => r.RaceDate)
            .Select(r => new
            {
                r.RaceId
            })
            .FirstOrDefaultAsync();

        if (race == null)
        {
            return NotFound(new
            {
                message = "Race not found."
            });
        }

        var horses = await _context.RaceRegistrations
            .AsNoTracking()
            .Where(r =>
                r.RaceId == race.RaceId &&
                VisibleRegistrationStatuses.Contains(r.Status))
            .OrderBy(r => r.RegistrationId)
            .Select(r => new TournamentHorseItem
            {
                RegistrationId = r.RegistrationId,
                HorseId = r.HorseId,
                HorseName = r.Horse.HorseName,
                ImageUrl = r.Horse.ImageUrl,
                BreedName = r.Horse.Breed.BreedName,
                Age = r.Horse.Age,
                HealthStatus = r.Horse.HealthStatus,
                RegistrationStatus = r.Status,
                OwnerName = r.Owner.Owner.FullName,
                JockeyName = r.Jockey == null
                    ? null
                    : r.Jockey.JockeyNavigation.FullName
            })
            .ToListAsync();

        return Ok(horses);
    }

    [HttpGet("/api/spectator/races/{raceId:int}/registrations")]
    public async Task<IActionResult> GetRaceRegistrations(int raceId)
    {
        var race = await _context.Races
            .AsNoTracking()
            .Where(r =>
                r.RaceId == raceId &&
                r.Status != RaceStatuses.Cancelled &&
                r.Tournament.Status != TournamentStatuses.Cancelled)
            .Select(r => new
            {
                r.RaceId
            })
            .FirstOrDefaultAsync();

        if (race == null)
        {
            return NotFound(new
            {
                message = "Race not found or has been cancelled."
            });
        }

        var registrations = await _context.RaceRegistrations
            .AsNoTracking()
            .Where(r =>
                r.RaceId == raceId &&
                VisibleRegistrationStatuses.Contains(r.Status) &&
                r.Race.Status != RaceStatuses.Cancelled &&
                r.Race.Tournament.Status != TournamentStatuses.Cancelled)
            .OrderBy(r => r.RegistrationId)
            .Select(r => new
            {
                registrationId = r.RegistrationId,
                horseId = r.HorseId,
                horseName = r.Horse.HorseName,
                horseAge = r.Horse.Age,
                horseWeightKg = r.Horse.WeightKg,
                horseHealthStatus = r.Horse.HealthStatus,
                ownerId = r.OwnerId,
                jockeyId = r.JockeyId,
                jockeyName = r.Jockey == null
                    ? null
                    : r.Jockey.JockeyNavigation.FullName,
                status = r.Status
            })
            .ToListAsync();

        return Ok(registrations);
    }
}