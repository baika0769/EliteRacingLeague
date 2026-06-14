using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Jockey;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Eliteracingleague.API.Controllers.Jockey;

[Route("api/jockey/dashboard")]
[ApiController]
[Authorize(Roles = UserRoles.Jockey)]
public class JockeyDashboardController : ControllerBase
{
    private static readonly string[] AcceptedRegistrationStatuses =
    {
        RaceRegistrationStatuses.ReadyToRace
    };

    private readonly EliteRacingLeagueContext _context;

    public JockeyDashboardController(EliteRacingLeagueContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetDashboard()
    {
        var jockeyIdText = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!int.TryParse(jockeyIdText, out var jockeyId))
        {
            return Unauthorized(new { message = "Token không hợp lệ." });
        }

        var jockey = await _context.Jockeys
            .AsNoTracking()
            .Include(j => j.JockeyNavigation)
            .FirstOrDefaultAsync(j => j.JockeyId == jockeyId);

        if (jockey == null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Không tìm thấy hồ sơ jockey."
            });
        }

        var user = jockey.JockeyNavigation;

        if (user.Role != UserRoles.Jockey)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Tài khoản không có quyền Jockey."
            });
        }

        if (!user.EmailVerified)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Email chưa được xác thực.",
                nextStep = AuthNextSteps.VerifyEmail
            });
        }

        if (user.Status == UserStatuses.Banned)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Tài khoản đã bị khóa.",
                nextStep = AuthNextSteps.AccountBlocked
            });
        }

        if (user.Status == UserStatuses.Inactive)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Tài khoản đang bị vô hiệu hóa.",
                nextStep = AuthNextSteps.ContactSupport
            });
        }

        if (user.Status != UserStatuses.Active || !jockey.IsActive)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Tài khoản jockey chưa được kích hoạt.",
                status = user.Status,
                isActive = jockey.IsActive,
                nextStep = AuthNextSteps.WaitForActivation
            });
        }

        var now = DateTime.UtcNow;

        var pendingInvitations = await _context.JockeyInvitations
            .AsNoTracking()
            .CountAsync(i => i.JockeyId == jockeyId
                && i.Status == InvitationStatuses.Pending);

        var acceptedRaces = await _context.RaceRegistrations
            .AsNoTracking()
            .CountAsync(r => r.JockeyId == jockeyId
                && AcceptedRegistrationStatuses.Contains(r.Status));

        var upcomingRaces = await _context.RaceRegistrations
            .AsNoTracking()
            .Where(r => r.JockeyId == jockeyId
                && AcceptedRegistrationStatuses.Contains(r.Status)
                && r.Race.RaceDate > now
                && r.Race.Status != RaceStatuses.Cancelled
                && r.Race.Status != RaceStatuses.Completed)
            .Select(r => r.RaceId)
            .Distinct()
            .CountAsync();

        var completedRaces = await _context.RaceRegistrations
            .AsNoTracking()
            .Where(r => r.JockeyId == jockeyId
                && (r.Status == RaceRegistrationStatuses.Completed
                    || r.Race.Status == RaceStatuses.Completed))
            .Select(r => r.RaceId)
            .Distinct()
            .CountAsync();

        return Ok(new JockeyDashboardResponse
        {
            PendingInvitations = pendingInvitations,
            AcceptedRaces = acceptedRaces,
            UpcomingRaces = upcomingRaces,
            CompletedRaces = completedRaces,
            ProfileStatus = user.Status,
            HealthStatus = jockey.HealthStatus
        });
    }
}
