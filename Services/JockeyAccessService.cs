using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Jockey;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Eliteracingleague.API.Services;

public class JockeyAccessService
{
    private readonly EliteRacingLeagueContext _context;

    public JockeyAccessService(EliteRacingLeagueContext context)
    {
        _context = context;
    }

    public int? GetCurrentUserId(ClaimsPrincipal user)
    {
        var userIdText = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return int.TryParse(userIdText, out var userId) ? userId : null;
    }

    public async Task<JockeyAccessResult> ValidateActiveJockeyAsync(ClaimsPrincipal user)
    {
        var userId = GetCurrentUserId(user);

        if (userId == null)
        {
            return Fail(StatusCodes.Status401Unauthorized, "Token không hợp lệ.");
        }

        var currentUser = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == userId.Value);

        if (currentUser == null)
        {
            return Fail(StatusCodes.Status401Unauthorized, "Token không hợp lệ.");
        }

        if (currentUser.Role != UserRoles.Jockey)
        {
            return Fail(StatusCodes.Status403Forbidden, "Tài khoản không có quyền Jockey.");
        }

        if (!currentUser.EmailVerified)
        {
            return Fail(StatusCodes.Status403Forbidden, "Email chưa được xác thực.", AuthNextSteps.VerifyEmail, currentUser);
        }

        if (currentUser.Status == UserStatuses.Banned)
        {
            return Fail(StatusCodes.Status403Forbidden, "Tài khoản đã bị khóa.", AuthNextSteps.AccountBlocked, currentUser);
        }

        if (currentUser.Status != UserStatuses.Active)
        {
            return Fail(StatusCodes.Status403Forbidden, "Tài khoản jockey chưa được kích hoạt.", AuthNextSteps.WaitForActivation, currentUser);
        }

        var jockey = await _context.Jockeys
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.JockeyId == userId.Value);

        if (jockey == null)
        {
            return Fail(StatusCodes.Status403Forbidden, "Không tìm thấy hồ sơ jockey.", AuthNextSteps.CompleteJockeyProfile, currentUser);
        }

        if (!jockey.IsActive)
        {
            return Fail(StatusCodes.Status403Forbidden, "Tài khoản jockey chưa được kích hoạt.", AuthNextSteps.WaitForActivation, currentUser, jockey);
        }

        return new JockeyAccessResult
        {
            Succeeded = true,
            StatusCode = StatusCodes.Status200OK,
            User = currentUser,
            Jockey = jockey
        };
    }

    public Task<JockeyAccessResult> GetCurrentJockeyAsync(ClaimsPrincipal user)
    {
        return ValidateActiveJockeyAsync(user);
    }

    private static JockeyAccessResult Fail(
        int statusCode,
        string message,
        string? nextStep = null,
        Eliteracingleague.API.Models.User? user = null,
        Eliteracingleague.API.Models.Jockey? jockey = null)
    {
        return new JockeyAccessResult
        {
            Succeeded = false,
            StatusCode = statusCode,
            Message = message,
            NextStep = nextStep,
            User = user,
            Jockey = jockey
        };
    }
}
