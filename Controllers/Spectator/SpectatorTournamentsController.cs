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

    public SpectatorTournamentsController(EliteRacingLeagueContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetTournaments()
    {
        var tournaments = await _context.Tournaments
            .Where(t => t.Status != TournamentStatuses.Draft)
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
                status = t.Status,
                race = _context.Races
                    .Where(r => r.TournamentId == t.TournamentId)
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
            .Where(t => t.TournamentId == id)
            .Select(t => new
            {
                tournamentId = t.TournamentId,
                tournamentName = t.TournamentName,
                description = t.Description,
                location = t.Location,
                startDate = t.StartDate,
                endDate = t.EndDate,
                prizePool = t.PrizePool,
                rules = t.Rules,
                status = t.Status,
                race = _context.Races
                    .Where(r => r.TournamentId == t.TournamentId)
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
            return NotFound("Tournament not found.");

        return Ok(tournament);
    }

    [HttpGet("../races/{raceId}/registrations")]
    public async Task<IActionResult> GetRaceRegistrations(int raceId)
    {
        var registrations = await _context.RaceRegistrations
            .Where(r => r.RaceId == raceId &&
                        r.Status == RaceRegistrationStatuses.Approved)
            .Select(r => new
            {
                registrationId = r.RegistrationId,
                horseId = r.HorseId,
                horseName = r.Horse.HorseName,
                horseAge = r.Horse.Age,
                horseWeightKg = r.Horse.WeightKg,
                horseHealthStatus = r.Horse.HealthStatus,
                ownerId = r.OwnerId,
                jockeyId = r.JockeyId
            })
            .ToListAsync();

        return Ok(registrations);
    }
}