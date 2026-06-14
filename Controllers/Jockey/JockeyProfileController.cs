using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Jockey;
using Eliteracingleague.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Eliteracingleague.API.Controllers.Jockey;

[Route("api/jockey/profile")]
[ApiController]
[Authorize(Roles = UserRoles.Jockey)]
public class JockeyProfileController : ControllerBase
{
    private readonly EliteRacingLeagueContext _context;

    public JockeyProfileController(EliteRacingLeagueContext context)
    {
        _context = context;
    }

    [HttpGet("me")]
    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings()
    {
        var jockeyIdText = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!int.TryParse(jockeyIdText, out var jockeyId))
        {
            return Unauthorized(new { message = "Token không hợp lệ." });
        }

        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == jockeyId);

        if (user == null)
        {
            return NotFound(new { message = "Không tìm thấy tài khoản jockey." });
        }

        if (user.Role != UserRoles.Jockey)
        {
            return Forbid();
        }

        var jockey = await _context.Jockeys
            .AsNoTracking()
            .Include(j => j.JockeyDistanceExperiences)
            .FirstOrDefaultAsync(j => j.JockeyId == jockeyId);

        if (jockey == null)
        {
            return NotFound(new { message = "Không tìm thấy hồ sơ jockey." });
        }

        var distanceExperiences = await _context.JockeyDistanceExperiences
            .AsNoTracking()
            .Where(e => e.JockeyId == jockeyId)
            .OrderBy(e => e.DistanceMeters)
            .ToListAsync();

        var breedExperiences = await _context.JockeyBreedExperiences
            .AsNoTracking()
            .Include(e => e.Breed)
            .Where(e => e.JockeyId == jockeyId)
            .OrderBy(e => e.Breed.BreedName)
            .Select(e => new JockeyBreedExperienceResponse
            {
                JockeyBreedExperienceId = e.JockeyBreedExperienceId,
                BreedId = e.BreedId,
                BreedName = e.Breed.BreedName,
                ExperienceLevel = e.ExperienceLevel
            })
            .ToListAsync();

        var response = new JockeyProfileResponse
        {
            UserId = user.UserId,
            JockeyId = jockey.JockeyId,
            FullName = user.FullName,
            Email = user.Email,
            Phone = user.Phone,
            Status = user.Status,
            EmailVerified = user.EmailVerified,
            NextStep = GetNextStep(user, jockey),
            WeightKg = jockey.WeightKg,
            YearsOfExperience = jockey.YearsOfExperience,
            HealthStatus = jockey.HealthStatus,
            CertificateNo = jockey.CertificateNo,
            CertificateFileUrl = jockey.CertificateFileUrl,
            ProfileImageUrl = jockey.ProfileImageUrl,
            IdCardFrontUrl = jockey.IdCardFrontUrl,
            IdCardBackUrl = jockey.IdCardBackUrl,
            HealthCertificateUrl = jockey.HealthCertificateUrl,
            IsActive = jockey.IsActive,
            CreatedAt = jockey.CreatedAt,
            DistanceExperiences = distanceExperiences
                .Select(e => new JockeyDistanceExperienceResponse
                {
                    JockeyDistanceExperienceId = e.JockeyDistanceExperienceId,
                    DistanceMeters = e.DistanceMeters,
                    DistanceLabel = JockeyDistanceMeters.Labels.TryGetValue(e.DistanceMeters, out var label)
                        ? label
                        : $"{e.DistanceMeters}m",
                    SkillLevel = e.SkillLevel
                })
                .ToList(),
            BreedExperiences = breedExperiences
        };

        return Ok(response);
    }

    [HttpPut("me")]
    public async Task<IActionResult> UpdateProfile(UpdateJockeyProfileRequest request)
    {
        var jockeyIdText = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!int.TryParse(jockeyIdText, out var jockeyId))
        {
            return Unauthorized(new { message = "Token không hợp lệ." });
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == jockeyId);

        if (user == null)
        {
            return NotFound(new { message = "Không tìm thấy tài khoản jockey." });
        }

        if (user.Role != UserRoles.Jockey)
        {
            return Forbid();
        }

        var jockey = await _context.Jockeys.FirstOrDefaultAsync(j => j.JockeyId == jockeyId);

        if (jockey == null)
        {
            return NotFound(new { message = "Không tìm thấy hồ sơ jockey." });
        }

        if (!user.EmailVerified)
        {
            return BadRequest(new
            {
                message = "Email chưa được xác thực.",
                nextStep = AuthNextSteps.VerifyEmail
            });
        }

        if (user.Status == UserStatuses.Banned)
        {
            return BadRequest(new
            {
                message = "Tài khoản đã bị khóa.",
                nextStep = AuthNextSteps.AccountBlocked
            });
        }

        if (user.Status == UserStatuses.Inactive)
        {
            return BadRequest(new
            {
                message = "Tài khoản đang bị vô hiệu hóa.",
                nextStep = AuthNextSteps.ContactSupport
            });
        }

        if (user.Status != UserStatuses.Active || !jockey.IsActive)
        {
            return BadRequest(new
            {
                message = "Chỉ jockey đã được duyệt mới được cập nhật thông tin cá nhân.",
                status = user.Status,
                isActive = jockey.IsActive,
                nextStep = AuthNextSteps.WaitForActivation
            });
        }

        user.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();

        if (!string.IsNullOrWhiteSpace(request.ProfileImageUrl))
        {
            jockey.ProfileImageUrl = request.ProfileImageUrl.Trim();
        }

        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Cập nhật thông tin cá nhân thành công.",
            phone = user.Phone,
            profileImageUrl = jockey.ProfileImageUrl,
            status = user.Status,
            isActive = jockey.IsActive,
            nextStep = AuthNextSteps.GoToDashboard
        });
    }

    [HttpPut("verification")]
    public async Task<IActionResult> UpdateVerification(UpdateJockeyVerificationRequest request)
    {
        var jockeyIdText = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!int.TryParse(jockeyIdText, out var jockeyId))
        {
            return Unauthorized(new { message = "Token không hợp lệ." });
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == jockeyId);

        if (user == null)
        {
            return NotFound(new { message = "Không tìm thấy tài khoản jockey." });
        }

        if (user.Role != UserRoles.Jockey)
        {
            return Forbid();
        }

        var jockey = await _context.Jockeys.FirstOrDefaultAsync(j => j.JockeyId == jockeyId);

        if (jockey == null)
        {
            return NotFound(new { message = "Không tìm thấy hồ sơ jockey." });
        }

        if (!user.EmailVerified)
        {
            return BadRequest(new
            {
                message = "Email chưa được xác thực.",
                nextStep = AuthNextSteps.VerifyEmail
            });
        }

        if (user.Status == UserStatuses.Banned || user.Status == UserStatuses.Inactive)
        {
            return BadRequest(new
            {
                message = "Tài khoản không được phép cập nhật hồ sơ.",
                nextStep = AuthNextSteps.ContactSupport
            });
        }

        request.DistanceExperiences ??= new();
        request.BreedExperiences ??= new();

        if (request.WeightKg <= 0)
        {
            return BadRequest(new
            {
                message = "Cân nặng phải lớn hơn 0.",
                nextStep = AuthNextSteps.CompleteJockeyProfile
            });
        }

        if (request.YearsOfExperience < 0)
        {
            return BadRequest(new
            {
                message = "Số năm kinh nghiệm không hợp lệ.",
                nextStep = AuthNextSteps.CompleteJockeyProfile
            });
        }

        if (!HorseHealthStatuses.IsValid(request.HealthStatus))
        {
            return BadRequest(new
            {
                message = "Tình trạng sức khỏe không hợp lệ.",
                nextStep = AuthNextSteps.CompleteJockeyProfile
            });
        }

        if (!HasRequiredDocuments(request))
        {
            return BadRequest(new
            {
                message = "Vui lòng bổ sung đầy đủ giấy tờ bắt buộc.",
                nextStep = AuthNextSteps.CompleteJockeyProfile
            });
        }

        if (request.DistanceExperiences.Any(e => !JockeyDistanceMeters.IsValid(e.DistanceMeters)))
        {
            return BadRequest(new
            {
                message = "Cự ly kinh nghiệm không hợp lệ.",
                nextStep = AuthNextSteps.CompleteJockeyProfile
            });
        }

        if (request.DistanceExperiences.Any(e => !JockeyDistanceSkillLevels.IsValid(e.SkillLevel)))
        {
            return BadRequest(new
            {
                message = "Cấp độ kinh nghiệm theo cự ly không hợp lệ.",
                nextStep = AuthNextSteps.CompleteJockeyProfile
            });
        }

        if (request.DistanceExperiences
            .GroupBy(e => e.DistanceMeters)
            .Any(g => g.Count() > 1))
        {
            return BadRequest(new
            {
                message = "Cự ly kinh nghiệm bị trùng.",
                nextStep = AuthNextSteps.CompleteJockeyProfile
            });
        }

        if (!HasRequiredDistanceExperiences(request.DistanceExperiences))
        {
            return BadRequest(new
            {
                message = "Vui lòng bổ sung đầy đủ kinh nghiệm theo cự ly bắt buộc.",
                nextStep = AuthNextSteps.CompleteJockeyProfile
            });
        }

        if (request.BreedExperiences.Any(e => !JockeyBreedSkillLevels.IsValid(e.ExperienceLevel)))
        {
            return BadRequest(new
            {
                message = "Cấp độ kinh nghiệm theo giống ngựa không hợp lệ.",
                nextStep = AuthNextSteps.CompleteJockeyProfile
            });
        }

        if (request.BreedExperiences
            .GroupBy(e => e.BreedId)
            .Any(g => g.Count() > 1))
        {
            return BadRequest(new
            {
                message = "Giống ngựa kinh nghiệm bị trùng.",
                nextStep = AuthNextSteps.CompleteJockeyProfile
            });
        }

        var requestedBreedIds = request.BreedExperiences
            .Select(e => e.BreedId)
            .Distinct()
            .ToList();

        if (requestedBreedIds.Count > 0)
        {
            var validBreedCount = await _context.HorseBreeds
                .CountAsync(b => requestedBreedIds.Contains(b.BreedId) && b.IsActive);

            if (validBreedCount != requestedBreedIds.Count)
            {
                return BadRequest(new
                {
                    message = "Giống ngựa không hợp lệ.",
                    nextStep = AuthNextSteps.CompleteJockeyProfile
                });
            }
        }

        jockey.WeightKg = request.WeightKg;
        jockey.YearsOfExperience = request.YearsOfExperience;
        jockey.HealthStatus = request.HealthStatus!;
        jockey.CertificateNo = request.CertificateNo;
        jockey.CertificateFileUrl = request.CertificateFileUrl;
        jockey.ProfileImageUrl = request.ProfileImageUrl;
        jockey.IdCardFrontUrl = request.IdCardFrontUrl;
        jockey.IdCardBackUrl = request.IdCardBackUrl;
        jockey.HealthCertificateUrl = request.HealthCertificateUrl;
        jockey.IsActive = false;

        user.Status = UserStatuses.Pending;
        user.UpdatedAt = DateTime.UtcNow;

        var existingDistanceExperiences = await _context.JockeyDistanceExperiences
            .Where(e => e.JockeyId == jockeyId)
            .ToListAsync();

        _context.JockeyDistanceExperiences.RemoveRange(existingDistanceExperiences);

        foreach (var distanceExperience in request.DistanceExperiences)
        {
            _context.JockeyDistanceExperiences.Add(new Eliteracingleague.API.Models.JockeyDistanceExperience
            {
                JockeyId = jockeyId,
                DistanceMeters = distanceExperience.DistanceMeters,
                SkillLevel = distanceExperience.SkillLevel
            });
        }

        var existingBreedExperiences = await _context.JockeyBreedExperiences
            .Where(e => e.JockeyId == jockeyId)
            .ToListAsync();

        _context.JockeyBreedExperiences.RemoveRange(existingBreedExperiences);

        foreach (var breedExperience in request.BreedExperiences)
        {
            _context.JockeyBreedExperiences.Add(new Eliteracingleague.API.Models.JockeyBreedExperience
            {
                JockeyId = jockeyId,
                BreedId = breedExperience.BreedId,
                ExperienceLevel = breedExperience.ExperienceLevel
            });
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Đã gửi hồ sơ. Vui lòng chờ admin duyệt.",
            status = user.Status,
            isActive = jockey.IsActive,
            nextStep = AuthNextSteps.WaitForActivation
        });
    }

    private static string GetNextStep(Eliteracingleague.API.Models.User user, Eliteracingleague.API.Models.Jockey jockey)
    {
        if (!user.EmailVerified)
        {
            return AuthNextSteps.VerifyEmail;
        }

        if (user.Status == UserStatuses.Banned)
        {
            return AuthNextSteps.AccountBlocked;
        }

        if (user.Status == UserStatuses.Inactive)
        {
            return AuthNextSteps.ContactSupport;
        }

        if (!JockeyProfileService.IsJockeyProfileCompleted(jockey))
        {
            return AuthNextSteps.CompleteJockeyProfile;
        }

        if (user.Status == UserStatuses.Pending || !jockey.IsActive)
        {
            return AuthNextSteps.WaitForActivation;
        }

        if (user.Status == UserStatuses.Active && jockey.IsActive)
        {
            return AuthNextSteps.GoToDashboard;
        }

        return AuthNextSteps.ContactSupport;
    }

    private static bool HasRequiredDocuments(UpdateJockeyVerificationRequest request)
    {
        return !string.IsNullOrWhiteSpace(request.ProfileImageUrl)
            && !string.IsNullOrWhiteSpace(request.IdCardFrontUrl)
            && !string.IsNullOrWhiteSpace(request.IdCardBackUrl)
            && !string.IsNullOrWhiteSpace(request.CertificateNo)
            && !string.IsNullOrWhiteSpace(request.CertificateFileUrl)
            && !string.IsNullOrWhiteSpace(request.HealthCertificateUrl);
    }

    private static bool HasRequiredDistanceExperiences(IEnumerable<JockeyDistanceExperienceRequest> distanceExperiences)
    {
        var distances = distanceExperiences.Select(e => e.DistanceMeters).Distinct().ToHashSet();

        return JockeyDistanceMeters.All.All(distances.Contains)
            && distanceExperiences.All(e => JockeyDistanceMeters.IsValid(e.DistanceMeters)
                && JockeyDistanceSkillLevels.IsValid(e.SkillLevel));
    }
}
