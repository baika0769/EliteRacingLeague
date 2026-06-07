using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Owner;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Eliteracingleague.API.Models;

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
                .Where(r =>
                r.OwnerId == ownerId.Value &&
                ApprovedRegistrationStatuses.Contains(r.Status))
                .Select(r => r.RaceId)
                .Distinct()
                .CountAsync();

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

    // POST: /api/owner/horses
    [HttpPost("horses")]
    public async Task<IActionResult> CreateHorse(CreateOwnerHorseRequest request)
    {
        var ownerId = GetCurrentUserId();

        if (ownerId == null)
        {
            return Unauthorized(new
            {
                message = "Token không hợp lệ hoặc thiếu UserId."
            });
        }

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.UserId == ownerId.Value);

        var owner = await _context.HorseOwners
            .FirstOrDefaultAsync(o => o.OwnerId == ownerId.Value);

        if (user == null || owner == null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy hồ sơ chủ ngựa."
            });
        }

        if (!user.EmailVerified)
        {
            return BadRequest(new
            {
                message = "Email chưa được xác thực.",
                nextStep = AuthNextSteps.VerifyEmail
            });
        }

        if (user.Status == UserStatuses.Inactive)
        {
            return BadRequest(new
            {
                message = "Tài khoản đang bị vô hiệu hóa.",
                status = user.Status,
                nextStep = AuthNextSteps.ContactSupport
            });
        }

        if (user.Status == UserStatuses.Banned)
        {
            return BadRequest(new
            {
                message = "Tài khoản đã bị khóa.",
                status = user.Status,
                nextStep = AuthNextSteps.AccountBlocked
            });
        }

        if (user.Status != UserStatuses.Pending && user.Status != UserStatuses.Active)
        {
            return BadRequest(new
            {
                message = "Trạng thái tài khoản không hợp lệ.",
                status = user.Status,
                nextStep = AuthNextSteps.Unknown
            });
        }

        var breedExists = await _context.HorseBreeds
            .AnyAsync(b => b.BreedId == request.BreedId);

        if (!breedExists)
        {
            return BadRequest(new
            {
                message = "Giống ngựa không hợp lệ."
            });
        }

        if (string.IsNullOrWhiteSpace(request.HorseName))
        {
            return BadRequest(new
            {
                message = "Tên ngựa không được để trống."
            });
        }

        if (!HorseHealthStatuses.IsValid(request.HealthStatus))
        {
            return BadRequest(new
            {
                message = "Tình trạng sức khỏe không hợp lệ.",
                allowedValues = HorseHealthStatuses.All
            });
        }

        if (request.Age <= 0)
        {
            return BadRequest(new
            {
                message = "Tuổi ngựa phải lớn hơn 0."
            });
        }

        if (request.WeightKg <= 0)
        {
            return BadRequest(new
            {
                message = "Cân nặng ngựa phải lớn hơn 0."
            });
        }

        if (string.IsNullOrWhiteSpace(request.HealthStatus))
        {
            return BadRequest(new
            {
                message = "Tình trạng sức khỏe không được để trống."
            });
        }

        var horse = new Horse
        {
            OwnerId = ownerId.Value,
            BreedId = request.BreedId,
            HorseName = request.HorseName.Trim(),
            Age = request.Age,
            HeightCm = request.HeightCm,
            WeightKg = request.WeightKg,
            HealthStatus = request.HealthStatus.Trim(),
            AchievementSummary = request.AchievementSummary,
            ImageUrl = request.ImageUrl,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Horses.Add(horse);

        var wasPending = user.Status == UserStatuses.Pending;
        if (wasPending)
        {
            user.Status = UserStatuses.Active;
            user.UpdatedAt = DateTime.UtcNow;
            owner.IsActive = true;
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = wasPending
                ? "Thêm ngựa thành công. Tài khoản HorseOwner đã được kích hoạt."
                : "Thêm ngựa thành công.",
            horseId = horse.HorseId,
            status = user.Status,
            ownerIsActive = owner.IsActive,
            nextStep = AuthNextSteps.GoToDashboard
        });
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
        var data = await _context.HorseOwners
            .AsNoTracking()
            .Where(o => o.OwnerId == ownerId)
            .Select(o => new
            {
                o.IsActive,
                UserStatus = o.Owner.Status,
                o.Owner.EmailVerified
            })
            .FirstOrDefaultAsync();

        if (data == null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Không tìm thấy hồ sơ HorseOwner.",
                nextStep = AuthNextSteps.Unknown
            });
        }

        if (!data.EmailVerified)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Email chưa được xác thực.",
                nextStep = AuthNextSteps.VerifyEmail
            });
        }

        if (data.UserStatus == UserStatuses.Pending)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Vui lòng thêm ngựa để kích hoạt tài khoản.",
                nextStep = AuthNextSteps.AddHorse
            });
        }

        if (data.UserStatus == UserStatuses.Inactive)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Tài khoản đang bị vô hiệu hóa.",
                status = data.UserStatus,
                nextStep = AuthNextSteps.ContactSupport
            });
        }

        if (data.UserStatus == UserStatuses.Banned)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Tài khoản đã bị khóa.",
                status = data.UserStatus,
                nextStep = AuthNextSteps.AccountBlocked
            });
        }

        if (data.UserStatus != UserStatuses.Active || !data.IsActive)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Tài khoản HorseOwner chưa được kích hoạt.",
                status = data.UserStatus,
                nextStep = AuthNextSteps.Unknown
            });
        }

        return null;
    }


    [HttpGet("horses/{horseId:int}")]
    public async Task<IActionResult> GetHorseDetail(int horseId)
    {
        var ownerId = GetCurrentUserId();

        if (ownerId == null)
        {
            return Unauthorized(new
            {
                message = "Token không hợp lệ hoặc thiếu UserId."
            });
        }

        var ownerError = await ValidateOwnerCanManageHorsesAsync(ownerId.Value);

        if (ownerError != null)
        {
            return ownerError;
        }

        var horse = await _context.Horses
            .AsNoTracking()
            .Where(h => h.HorseId == horseId && h.OwnerId == ownerId.Value)
            .Select(h => new OwnerHorseResponse
            {
                HorseId = h.HorseId,
                HorseName = h.HorseName,
                BreedId = h.BreedId,
                BreedName = h.Breed.BreedName,
                Age = h.Age,
                HeightCm = h.HeightCm,
                WeightKg = h.WeightKg,
                HealthStatus = h.HealthStatus,
                ImageUrl = h.ImageUrl,
                IsActive = h.IsActive,
                Status = h.IsActive ? "Active" : "Inactive",
                InRaceCount = h.RaceRegistrations.Count
            })
            .FirstOrDefaultAsync();

        if (horse == null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy ngựa hoặc bạn không có quyền xem ngựa này."
            });
        }

        return Ok(horse);
    }

    [HttpGet("horse-breeds")]
    public async Task<IActionResult> GetHorseBreeds()
    {
        var ownerId = GetCurrentUserId();

        if (ownerId == null)
        {
            return Unauthorized(new
            {
                message = "Token không hợp lệ hoặc thiếu UserId."
            });
        }

        var ownerError = await ValidateOwnerCanManageHorsesAsync(ownerId.Value);

        if (ownerError != null)
        {
            return ownerError;
        }

        var breeds = await _context.HorseBreeds
            .AsNoTracking()
            .Where(b => b.IsActive)
            .OrderBy(b => b.BreedName)
            .Select(b => new HorseBreedOptionResponse
            {
                BreedId = b.BreedId,
                BreedName = b.BreedName
            })
            .ToListAsync();

        return Ok(breeds);
    }

    [HttpGet("horses")]
    public async Task<IActionResult> GetHorses(
    [FromQuery] string? search,
    [FromQuery] int? breedId,
    [FromQuery] string? healthStatus,
    [FromQuery] string? status,
    [FromQuery] string? sortBy = "name",
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 5)
    {
        var ownerId = GetCurrentUserId();

        if (ownerId == null)
        {
            return Unauthorized(new
            {
                message = "Token không hợp lệ hoặc thiếu UserId."
            });
        }

        var ownerError = await ValidateOwnerCanManageHorsesAsync(ownerId.Value);

        if (ownerError != null)
        {
            return ownerError;
        }

        if (page <= 0) page = 1;
        if (pageSize <= 0) pageSize = 5;
        if (pageSize > 50) pageSize = 50;

        var query = _context.Horses
            .AsNoTracking()
            .Where(h => h.OwnerId == ownerId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim().ToLower();

            query = query.Where(h =>
                h.HorseName.ToLower().Contains(keyword));
        }

        if (breedId.HasValue)
        {
            query = query.Where(h => h.BreedId == breedId.Value);
        }

        if (!string.IsNullOrWhiteSpace(healthStatus))
        {
            query = query.Where(h => h.HealthStatus == healthStatus);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (status == "Active")
            {
                query = query.Where(h => h.IsActive);
            }
            else if (status == "Inactive")
            {
                query = query.Where(h => !h.IsActive);
            }
        }

        query = sortBy switch
        {
            "age" => query.OrderBy(h => h.Age),
            "weight" => query.OrderBy(h => h.WeightKg),
            "health" => query.OrderBy(h => h.HealthStatus),
            _ => query.OrderBy(h => h.HorseName)
        };

        var totalItems = await query.CountAsync();

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(h => new OwnerHorseResponse
            {
                HorseId = h.HorseId,
                HorseName = h.HorseName,
                BreedId = h.BreedId,
                BreedName = h.Breed.BreedName,
                Age = h.Age,
                HeightCm = h.HeightCm,
                WeightKg = h.WeightKg,
                HealthStatus = h.HealthStatus,
                ImageUrl = h.ImageUrl,
                IsActive = h.IsActive,
                Status = h.IsActive ? "Active" : "Inactive",
                InRaceCount = h.RaceRegistrations.Count
            })
            .ToListAsync();

        var response = new OwnerHorseListResponse
        {
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize),
            Items = items
        };

        return Ok(response);
    }

    [HttpPatch("horses/{horseId:int}/status")]
    public async Task<IActionResult> UpdateHorseStatus(int horseId, UpdateHorseStatusRequest request)
    {
        var ownerId = GetCurrentUserId();

        if (ownerId == null)
        {
            return Unauthorized(new
            {
                message = "Token không hợp lệ hoặc thiếu UserId."
            });
        }

        var ownerError = await ValidateOwnerCanManageHorsesAsync(ownerId.Value);

        if (ownerError != null)
        {
            return ownerError;
        }

        var horse = await _context.Horses
            .FirstOrDefaultAsync(h => h.HorseId == horseId && h.OwnerId == ownerId.Value);

        if (horse == null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy ngựa hoặc bạn không có quyền cập nhật ngựa này."
            });
        }

        horse.IsActive = request.IsActive;
        horse.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = request.IsActive
                ? "Ngựa đã được chuyển sang Active."
                : "Ngựa đã được chuyển sang Inactive.",
            horseId = horse.HorseId,
            isActive = horse.IsActive,
            status = horse.IsActive ? "Active" : "Inactive"
        });
    }


    [HttpPut("horses/{horseId:int}")]
    public async Task<IActionResult> UpdateHorse(int horseId, UpdateOwnerHorseRequest request)
    {
        var ownerId = GetCurrentUserId();

        if (ownerId == null)
        {
            return Unauthorized(new
            {
                message = "Token không hợp lệ hoặc thiếu UserId."
            });
        }

        var ownerError = await ValidateOwnerCanManageHorsesAsync(ownerId.Value);

        if (ownerError != null)
        {
            return ownerError;
        }

        var horse = await _context.Horses
            .FirstOrDefaultAsync(h => h.HorseId == horseId && h.OwnerId == ownerId.Value);

        if (horse == null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy ngựa hoặc bạn không có quyền sửa ngựa này."
            });
        }

        var breedExists = await _context.HorseBreeds
            .AnyAsync(b => b.BreedId == request.BreedId && b.IsActive);

        if (!breedExists)
        {
            return BadRequest(new
            {
                message = "Giống ngựa không hợp lệ."
            });
        }

        if (string.IsNullOrWhiteSpace(request.HorseName))
        {
            return BadRequest(new
            {
                message = "Tên ngựa không được để trống."
            });
        }

        if (request.Age <= 0)
        {
            return BadRequest(new
            {
                message = "Tuổi ngựa phải lớn hơn 0."
            });
        }

        if (request.WeightKg <= 0)
        {
            return BadRequest(new
            {
                message = "Cân nặng ngựa phải lớn hơn 0."
            });
        }

        if (!HorseHealthStatuses.IsValid(request.HealthStatus))
        {
            return BadRequest(new
            {
                message = "Tình trạng sức khỏe không hợp lệ.",
                allowedValues = HorseHealthStatuses.All
            });
        }

        horse.BreedId = request.BreedId;
        horse.HorseName = request.HorseName.Trim();
        horse.Age = request.Age;
        horse.HeightCm = request.HeightCm;
        horse.WeightKg = request.WeightKg;
        horse.HealthStatus = request.HealthStatus.Trim();
        horse.AchievementSummary = request.AchievementSummary;
        horse.ImageUrl = request.ImageUrl;
        horse.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Cập nhật ngựa thành công.",
            horseId = horse.HorseId
        });
    }

    [HttpGet("horses/stats")]
    public async Task<IActionResult> GetHorseStats()
    {
        var ownerId = GetCurrentUserId();

        if (ownerId == null)
        {
            return Unauthorized(new
            {
                message = "Token không hợp lệ hoặc thiếu UserId."
            });
        }

        var ownerError = await ValidateOwnerCanManageHorsesAsync(ownerId.Value);

        if (ownerError != null)
        {
            return ownerError;
        }

        var totalHorses = await _context.Horses
            .CountAsync(h => h.OwnerId == ownerId.Value);

        var activeHorses = await _context.Horses
            .CountAsync(h => h.OwnerId == ownerId.Value && h.IsActive);

        var injuredHorses = await _context.Horses
            .CountAsync(h =>
                h.OwnerId == ownerId.Value &&
                h.HealthStatus == HorseHealthStatuses.Injured);

        var inRaces = await _context.RaceRegistrations
            .Where(r => r.OwnerId == ownerId.Value)
            .Select(r => r.HorseId)
            .Distinct()
            .CountAsync();

        var response = new OwnerHorseStatsResponse
        {
            TotalHorses = totalHorses,
            ActiveHorses = activeHorses,
            InjuredHorses = injuredHorses,
            InRaces = inRaces
        };

        return Ok(response);
    }

    private async Task<IActionResult?> ValidateOwnerCanManageHorsesAsync(int ownerId)
    {
        var data = await _context.HorseOwners
            .AsNoTracking()
            .Where(o => o.OwnerId == ownerId)
            .Select(o => new
            {
                o.IsActive,
                UserStatus = o.Owner.Status,
                o.Owner.EmailVerified
            })
            .FirstOrDefaultAsync();

        if (data == null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Không tìm thấy hồ sơ HorseOwner.",
                nextStep = AuthNextSteps.Unknown
            });
        }

        if (!data.EmailVerified)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Email chưa được xác thực.",
                nextStep = AuthNextSteps.VerifyEmail
            });
        }

        if (data.UserStatus == UserStatuses.Inactive)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Tài khoản đang bị vô hiệu hóa.",
                nextStep = AuthNextSteps.ContactSupport
            });
        }

        if (data.UserStatus == UserStatuses.Banned)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Tài khoản đã bị khóa.",
                nextStep = AuthNextSteps.AccountBlocked
            });
        }

        if (data.UserStatus != UserStatuses.Pending && data.UserStatus != UserStatuses.Active)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Trạng thái tài khoản không hợp lệ.",
                status = data.UserStatus,
                nextStep = AuthNextSteps.Unknown
            });
        }

        return null;
    }

}