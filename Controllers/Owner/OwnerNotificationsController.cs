using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Owner;
using Eliteracingleague.API.DTOs.Owner.Notifications;
using Eliteracingleague.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Controllers.Owner;

[ApiController]
[Route("api/owner/notifications")]
[Authorize(Roles = UserRoles.HorseOwner)]
public class OwnerNotificationsController : OwnerBaseController
{
    private const int DefaultPageSize = 10;
    private const int MaxPageSize = 50;

    private static readonly string[] SupportedCategories =
    {
        "All",
        "Registrations",
        "Jockeys",
        "Tournaments"
    };

    public OwnerNotificationsController(EliteRacingLeagueContext context)
        : base(context)
    {
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        var ownerId = GetCurrentUserId();
        if (ownerId == null)
        {
            return InvalidToken();
        }

        var profileError = await ValidateOwnerProfileAsync(ownerId.Value);
        if (profileError != null)
        {
            return profileError;
        }

        var unread = await _context.Notifications
            .AsNoTracking()
            .CountAsync(n => n.UserId == ownerId.Value && !n.IsRead);

        var invitations = await _context.Notifications
            .AsNoTracking()
            .CountAsync(n => n.UserId == ownerId.Value &&
                (n.Title.Contains("Invitation Accepted") ||
                 n.Message.Contains("Invitation Accepted") ||
                 n.Title.Contains("Invitation Rejected") ||
                 n.Message.Contains("Invitation Rejected") ||
                 n.Title.Contains("Official Jockey") ||
                 n.Message.Contains("Official Jockey") ||
                 n.Title.Contains("Jockey") ||
                 n.Message.Contains("Jockey") ||
                 n.Title.Contains("lời mời") ||
                 n.Message.Contains("lời mời")));

        var upcomingRaceCandidates = await _context.RaceRegistrations
            .AsNoTracking()
            .Where(r => r.OwnerId == ownerId.Value &&
                r.Race.RaceDate >= DateTime.UtcNow &&
                (r.Status == RaceRegistrationStatuses.Approved ||
                 r.Status == RaceRegistrationStatuses.JockeyInvited ||
                 r.Status == RaceRegistrationStatuses.ReadyToRace))
            .Select(r => new
            {
                r.RaceId,
                RaceStatus = r.Race.Status
            })
            .ToListAsync();

        var upcomingRaces = upcomingRaceCandidates
            .Where(r => !RaceStatuses.IsClosedForJockeyAssignment(r.RaceStatus))
            .Select(r => r.RaceId)
            .Distinct()
            .Count();

        return Ok(new OwnerNotificationSummaryResponse
        {
            Unread = unread,
            Invitations = invitations,
            UpcomingRaces = upcomingRaces
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] string category = "All",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize)
    {
        var ownerId = GetCurrentUserId();
        if (ownerId == null)
        {
            return InvalidToken();
        }

        var profileError = await ValidateOwnerProfileAsync(ownerId.Value);
        if (profileError != null)
        {
            return profileError;
        }

        var normalizedCategory = SupportedCategories.FirstOrDefault(
            value => string.Equals(value, category, StringComparison.OrdinalIgnoreCase));

        if (normalizedCategory == null)
        {
            return BadRequest(new
            {
                message = $"Category must be one of: {string.Join(", ", SupportedCategories)}."
            });
        }

        page = Math.Max(1, page);
        pageSize = pageSize <= 0
            ? DefaultPageSize
            : Math.Min(pageSize, MaxPageSize);

        var query = _context.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == ownerId.Value);

        query = ApplyCategoryFilter(query, normalizedCategory);

        var totalItems = await query.CountAsync();
        var notifications = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new OwnerNotificationListResponse
        {
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize),
            Items = notifications.Select(MapNotification).ToList()
        });
    }

    [HttpGet("{notificationId:int}")]
    public async Task<IActionResult> GetNotificationDetail(int notificationId)
    {
        var ownerId = GetCurrentUserId();
        if (ownerId == null)
        {
            return InvalidToken();
        }

        var profileError = await ValidateOwnerProfileAsync(ownerId.Value);
        if (profileError != null)
        {
            return profileError;
        }

        var notification = await _context.Notifications
            .AsNoTracking()
            .FirstOrDefaultAsync(n =>
                n.NotificationId == notificationId &&
                n.UserId == ownerId.Value);

        if (notification == null)
        {
            return NotFound(new { message = "Notification not found." });
        }

        return Ok(MapNotificationDetail(notification));
    }

    [HttpPut("{notificationId:int}/read")]
    public async Task<IActionResult> MarkAsRead(int notificationId)
    {
        var ownerId = GetCurrentUserId();
        if (ownerId == null)
        {
            return InvalidToken();
        }

        var profileError = await ValidateOwnerProfileAsync(ownerId.Value);
        if (profileError != null)
        {
            return profileError;
        }

        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n =>
                n.NotificationId == notificationId &&
                n.UserId == ownerId.Value);

        if (notification == null)
        {
            return NotFound(new { message = "Notification not found." });
        }

        if (!notification.IsRead)
        {
        notification.IsRead = true;
        await _context.SaveChangesAsync();
        }

        return Ok(new OwnerNotificationMarkReadResponse
        {
            Message = "Notification marked as read.",
            NotificationId = notification.NotificationId,
            IsRead = notification.IsRead
        });
    }

    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var ownerId = GetCurrentUserId();
        if (ownerId == null)
        {
            return InvalidToken();
        }

        var profileError = await ValidateOwnerProfileAsync(ownerId.Value);
        if (profileError != null)
        {
            return profileError;
        }

        var notifications = await _context.Notifications
            .Where(n => n.UserId == ownerId.Value && !n.IsRead)
            .ToListAsync();

        foreach (var notification in notifications)
        {
            notification.IsRead = true;
        }

        if (notifications.Count > 0)
        {
        await _context.SaveChangesAsync();
        }

        return Ok(new OwnerNotificationMarkAllReadResponse
        {
            Message = "All notifications marked as read.",
            UpdatedCount = notifications.Count
        });
    }

    private static IQueryable<Notification> ApplyCategoryFilter(
        IQueryable<Notification> query,
        string category)
    {
        return category switch
        {
            "Registrations" => query.Where(n =>
                !(n.Title.Contains("Invitation") ||
                  n.Message.Contains("Invitation") ||
                  n.Title.Contains("Jockey") ||
                  n.Message.Contains("Jockey") ||
                  n.Title.Contains("lời mời") ||
                  n.Message.Contains("lời mời") ||
                  n.Title.Contains("Official Jockey") ||
                  n.Message.Contains("Official Jockey")) &&
                (n.Title.Contains("Registration") ||
                 n.Message.Contains("Registration") ||
                 n.Title.Contains("đăng ký") ||
                n.Message.Contains("Registration") ||
                 n.Message.Contains("đăng ký") ||
                 n.Title.Contains("Approved") ||
                 n.Message.Contains("Approved") ||
                 n.Title.Contains("Rejected") ||
                 n.Message.Contains("Rejected") ||
                 n.Title.Contains("Returned") ||
                 n.Message.Contains("Returned"))),
            "Jockeys" => query.Where(n =>
                n.Title.Contains("Invitation") ||
                n.Message.Contains("Invitation") ||
                n.Title.Contains("Jockey") ||
                n.Message.Contains("Jockey") ||
                n.Title.Contains("lời mời") ||
                n.Message.Contains("lời mời") ||
                n.Title.Contains("Official Jockey") ||
                n.Message.Contains("Official Jockey")),
            "Tournaments" => query.Where(n =>
                !(n.Title.Contains("Invitation") ||
                  n.Message.Contains("Invitation") ||
                  n.Title.Contains("Jockey") ||
                  n.Message.Contains("Jockey") ||
                  n.Title.Contains("lời mời") ||
                  n.Message.Contains("lời mời") ||
                  n.Title.Contains("Official Jockey") ||
                  n.Message.Contains("Official Jockey")) &&
                !(n.Title.Contains("Registration") ||
                  n.Message.Contains("Registration") ||
                  n.Title.Contains("đăng ký") ||
                  n.Message.Contains("đăng ký") ||
                  n.Title.Contains("Approved") ||
                  n.Message.Contains("Approved") ||
                  n.Title.Contains("Rejected") ||
                  n.Message.Contains("Rejected") ||
                  n.Title.Contains("Returned") ||
                  n.Message.Contains("Returned")) &&
                (n.Title.Contains("Tournament") ||
                 n.Message.Contains("Tournament") ||
                 n.Title.Contains("Race") ||
                 n.Message.Contains("Race") ||
                 n.Title.Contains("lịch đua") ||
                 n.Message.Contains("lịch đua") ||
                 n.Title.Contains("giải đấu") ||
                 n.Message.Contains("giải đấu") ||
                 n.Title.Contains("Upcoming") ||
                 n.Message.Contains("Upcoming"))),
            _ => query
        };
    }

    private static OwnerNotificationResponse MapNotification(Notification notification)
    {
        return new OwnerNotificationResponse
        {
            NotificationId = notification.NotificationId,
            Title = notification.Title,
            Message = notification.Message,
            Category = ResolveCategory(notification.Title, notification.Message),
            StatusLabel = ResolveStatusLabel(notification.Title, notification.Message),
            IsRead = notification.IsRead,
            CreatedAt = notification.CreatedAt,
            DisplayTime = BuildDisplayTime(notification.CreatedAt),
            RelatedType = notification.RelatedType,
            RelatedId = notification.RelatedId,
            ActionType = notification.ActionType,
            ActionUrl = notification.ActionUrl
        };
    }

    private static OwnerNotificationDetailResponse MapNotificationDetail(Notification notification)
    {
        return new OwnerNotificationDetailResponse
        {
            NotificationId = notification.NotificationId,
            Title = notification.Title,
            Message = notification.Message,
            Category = ResolveCategory(notification.Title, notification.Message),
            StatusLabel = ResolveStatusLabel(notification.Title, notification.Message),
            IsRead = notification.IsRead,
            CreatedAt = notification.CreatedAt,
            DisplayTime = BuildDisplayTime(notification.CreatedAt),
            RelatedType = notification.RelatedType,
            RelatedId = notification.RelatedId,
            ActionType = notification.ActionType,
            ActionUrl = notification.ActionUrl
        };
    }

    private static string ResolveCategory(string title, string message)
    {
        var content = $"{title} {message}";

        if (ContainsAny(content, "Invitation", "Jockey", "lời mời", "Official Jockey"))
        {
            return "Jockeys";
        }

        if (ContainsAny(content, "Registration", "đăng ký", "Approved", "Rejected", "Returned"))
        {
            return "Registrations";
        }

        if (ContainsAny(content, "Tournament", "Race", "lịch đua", "giải đấu", "Upcoming"))
        {
            return "Tournaments";
        }

        return "All";
    }

    private static string? ResolveStatusLabel(string title, string message)
    {
        var content = $"{title} {message}";

        if (ContainsAny(content, "Approved"))
        {
            return RaceRegistrationStatuses.Approved;
        }

        if (ContainsAny(content, "Accepted", "Confirmed", "Official"))
        {
            return "Confirmed";
        }

        if (ContainsAny(content, "Returned"))
        {
            return "Returned";
        }

        if (ContainsAny(content, "Rejected"))
        {
            return "Rejected";
        }

        if (ContainsAny(content, "Pending"))
        {
            return RaceRegistrationStatuses.Pending;
        }

        return null;
    }

    private static bool ContainsAny(string content, params string[] values)
    {
        return values.Any(value =>
            content.Contains(value, StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildDisplayTime(DateTime createdAt)
    {
        var elapsed = DateTime.UtcNow - createdAt;

        if (elapsed <= TimeSpan.Zero || elapsed.TotalMinutes < 1)
        {
            return "Just now";
        }

        if (elapsed.TotalHours < 1)
        {
            var minutes = Math.Max(1, (int)elapsed.TotalMinutes);
            return $"{minutes} minute{(minutes == 1 ? string.Empty : "s")} ago";
        }

        if (elapsed.TotalDays < 1)
        {
            var hours = Math.Max(1, (int)elapsed.TotalHours);
            return $"{hours} hour{(hours == 1 ? string.Empty : "s")} ago";
        }

        var days = Math.Max(1, (int)elapsed.TotalDays);
        return $"{days} day{(days == 1 ? string.Empty : "s")} ago";
    }
}
