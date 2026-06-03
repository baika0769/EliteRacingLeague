using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Owner;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Eliteracingleague.API.Controllers.Owner;

[Route("api/owner")]
[ApiController]
[Authorize(Roles = UserRoles.HorseOwner)]
public class OwnerController : ControllerBase
{
    private readonly EliteRacingLeagueContext _context;

    private static readonly string[] ApprovedRegistrationStatuses =
    {
        RaceRegistrationStatuses.Approved,
        RaceRegistrationStatuses.JockeyInvited,
        RaceRegistrationStatuses.ReadyToRace
    };

    public OwnerController(EliteRacingLeagueContext context)
    {
        _context = context;
    }

    // GET: /api/owner/dashboard/overview
    [HttpGet("dashboard/overview")]
    public async Task<IActionResult> GetDashboardOverview()
    {
        var ownerId = GetCurrentUserId();

        if (ownerId == null)
        {
            return Unauthorized(new
            {
                message = "Token không hợp lệ hoặc thiếu UserId."
            });
        }

        var ownerProfileError = await ValidateOwnerProfileAsync(ownerId.Value);

        if (ownerProfileError != null)
        {
            return ownerProfileError;
        }

        var totalHorse = await _context.Horses
            .CountAsync(h => h.OwnerId == ownerId.Value && h.IsActive);

        var registrations = await _context.RaceRegistrations
            .CountAsync(r => r.OwnerId == ownerId.Value);

        var pendingInvitations = await _context.JockeyInvitations
            .CountAsync(i =>
                i.InvitedByOwnerId == ownerId.Value &&
                i.Status == InvitationStatuses.Pending);

        var approvedRaces = await _context.RaceRegistrations
            .CountAsync(r =>
                r.OwnerId == ownerId.Value &&
                ApprovedRegistrationStatuses.Contains(r.Status));

        var response = new OwnerDashboardOverviewResponse
        {
            TotalHorse = totalHorse,
            Registrations = registrations,
            PendingInvitations = pendingInvitations,
            ApprovedRaces = approvedRaces
        };

        return Ok(response);
    }

    // GET: /api/owner/dashboard/approved-registrations
    [HttpGet("dashboard/approved-registrations")]
    public async Task<IActionResult> GetApprovedRegistrations()
    {
        var ownerId = GetCurrentUserId();

        if (ownerId == null)
        {
            return Unauthorized(new
            {
                message = "Token không hợp lệ hoặc thiếu UserId."
            });
        }

        var ownerProfileError = await ValidateOwnerProfileAsync(ownerId.Value);

        if (ownerProfileError != null)
        {
            return ownerProfileError;
        }

        var data = await _context.RaceRegistrations
            .AsNoTracking()
            .Where(r =>
                r.OwnerId == ownerId.Value &&
                ApprovedRegistrationStatuses.Contains(r.Status))
            .OrderByDescending(r => r.Race.RaceDate)
            .Select(r => new
            {
                r.RegistrationId,
                r.RaceId,
                TournamentName = r.Race.Tournament.TournamentName,
                HorseName = r.Horse.HorseName,
                JockeyName = r.Jockey == null ? null : r.Jockey.JockeyNavigation.FullName,
                RaceDate = r.Race.RaceDate,
                r.Status
            })
            .ToListAsync();

        var response = data.Select(r => new OwnerApprovedRegistrationResponse
        {
            RegistrationId = r.RegistrationId,
            RaceId = r.RaceId,
            TournamentName = r.TournamentName,
            HorseName = r.HorseName,
            JockeyName = r.JockeyName,
            RaceDate = r.RaceDate.ToString("yyyy-MM-dd"),
            Status = r.Status
        });

        return Ok(response);
    }

    // GET: /api/owner/horses
    [HttpGet("horses")]
    public async Task<IActionResult> GetMyHorses()
    {
        var ownerId = GetCurrentUserId();

        if (ownerId == null)
        {
            return Unauthorized(new
            {
                message = "Token không hợp lệ hoặc thiếu UserId."
            });
        }

        var ownerProfileError = await ValidateOwnerProfileAsync(ownerId.Value);

        if (ownerProfileError != null)
        {
            return ownerProfileError;
        }

        var horses = await _context.Horses
            .AsNoTracking()
            .Where(h => h.OwnerId == ownerId.Value && h.IsActive)
            .OrderByDescending(h => h.CreatedAt)
            .Select(h => new OwnerHorseResponse
            {
                HorseId = h.HorseId,
                HorseName = h.HorseName,
                BreedName = h.Breed.BreedName,
                Age = h.Age,
                WeightKg = h.WeightKg,
                HealthStatus = h.HealthStatus,
                ImageUrl = null
            })
            .ToListAsync();

        return Ok(horses);
    }

    // GET: /api/owner/tournaments/new
    [HttpGet("tournaments/new")]
    public async Task<IActionResult> GetNewTournaments()
    {
        var ownerId = GetCurrentUserId();

        if (ownerId == null)
        {
            return Unauthorized(new
            {
                message = "Token không hợp lệ hoặc thiếu UserId."
            });
        }

        var ownerProfileError = await ValidateOwnerProfileAsync(ownerId.Value);

        if (ownerProfileError != null)
        {
            return ownerProfileError;
        }

        var today = DateTime.UtcNow.Date;

        var data = await _context.Tournaments
            .AsNoTracking()
            .Where(t => t.Race != null && t.Race.RaceDate >= today)
            .OrderBy(t => t.Race!.RaceDate)
            .Take(5)
            .Select(t => new
            {
                t.TournamentId,
                t.TournamentName,
                RaceId = t.Race!.RaceId,
                RaceDate = t.Race.RaceDate,
                Location = t.Race.Location ?? t.Location
            })
            .ToListAsync();

        var response = data.Select(t => new OwnerNewTournamentResponse
        {
            TournamentId = t.TournamentId,
            TournamentName = t.TournamentName,
            RaceId = t.RaceId,
            RaceDate = t.RaceDate.ToString("yyyy-MM-dd"),
            Location = t.Location,
            Surface = null
        });

        return Ok(response);
    }

    // GET: /api/owner/races/{raceId}
    [HttpGet("races/{raceId:int}")]
    public async Task<IActionResult> GetRaceDetail(int raceId)
    {
        var ownerId = GetCurrentUserId();

        if (ownerId == null)
        {
            return Unauthorized(new
            {
                message = "Token không hợp lệ hoặc thiếu UserId."
            });
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
            Surface = null,
            Status = race.Status
        };

        return Ok(response);
    }

    private int? GetCurrentUserId()
    {
        var userIdText = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!int.TryParse(userIdText, out var userId))
        {
            return null;
        }

        return userId;
    }

    private async Task<IActionResult?> ValidateOwnerProfileAsync(int ownerId)
    {
        var ownerExists = await _context.HorseOwners
            .AsNoTracking()
            .AnyAsync(o => o.OwnerId == ownerId && o.IsActive);

        if (!ownerExists)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Tài khoản HorseOwner chưa có profile hoặc đã bị khóa."
            });
        }

        return null;
    }
}