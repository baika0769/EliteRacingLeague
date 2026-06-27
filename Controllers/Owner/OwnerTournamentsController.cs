using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Owner;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Controllers.Owner;

[Route("api/owner/tournaments")]
[ApiController]
[Authorize(Roles = UserRoles.HorseOwner)]
public class OwnerTournamentsController : OwnerBaseController
{
    public OwnerTournamentsController(EliteRacingLeagueContext context) : base(context)
    {
    }

    [HttpGet("new")]
    public async Task<IActionResult> GetNewTournaments()
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

        var today = DateTime.UtcNow.Date;

        var data = await _context.Tournaments
            .AsNoTracking()
            .Where(t =>
                t.Race != null &&
                t.Race.RaceDate >= today &&
                t.Status == TournamentStatuses.OpenRegistration &&
                t.Race.RaceRegistrations.Count < t.Race.MaxHorses &&
                !t.Race.RaceRegistrations.Any(r => r.OwnerId == ownerId.Value))
            .OrderBy(t => t.Race!.RaceDate)
            .Select(t => new
            {
                t.TournamentId,
                t.TournamentName,
                t.ImageUrl,
                RaceId = t.Race!.RaceId,
                RaceStatus = t.Race.Status,
                RaceDate = t.Race.RaceDate,
                Location = t.Race.Location ?? t.Location
            })
            .ToListAsync();

        var response = data
            .Where(t => RaceStatuses.CanRegister(t.RaceStatus))
            .Take(5)
            .Select(t => new OwnerNewTournamentResponse
            {
                TournamentId = t.TournamentId,
                TournamentName = t.TournamentName,
                RaceId = t.RaceId,
                RaceDate = t.RaceDate.ToString("yyyy-MM-dd"),
                Location = t.Location
            });

        return Ok(response);
    }
}
