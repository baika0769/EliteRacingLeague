using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Owner;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Controllers.Owner;

[Route("api/owner/races")]
[ApiController]
[Authorize(Roles = UserRoles.HorseOwner)]
public class OwnerRacesController : OwnerBaseController
{
    public OwnerRacesController(EliteRacingLeagueContext context) : base(context)
    {
    }

    [HttpGet("{raceId:int}")]
    public async Task<IActionResult> GetRaceDetail(int raceId)
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

        var race = await _context.Races
            .AsNoTracking()
            .Where(r =>
                r.RaceId == raceId &&
                (
                    r.Status == "Open" ||
                    r.RaceRegistrations.Any(rr => rr.OwnerId == ownerId.Value)
                ))
            .Select(r => new
            {
                r.RaceId,
                r.RaceName,
                r.RaceDate,
                r.DistanceMeters,
                r.Location,
                r.Status,
                TournamentName = r.Tournament.TournamentName,
                TournamentLocation = r.Tournament.Location
            })
            .FirstOrDefaultAsync();

        if (race == null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy race hoặc bạn không có quyền xem race này."
            });
        }

        var response = new OwnerRaceDetailResponse
        {
            RaceId = race.RaceId,
            TournamentName = race.TournamentName,
            RaceName = race.RaceName,
            RaceDate = race.RaceDate.ToString("yyyy-MM-dd"),
            Location = race.Location ?? race.TournamentLocation,
            Distance = race.DistanceMeters,
            Status = race.Status
        };

        return Ok(response);
    }
}