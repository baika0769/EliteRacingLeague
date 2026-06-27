using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Jockey.Notifications;
using Eliteracingleague.API.DTOs.Jockey;
using Eliteracingleague.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Eliteracingleague.API.Controllers.Jockey;

[Route("api/jockey/notifications")]
[ApiController]
[Authorize(Roles = UserRoles.Jockey)]
public class JockeyNotificationsController : ControllerBase
{
    private const int DefaultPageSize = 10;
    private const int MaxPageSize = 50;

    private readonly EliteRacingLeagueContext _context;
    private readonly JockeyAccessService _jockeyAccess;

    public JockeyNotificationsController(EliteRacingLeagueContext context, JockeyAccessService jockeyAccess)
    {
        _context = context;
        _jockeyAccess = jockeyAccess;
    }

    [HttpGet]
    public async Task<IActionResult> GetNotifications([FromQuery] JockeyNotificationListQuery query)
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

        var page = query.Page <= 0 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0 ? DefaultPageSize : query.PageSize;
        pageSize = Math.Min(pageSize, MaxPageSize);

        var notificationsQuery = _context.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == jockeyId.Value);

        if (string.Equals(query.Status, "Unread", StringComparison.OrdinalIgnoreCase))
        {
            notificationsQuery = notificationsQuery.Where(n => !n.IsRead);
        }
        else if (string.Equals(query.Status, "Read", StringComparison.OrdinalIgnoreCase))
        {
            notificationsQuery = notificationsQuery.Where(n => n.IsRead);
        }
        else if (!string.IsNullOrWhiteSpace(query.Status) &&
                 !string.Equals(query.Status, "All", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Status không hợp lệ. Giá trị hợp lệ: All, Unread, Read." });
        }

        if (query.Date.HasValue)
        {
            var date = query.Date.Value.Date;
            var nextDate = date.AddDays(1);
            notificationsQuery = notificationsQuery.Where(n => n.CreatedAt >= date && n.CreatedAt < nextDate);
        }

        if (string.Equals(query.Sort, "Oldest", StringComparison.OrdinalIgnoreCase))
        {
            notificationsQuery = notificationsQuery.OrderBy(n => n.CreatedAt);
        }
        else if (string.IsNullOrWhiteSpace(query.Sort) ||
                 string.Equals(query.Sort, "Newest", StringComparison.OrdinalIgnoreCase))
        {
            notificationsQuery = notificationsQuery.OrderByDescending(n => n.CreatedAt);
        }
        else
        {
            return BadRequest(new { message = "Sort không hợp lệ. Giá trị hợp lệ: Newest, Oldest." });
        }

        var totalItems = await notificationsQuery.CountAsync();

        var notifications = await notificationsQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new
            {
                n.NotificationId,
                n.Title,
                n.Message,
                n.IsRead,
                n.CreatedAt
            })
            .ToListAsync();

        var response = new JockeyNotificationListResponse
        {
            Items = notifications.Select(n => new JockeyNotificationItemResponse
            {
                NotificationId = n.NotificationId,
                Title = n.Title,
                Message = n.Message,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt,
                DisplayTime = GetDisplayTime(n.CreatedAt)
            }).ToList(),
            TotalItems = totalItems,
            Page = page,
            PageSize = pageSize
        };

        return Ok(response);
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
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

        var totalAlerts = await _context.Notifications
            .AsNoTracking()
            .CountAsync(n => n.UserId == jockeyId.Value);

        var unread = await _context.Notifications
            .AsNoTracking()
            .CountAsync(n => n.UserId == jockeyId.Value && !n.IsRead);

        var invitations = await _context.JockeyInvitations
            .AsNoTracking()
            .CountAsync(i => i.JockeyId == jockeyId.Value && i.Status == InvitationStatuses.Pending);

        return Ok(new JockeyNotificationSummaryResponse
        {
            TotalAlerts = totalAlerts,
            Unread = unread,
            Invitations = invitations
        });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetNotificationDetail(int id)
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

        var notification = await _context.Notifications
            .AsNoTracking()
            .Where(n => n.NotificationId == id && n.UserId == jockeyId.Value)
            .Select(n => new
            {
                n.NotificationId,
                n.Title,
                n.Message,
                n.IsRead,
                n.CreatedAt
            })
            .FirstOrDefaultAsync();

        if (notification == null)
        {
            return NotFound(new { message = "Không tìm thấy notification." });
        }

        var response = new JockeyNotificationDetailResponse
        {
            NotificationId = notification.NotificationId,
            Title = notification.Title,
            Message = notification.Message,
            IsRead = notification.IsRead,
            CreatedAt = notification.CreatedAt,
            DisplayTime = GetDisplayTime(notification.CreatedAt),
            RaceDetail = await FindRaceDetailAsync(jockeyId.Value, notification.CreatedAt)
        };

        return Ok(response);
    }

    [HttpPut("{id:int}/read")]
    public async Task<IActionResult> MarkAsRead(int id)
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

        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.NotificationId == id && n.UserId == jockeyId.Value);

        if (notification == null)
        {
            return NotFound(new { message = "Không tìm thấy notification." });
        }

        notification.IsRead = true;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Đã đánh dấu notification là đã đọc.",
            notificationId = notification.NotificationId,
            isRead = notification.IsRead
        });
    }

    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllAsRead()
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

        var notifications = await _context.Notifications
            .Where(n => n.UserId == jockeyId.Value && !n.IsRead)
            .ToListAsync();

        foreach (var notification in notifications)
        {
            notification.IsRead = true;
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Đã đánh dấu tất cả notification là đã đọc.",
            updatedCount = notifications.Count
        });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteNotification(int id)
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

        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.NotificationId == id && n.UserId == jockeyId.Value);

        if (notification == null)
        {
            return NotFound(new { message = "Không tìm thấy notification." });
        }

        _context.Notifications.Remove(notification);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Đã xóa notification.",
            notificationId = id
        });
    }

    private int? GetCurrentJockeyId()
    {
        return _jockeyAccess.GetCurrentUserId(User);
    }

    private IActionResult InvalidToken()
    {
        return Unauthorized(new { message = "Token không hợp lệ." });
    }

    private async Task<IActionResult?> ValidateActiveJockeyAsync(int jockeyId)
    {
        if (_jockeyAccess != null)
        {
            var access = await _jockeyAccess.ValidateActiveJockeyAsync(User);
            return access.Succeeded ? null : AccessError(access);
        }

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

    private IActionResult AccessError(JockeyAccessResult access)
    {
        if (access.StatusCode == StatusCodes.Status401Unauthorized)
        {
            return Unauthorized(new { message = access.Message });
        }

        return StatusCode(access.StatusCode, new
        {
            message = access.Message,
            nextStep = access.NextStep
        });
    }

    private async Task<JockeyNotificationRaceDetailResponse?> FindRaceDetailAsync(
        int jockeyId,
        DateTime notificationCreatedAt)
    {
        return await _context.JockeyInvitations
            .AsNoTracking()
            .Where(i => i.JockeyId == jockeyId && i.SentAt == notificationCreatedAt)
            .OrderByDescending(i => i.InvitationId)
            .Select(i => new JockeyNotificationRaceDetailResponse
            {
                RaceId = i.Registration.RaceId,
                RaceName = i.Registration.Race.RaceName,
                RaceDate = i.Registration.Race.RaceDate,
                Location = i.Registration.Race.Location,
                HorseId = i.Registration.HorseId,
                HorseName = i.Registration.Horse.HorseName,
                HorseAge = i.Registration.Horse.Age,
                HorseImageUrl = i.Registration.Horse.ImageUrl,
                HorseHealthStatus = i.Registration.Horse.HealthStatus,
                HealthCertificateImageUrl = i.Registration.Horse.HealthCertificateImageUrl,
                OwnerName = i.InvitedByOwner.Owner.FullName,
                OwnerMessage = i.Message
            })
            .FirstOrDefaultAsync();
    }

    private static string GetDisplayTime(DateTime createdAt)
    {
        var elapsed = DateTime.UtcNow - createdAt;

        if (elapsed.TotalMinutes < 1)
        {
            return "Just now";
        }

        if (elapsed.TotalHours < 1)
        {
            return $"{Math.Max(1, (int)elapsed.TotalMinutes)} mins ago";
        }

        if (elapsed.TotalDays < 1)
        {
            return $"{Math.Max(1, (int)elapsed.TotalHours)} hours ago";
        }

        return $"{Math.Max(1, (int)elapsed.TotalDays)} days ago";
    }
}
