using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Owner;
using Eliteracingleague.API.DTOs.Owner.Results;
using Eliteracingleague.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Controllers.Owner;

[Route("api/owner/horses")]
[ApiController]
[Authorize(Roles = UserRoles.HorseOwner)]
public class OwnerHorsesController : OwnerBaseController
{
    public OwnerHorsesController(EliteRacingLeagueContext context) : base(context)
    {
    }

    [HttpPost]
    public async Task<IActionResult> CreateHorse(CreateOwnerHorseRequest request)
    {
        var ownerId = GetCurrentUserId();

        if (ownerId == null)
        {
            return InvalidToken();
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

        if (string.IsNullOrWhiteSpace(request.HealthStatus))
        {
            return BadRequest(new
            {
                message = "Tình trạng sức khỏe không được để trống."
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
            HealthCertificateImageUrl = request.HealthCertificateImageUrl,
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

    [HttpGet]
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
            return InvalidToken();
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
            if (status == HorseActivityStatuses.Active)
            {
                query = query.Where(h => h.IsActive);
            }
            else if (status == HorseActivityStatuses.Inactive)
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
                HealthCertificateImageUrl = h.HealthCertificateImageUrl,
                IsActive = h.IsActive,
                Status = h.IsActive
                    ? HorseActivityStatuses.Active
                    : HorseActivityStatuses.Inactive,
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

    [HttpGet("{horseId:int}")]
    public async Task<IActionResult> GetHorseDetail(int horseId)
    {
        var ownerId = GetCurrentUserId();

        if (ownerId == null)
        {
            return InvalidToken();
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
                HealthCertificateImageUrl = h.HealthCertificateImageUrl,
                IsActive = h.IsActive,
                Status = h.IsActive
                    ? HorseActivityStatuses.Active
                    : HorseActivityStatuses.Inactive,
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

    [HttpPut("{horseId:int}")]
    public async Task<IActionResult> UpdateHorse(int horseId, UpdateOwnerHorseRequest request)
    {
        var ownerId = GetCurrentUserId();

        if (ownerId == null)
        {
            return InvalidToken();
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
        horse.HealthCertificateImageUrl = request.HealthCertificateImageUrl;
        horse.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Cập nhật ngựa thành công.",
            horseId = horse.HorseId
        });
    }

    [HttpPatch("{horseId:int}/status")]
    public async Task<IActionResult> UpdateHorseStatus(int horseId, UpdateHorseStatusRequest request)
    {
        var ownerId = GetCurrentUserId();

        if (ownerId == null)
        {
            return InvalidToken();
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
            status = horse.IsActive
                ? HorseActivityStatuses.Active
                : HorseActivityStatuses.Inactive
        });
    }

    [HttpDelete("{horseId:int}")]
    public async Task<IActionResult> DeleteHorse(int horseId)
    {
        var ownerId = GetCurrentUserId();

        if (ownerId == null)
        {
            return InvalidToken();
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
                message = "Không tìm thấy ngựa hoặc bạn không có quyền xóa ngựa này."
            });
        }

        var hasActiveRegistration = await _context.RaceRegistrations
            .AnyAsync(r =>
                r.HorseId == horseId &&
                r.OwnerId == ownerId.Value &&
                RaceRegistrationStatuses.HorseDeleteBlockingStatuses.Contains(r.Status));

        if (hasActiveRegistration)
        {
            return BadRequest(new
            {
                message = "Không thể xóa ngựa đang có đăng ký cuộc đua đang hoạt động."
            });
        }

        var hasRegistrationHistory = await _context.RaceRegistrations
            .AnyAsync(r => r.HorseId == horseId);

        if (hasRegistrationHistory)
        {
            return BadRequest(new
            {
                message = "Không thể xóa ngựa đã có lịch sử đăng ký cuộc đua. Bạn có thể chuyển ngựa sang Inactive."
            });
        }

        _context.Horses.Remove(horse);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetHorseStats()
    {
        var ownerId = GetCurrentUserId();

        if (ownerId == null)
        {
            return InvalidToken();
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

    [HttpGet("{horseId:int}/performance")]
    public async Task<IActionResult> GetHorsePerformance(int horseId)
    {
        var ownerId = GetCurrentUserId();

        if (ownerId == null)
        {
            return InvalidToken();
        }

        var ownerError = await ValidateOwnerCanManageHorsesAsync(ownerId.Value);

        if (ownerError != null)
        {
            return ownerError;
        }

        var horseInfo = await _context.Horses
            .AsNoTracking()
            .Where(h => h.HorseId == horseId && h.OwnerId == ownerId.Value)
            .Select(h => new
            {
                h.HorseId,
                h.HorseName,
                BreedName = h.Breed.BreedName,
                h.ImageUrl,
                h.HealthCertificateImageUrl,
                h.Age,
                h.WeightKg,
                Award = h.AchievementSummary
            })
            .FirstOrDefaultAsync();

        if (horseInfo == null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy ngựa hoặc bạn không có quyền xem ngựa này."
            });
        }

        var ownerName = await _context.Users
            .AsNoTracking()
            .Where(u => u.UserId == ownerId.Value)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync();

        var assignedJockeyName = await _context.RaceRegistrations
            .AsNoTracking()
            .Where(r =>
                r.HorseId == horseId &&
                r.OwnerId == ownerId.Value &&
                r.JockeyId != null)
            .OrderByDescending(r => r.Race.RaceDate)
            .ThenByDescending(r => r.RegistrationId)
            .Select(r => r.Jockey == null
                ? null
                : r.Jockey.JockeyNavigation.FullName)
            .FirstOrDefaultAsync();

        var visibleStatuses = new[]
        {
            RaceResultStatuses.AdminApproved,
            RaceResultStatuses.Published
        };

        var raceHistory = await _context.RaceResults
            .AsNoTracking()
            .Where(r =>
                r.Registration.HorseId == horseId &&
                r.Registration.OwnerId == ownerId.Value &&
                visibleStatuses.Contains(r.Status))
            .OrderByDescending(r => r.Race.RaceDate)
            .ThenByDescending(r => r.ResultId)
            .Select(r => new OwnerHorseRaceHistoryResponse
            {
                RaceId = r.RaceId,
                ResultId = r.ResultId,
                TournamentName = r.Race.Tournament.TournamentName,
                RaceDate = r.Race.RaceDate,
                Track = r.Race.Location,
                DistanceMeters = r.Race.DistanceMeters,
                JockeyName = r.Registration.Jockey == null
                    ? null
                    : r.Registration.Jockey.JockeyNavigation.FullName,
                Position = r.FinishPosition,
                FinishTime = r.FinishTimeSeconds,
                Status = r.Status
            })
            .ToListAsync();

        var currentWinStreak = 0;

        foreach (var race in raceHistory)
        {
            if (race.Position != 1)
            {
                break;
            }

            currentWinStreak++;
        }

        var finishTimes = raceHistory
            .Where(r => r.FinishTime.HasValue)
            .Select(r => r.FinishTime!.Value)
            .ToList();

        var bestTime = finishTimes.Count > 0
            ? finishTimes.Min()
            : (decimal?)null;

        var response = new OwnerHorsePerformanceResponse
        {
            Horse = new OwnerHorsePerformanceInfoResponse
            {
                HorseId = horseInfo.HorseId,
                HorseName = horseInfo.HorseName,
                BreedName = horseInfo.BreedName,
                ImageUrl = horseInfo.ImageUrl,
                HealthCertificateImageUrl = horseInfo.HealthCertificateImageUrl,
                Age = horseInfo.Age,
                WeightKg = horseInfo.WeightKg,
                OwnerName = ownerName ?? string.Empty,
                AssignedJockeyName = assignedJockeyName
            },
            Achievements = new OwnerHorseAchievementResponse
            {
                ChampionTitles = raceHistory.Count(r => r.Position == 1),
                BestTime = bestTime,
                CurrentWinStreak = currentWinStreak,
                Award = horseInfo.Award
            },
            RaceHistory = raceHistory
        };

        return Ok(response);
    }
}
