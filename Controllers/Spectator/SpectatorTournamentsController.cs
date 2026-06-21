using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Controllers.Spectator;

[Authorize]
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

    [HttpGet]
    public async Task<IActionResult> GetTournaments()
    {
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