using System.Security.Claims;
using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Controllers.Spectator;

[Authorize]
[ApiController]
[Route("api/spectator/dashboard")]
public class SpectatorDashboardController : ControllerBase
{
    private readonly EliteRacingLeagueContext _context;

    public SpectatorDashboardController(EliteRacingLeagueContext context)
    {
        _context = context;
    }

    private int GetUserId()
        => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    [HttpGet]
    public async Task<IActionResult> GetDashboard()
    {
        var userId = GetUserId();

        var upcomingTournaments = await _context.Tournaments
            .CountAsync(t => t.Status != TournamentStatuses.Cancelled);

        var predictionsSubmitted = await _context.RacePredictions
            .CountAsync(p => p.SpectatorId == userId);

        var rewardPoints = await _context.RacePredictions
            .Where(p => p.SpectatorId == userId)
            .SumAsync(p => p.PointsAwarded);

        var featuredTournament = await _context.Tournaments
            .Where(t => t.Status != TournamentStatuses.Cancelled)
            .OrderByDescending(t => t.PrizePool)
            .Select(t => new
            {
                tournamentId = t.TournamentId,
                tournamentName = t.TournamentName,
                location = t.Location,
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
            .FirstOrDefaultAsync();

        return Ok(new
        {
            upcomingTournaments,
            predictionsSubmitted,
            rewardPoints,
            featuredTournament
        });
    }
}