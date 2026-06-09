using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

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
                message = "Không tìm thấy hồ sơ HorseOwner."
            });
        }

        if (!data.EmailVerified)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Email chưa được xác thực.",
                nextStep = "VerifyEmail"
            });
        }

        if (data.UserStatus == "Pending")
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Vui lòng thêm ngựa để kích hoạt tài khoản.",
                nextStep = "AddHorse"
            });
        }

        if (data.UserStatus == UserStatuses.Inactive)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Tài khoản đang bị vô hiệu hóa.",
                nextStep = "ContactSupport"
            });
        }

        if (data.UserStatus == UserStatuses.Banned)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Tài khoản đã bị khóa.",
                nextStep = "AccountBlocked"
            });
        }

        if (data.UserStatus != UserStatuses.Active || !data.IsActive)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Tài khoản HorseOwner chưa được kích hoạt.",
                status = data.UserStatus
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
                message = "Không tìm thấy hồ sơ HorseOwner."
            });
        }

        if (!data.EmailVerified)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Email chưa được xác thực.",
                nextStep = "VerifyEmail"
            });
        }

        if (data.UserStatus == UserStatuses.Inactive)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Tài khoản đang bị vô hiệu hóa.",
                nextStep = "ContactSupport"
            });
        }

        if (data.UserStatus == UserStatuses.Banned)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Tài khoản đã bị khóa.",
                nextStep = "AccountBlocked"
            });
        }

        if (data.UserStatus != "Pending" && data.UserStatus != UserStatuses.Active)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Trạng thái tài khoản không hợp lệ.",
                status = data.UserStatus
            });
        }

        return null;
    }
}