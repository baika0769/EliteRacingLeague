using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Jockey;
using Eliteracingleague.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Eliteracingleague.API.Controllers.Jockey;

[Route("api/jockey/invitations")]
[ApiController]
[Authorize(Roles = UserRoles.Jockey)]
public class JockeyInvitationsController : ControllerBase
{
    private readonly EliteRacingLeagueContext _context;

    public JockeyInvitationsController(EliteRacingLeagueContext context)
    {
        _context = context;
    }

    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingInvitations()
    {
        var jockeyId = GetCurrentJockeyId();

        if (jockeyId == null)
        {
            return InvalidToken();
        }

        var profileError = await ValidateActiveJockeyAsync(jockeyId.Value);

        if (profileError != null)
        {
            return profileError;
        }

        var invitations = await _context.JockeyInvitations
            .AsNoTracking()
            .Where(i => i.JockeyId == jockeyId.Value
                && i.Status == InvitationStatuses.Pending)
            .OrderByDescending(i => i.SentAt)
            .Select(i => new JockeyPendingInvitationResponse
            {
                InvitationId = i.InvitationId,
                RegistrationId = i.RegistrationId,
                RaceId = i.Registration.RaceId,
                RaceName = i.Registration.Race.RaceName,
                RaceDate = i.Registration.Race.RaceDate,
                Location = i.Registration.Race.Location,
                OwnerId = i.InvitedByOwnerId,
                OwnerName = i.InvitedByOwner.Owner.FullName,
                Status = i.Status,
                SentAt = i.SentAt
            })
            .ToListAsync();

        return Ok(invitations);
    }

    [HttpPut("{id}/accept")]
    public async Task<IActionResult> AcceptInvitation(int id)
    {
        var jockeyId = GetCurrentJockeyId();

        if (jockeyId == null)
        {
            return InvalidToken();
        }

        var profileError = await ValidateActiveJockeyAsync(jockeyId.Value);

        if (profileError != null)
        {
            return profileError;
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();

        var invitation = await LoadOwnedInvitationAsync(id, jockeyId.Value);

        if (invitation == null)
        {
            return NotFound(new { message = "Không tìm thấy lời mời." });
        }

        if (invitation.Status != InvitationStatuses.Pending)
        {
            return BadRequest(new
            {
                message = "Lời mời không còn ở trạng thái chờ.",
                status = invitation.Status
            });
        }

        var now = DateTime.UtcNow;
        var registration = invitation.Registration;

        invitation.Status = InvitationStatuses.Accepted;
        invitation.RespondedAt = now;

        registration.JockeyId = jockeyId.Value;
        registration.Status = RaceRegistrationStatuses.ReadyToRace;
        registration.JockeyConfirmedAt = now;

        var otherPendingInvitations = await _context.JockeyInvitations
            .Where(i => i.RegistrationId == invitation.RegistrationId
                && i.InvitationId != invitation.InvitationId
                && i.Status == InvitationStatuses.Pending)
            .ToListAsync();

        foreach (var otherInvitation in otherPendingInvitations)
        {
            otherInvitation.Status = InvitationStatuses.Cancelled;
            otherInvitation.RespondedAt = now;
        }

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return Ok(new
        {
            message = "Đã chấp nhận lời mời.",
            status = invitation.Status
        });
    }

    [HttpPut("{id}/reject")]
    public async Task<IActionResult> RejectInvitation(int id)
    {
        var jockeyId = GetCurrentJockeyId();

        if (jockeyId == null)
        {
            return InvalidToken();
        }

        var profileError = await ValidateActiveJockeyAsync(jockeyId.Value);

        if (profileError != null)
        {
            return profileError;
        }

        var invitation = await LoadOwnedInvitationAsync(id, jockeyId.Value);

        if (invitation == null)
        {
            return NotFound(new { message = "Không tìm thấy lời mời." });
        }

        if (invitation.Status != InvitationStatuses.Pending)
        {
            return BadRequest(new
            {
                message = "Lời mời không còn ở trạng thái chờ.",
                status = invitation.Status
            });
        }

        invitation.Status = InvitationStatuses.Rejected;
        invitation.RespondedAt = DateTime.UtcNow;

        if (invitation.Registration.JockeyId == jockeyId.Value)
        {
            invitation.Registration.JockeyId = null;
            invitation.Registration.JockeyConfirmedAt = null;
            invitation.Registration.Status = RaceRegistrationStatuses.JockeyInvited;
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Đã từ chối lời mời.",
            status = invitation.Status
        });
    }

    private int? GetCurrentJockeyId()
    {
        var jockeyIdText = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return int.TryParse(jockeyIdText, out var jockeyId) ? jockeyId : null;
    }

    private IActionResult InvalidToken()
    {
        return Unauthorized(new { message = "Token không hợp lệ." });
    }

    private async Task<IActionResult?> ValidateActiveJockeyAsync(int jockeyId)
    {
        var data = await _context.Jockeys
            .AsNoTracking()
            .Where(j => j.JockeyId == jockeyId)
            .Select(j => new
            {
                j.IsActive,
                j.JockeyNavigation.Role,
                UserStatus = j.JockeyNavigation.Status,
                j.JockeyNavigation.EmailVerified
            })
            .FirstOrDefaultAsync();

        if (data == null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Không tìm thấy hồ sơ jockey."
            });
        }

        if (data.Role != UserRoles.Jockey)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Tài khoản không có quyền Jockey."
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

        if (data.UserStatus == UserStatuses.Banned)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Tài khoản đã bị khóa.",
                nextStep = AuthNextSteps.AccountBlocked
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

        if (data.UserStatus != UserStatuses.Active || !data.IsActive)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Tài khoản jockey chưa được kích hoạt.",
                status = data.UserStatus,
                isActive = data.IsActive,
                nextStep = AuthNextSteps.WaitForActivation
            });
        }

        return null;
    }

    private async Task<JockeyInvitation?> LoadOwnedInvitationAsync(int invitationId, int jockeyId)
    {
        return await _context.JockeyInvitations
            .Include(i => i.Registration)
            .FirstOrDefaultAsync(i => i.InvitationId == invitationId
                && i.JockeyId == jockeyId);
    }
}
