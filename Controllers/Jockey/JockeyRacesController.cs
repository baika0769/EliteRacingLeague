using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Jockey;
using Eliteracingleague.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Controllers.Jockey;

[Route("api/jockey/races")]
[ApiController]
[Authorize(Roles = UserRoles.Jockey)]
public class JockeyRacesController : ControllerBase
{
    private static readonly string[] AcceptedRaceStatuses =
    {
        RaceRegistrationStatuses.ReadyToRace,
        RaceRegistrationStatuses.Completed
    };

    private readonly EliteRacingLeagueContext _context;
    private readonly JockeyAccessService _jockeyAccess;

    public JockeyRacesController(
        EliteRacingLeagueContext context,
        JockeyAccessService jockeyAccess)
    {
        _context = context;
        _jockeyAccess = jockeyAccess;
    }

    [HttpGet("accepted")]
    public async Task<IActionResult> GetAcceptedRaces()
    {
        var access = await _jockeyAccess.ValidateActiveJockeyAsync(User);

        if (!access.Succeeded)
        {
            return AccessError(access);
        }

        var jockeyId = access.Jockey!.JockeyId;

        var items = await _context.RaceRegistrations
            .AsNoTracking()
            .Where(r =>
                r.JockeyId == jockeyId &&
                AcceptedRaceStatuses.Contains(r.Status) &&
                r.Race.Status != RaceStatuses.Cancelled &&
                r.Race.Tournament.Status != TournamentStatuses.Cancelled)
            .OrderBy(r => r.Race.RaceDate)
            .Select(r => new JockeyAcceptedRaceResponse
            {
                RaceRegistrationId = r.RegistrationId,
                RaceId = r.RaceId,
                RaceName = r.Race.RaceName,
                TournamentName = r.Race.Tournament.TournamentName,
                RaceDate = r.Race.RaceDate,
                Location = r.Race.Location,
                HorseId = r.HorseId,
                HorseName = r.Horse.HorseName,
                HorseImageUrl = r.Horse.ImageUrl,
                HorseHealthStatus = r.Horse.HealthStatus,
                HealthCertificateImageUrl = r.Horse.HealthCertificateImageUrl,
                OwnerName = r.Owner.Owner.FullName,
                Status = r.Status
            })
            .ToListAsync();

        return Ok(new { items });
    }

    [HttpGet("{raceId:int}")]
    public async Task<IActionResult> GetRaceDetail(int raceId)
    {
        var access = await _jockeyAccess.ValidateActiveJockeyAsync(User);

        if (!access.Succeeded)
        {
            return AccessError(access);
        }

        var jockeyId = access.Jockey!.JockeyId;

        var race = await _context.RaceRegistrations
            .AsNoTracking()
            .Where(r =>
                r.RaceId == raceId &&
                r.JockeyId == jockeyId &&
                AcceptedRaceStatuses.Contains(r.Status) &&
                r.Race.Status != RaceStatuses.Cancelled &&
                r.Race.Tournament.Status != TournamentStatuses.Cancelled)
            .Select(r => new JockeyRaceDetailResponse
            {
                RaceRegistrationId = r.RegistrationId,
                RaceId = r.RaceId,
                RaceName = r.Race.RaceName,
                RaceDate = r.Race.RaceDate,
                Location = r.Race.Location,
                HorseId = r.HorseId,
                HorseName = r.Horse.HorseName,
                HorseImageUrl = r.Horse.ImageUrl,
                HorseHealthStatus = r.Horse.HealthStatus,
                HealthCertificateImageUrl = r.Horse.HealthCertificateImageUrl,
                OwnerName = r.Owner.Owner.FullName,
                Status = r.Status
            })
            .FirstOrDefaultAsync();

        if (race == null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy race hoặc bạn không có quyền xem race này."
            });
        }

        return Ok(race);
    }

    private IActionResult AccessError(JockeyAccessResult access)
    {
        if (access.StatusCode == StatusCodes.Status401Unauthorized)
        {
            return Unauthorized(new { message = access.Message });
        }

        return StatusCode(access.StatusCode, new
        {
            message = access.Message,
            nextStep = access.NextStep
        });
    }
}
