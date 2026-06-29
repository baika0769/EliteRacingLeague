using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
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

    private static readonly string[] VisibleRegistrationStatuses =
    {
        RaceRegistrationStatuses.Approved,
        RaceRegistrationStatuses.JockeyInvited,
        RaceRegistrationStatuses.ReadyToRace
    };

    public SpectatorTournamentsController(EliteRacingLeagueContext context)
    {
        _context = context;
    }

    private bool TryGetUserId(out int userId)
        => int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out userId);

    [HttpGet]
    public async Task<IActionResult> GetTournaments()
    {
        if (!TryGetUserId(out var spectatorId))
        {
            return Unauthorized(new { message = "Token không hợp lệ hoặc thiếu UserId." });
        }

        var tournaments = await _context.Tournaments
            .AsNoTracking()
            .Where(t =>
                t.Status != TournamentStatuses.Draft &&
                t.Status != TournamentStatuses.Cancelled)
            .OrderByDescending(t => t.StartDate)
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
                hasPredicted = _context.RacePredictions
                    .Any(p =>
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
                        isCorrect = p.IsCorrect,
                        pointsAwarded = p.PointsAwarded
                    })
                    .FirstOrDefault(),
                race = _context.Races
                    .Where(r =>
                        r.TournamentId == t.TournamentId &&
                        r.Status != RaceStatuses.Cancelled)
                    .Select(r => new
                    {
                        raceId = r.RaceId,
                        raceName = r.RaceName,
                        raceDate = r.RaceDate,
                        distanceMeters = r.DistanceMeters,
                        maxHorses = r.MaxHorses,
                        status = r.Status
                    })
                    .FirstOrDefault()
            })
            .ToListAsync();

        return Ok(tournaments);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetTournamentDetail(int id)
    {
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
                race = _context.Races
                    .Where(r =>
                        r.TournamentId == t.TournamentId &&
                        r.Status != RaceStatuses.Cancelled)
                    .Select(r => new
                    {
                        raceId = r.RaceId,
                        raceName = r.RaceName,
                        raceDate = r.RaceDate,
                        distanceMeters = r.DistanceMeters,
                        maxHorses = r.MaxHorses,
                        status = r.Status
                    })
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync();

        if (tournament == null)
        {
            return NotFound("Tournament not found.");
        }

        return Ok(tournament);
    }

    [HttpGet("{id}/horses")]
    public async Task<IActionResult> GetTournamentHorses(int id)
    {
        var tournamentExists = await _context.Tournaments
            .AsNoTracking()
            .AnyAsync(t => t.TournamentId == id && t.Status != TournamentStatuses.Cancelled);

        if (!tournamentExists)
        {
            return NotFound("Tournament not found.");
        }

        var race = await _context.Races
            .AsNoTracking()
            .Where(r => r.TournamentId == id && r.Status != RaceStatuses.Cancelled)
            .OrderBy(r => r.RaceDate)
            .Select(r => new { r.RaceId })
            .FirstOrDefaultAsync();

        if (race == null)
        {
            return NotFound("Race not found.");
        }

        var horses = await _context.RaceRegistrations
            .AsNoTracking()
            .Where(r =>
                r.RaceId == race.RaceId &&
                VisibleRegistrationStatuses.Contains(r.Status))
            .Select(r => new Eliteracingleague.API.DTOs.Spectator.TournamentHorseItem
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
                JockeyName = r.Jockey == null ? null : r.Jockey.JockeyNavigation.FullName
            })
            .ToListAsync();

        return Ok(horses);
    }

    [HttpGet("/api/spectator/races/{raceId}/registrations")]
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
            return NotFound("Race not found or has been cancelled.");
        }

        var registrations = await _context.RaceRegistrations
            .AsNoTracking()
            .Where(r =>
                r.RaceId == raceId &&
                VisibleRegistrationStatuses.Contains(r.Status) &&
                r.Race.Status != RaceStatuses.Cancelled &&
                r.Race.Tournament.Status != TournamentStatuses.Cancelled)
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
                jockeyName = r.Jockey == null ? null : r.Jockey.JockeyNavigation.FullName,
                status = r.Status
            })
            .ToListAsync();

        return Ok(registrations);
    }
}
