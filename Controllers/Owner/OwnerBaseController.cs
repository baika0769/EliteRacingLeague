using System.Security.Claims;
using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Controllers.Owner;

public abstract class OwnerBaseController : ControllerBase
{
    protected readonly EliteRacingLeagueContext _context;

    protected OwnerBaseController(EliteRacingLeagueContext context)
    {
        _context = context;
    }

    protected int? GetCurrentUserId()
    {
        var userIdText = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!int.TryParse(userIdText, out var userId))
        {
            return null;
        }

        return userId;
    }

    protected IActionResult InvalidToken()
    {
        return Unauthorized(new
        {
            message = "Token không hợp lệ hoặc thiếu UserId."
        });
    }

    protected async Task<IActionResult?> ValidateOwnerProfileAsync(int ownerId)
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

    protected async Task<IActionResult?> ValidateOwnerCanManageHorsesAsync(int ownerId)
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

        // Owner Pending vẫn được vào My Horse Directory để thêm ngựa.
        if (data.UserStatus != UserStatuses.Pending &&
            data.UserStatus != UserStatuses.Active)
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