using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Jockey;
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

    [HttpPut("verification")]
    public async Task<IActionResult> UpdateVerification(UpdateJockeyVerificationRequest request)
    {
        var jockeyIdText = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!int.TryParse(jockeyIdText, out var jockeyId))
        {
            return Unauthorized(new { message = "Token không hợp lệ." });
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == jockeyId);
        var jockey = await _context.Jockeys.FirstOrDefaultAsync(j => j.JockeyId == jockeyId);

        if (user == null || jockey == null)
        {
            return NotFound(new { message = "Không tìm thấy hồ sơ jockey." });
        }

        if (!user.EmailVerified)
        {
            return BadRequest(new { message = "Email chưa được xác thực." });
        }

        if (user.Status == UserStatuses.Banned || user.Status == UserStatuses.Inactive)
        {
            return BadRequest(new { message = "Tài khoản không được phép cập nhật hồ sơ." });
        }

        jockey.WeightKg = request.WeightKg;
        jockey.YearsOfExperience = request.YearsOfExperience;
        jockey.CertificateNo = request.CertificateNo;
        jockey.CertificateFileUrl = request.CertificateFileUrl;
        jockey.ProfileImageUrl = request.ProfileImageUrl;
        jockey.IdCardFrontUrl = request.IdCardFrontUrl;
        jockey.IdCardBackUrl = request.IdCardBackUrl;
        jockey.HealthCertificateUrl = request.HealthCertificateUrl;

        var isCompleted =
            !string.IsNullOrWhiteSpace(jockey.ProfileImageUrl) &&
            !string.IsNullOrWhiteSpace(jockey.IdCardFrontUrl) &&
            !string.IsNullOrWhiteSpace(jockey.IdCardBackUrl) &&
            !string.IsNullOrWhiteSpace(jockey.CertificateFileUrl) &&
            !string.IsNullOrWhiteSpace(jockey.HealthCertificateUrl);

        if (isCompleted)
        {
            user.Status = UserStatuses.Active;
            user.UpdatedAt = DateTime.UtcNow;
            jockey.IsActive = true;
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = isCompleted
                ? "Cập nhật hồ sơ thành công. Tài khoản Jockey đã được kích hoạt."
                : "Đã lưu hồ sơ. Vui lòng bổ sung đầy đủ giấy tờ.",
            status = user.Status,
            isActive = jockey.IsActive,
            nextStep = isCompleted
                ? AuthNextSteps.GoToDashboard
                : AuthNextSteps.CompleteJockeyProfile
        });
    }
}