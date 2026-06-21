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

        var notifications = _context.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == ownerId.Value);

        var unread = await notifications.CountAsync(n => !n.IsRead);
        var invitations = await notifications.CountAsync(n =>
            n.Title.Contains("Invitation") ||
            n.Title.Contains("Jockey") ||
            n.Title.Contains("Official Jockey") ||
            n.Title.Contains("lời mời") ||
            n.Message.Contains("Invitation") ||
            n.Message.Contains("Jockey") ||
            n.Message.Contains("lời mời"));

        var activeRegistrationStatuses = new[]
        {
            RaceRegistrationStatuses.Approved,
            RaceRegistrationStatuses.JockeyInvited,
            RaceRegistrationStatuses.ReadyToRace
        };

        var excludedRaceStatuses = new[]
        {
            RaceStatuses.Cancelled,
            RaceStatuses.Completed,
            RaceStatuses.Finished,
            RaceStatuses.ResultPending,
            RaceStatuses.Published
        };

        var upcomingRaces = await _context.RaceRegistrations
            .AsNoTracking()
            .CountAsync(r =>
                r.OwnerId == ownerId.Value &&
                r.Race.RaceDate >= DateTime.UtcNow &&
                activeRegistrationStatuses.Contains(r.Status) &&
                !excludedRaceStatuses.Contains(r.Race.Status));

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
        [FromQuery] int pageSize = 10)
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

        var normalizedCategory = SupportedCategories
            .FirstOrDefault(c => c.Equals(category, StringComparison.OrdinalIgnoreCase));

        if (normalizedCategory == null)
        {
            return BadRequest(new
            {
                message = "Category must be All, Registrations, Jockeys, or Tournaments."
            });
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

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

        var response = new OwnerHorseListResponse
        {
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize)
        };

        return Ok(new
        {
            response.Page,
            response.PageSize,
            response.TotalItems,
            response.TotalPages,
            Items = notifications.Select(MapListItem).ToList()
        });
    }

    [HttpGet("{notificationId:int}")]
    public async Task<IActionResult> GetNotification(int notificationId)
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

        return Ok(new OwnerNotificationDetailResponse
        {
            NotificationId = notification.NotificationId,
            Title = notification.Title,
            Message = notification.Message,
            Category = ResolveCategory(notification),
            StatusLabel = ResolveStatusLabel(notification),
            IsRead = notification.IsRead,
            CreatedAt = notification.CreatedAt,
            RelatedType = null,
            RelatedId = null
        });
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
                n.Title.Contains("Registration") ||
                n.Title.Contains("đăng ký") ||
                n.Message.Contains("Registration") ||
                n.Message.Contains("đăng ký") ||
                n.Message.Contains("approved") ||
                n.Message.Contains("rejected") ||
                n.Message.Contains("returned")),
            "Jockeys" => query.Where(n =>
                n.Title.Contains("Invitation") ||
                n.Title.Contains("Jockey") ||
                n.Title.Contains("lời mời") ||
                n.Message.Contains("Invitation") ||
                n.Message.Contains("Jockey") ||
                n.Message.Contains("lời mời")),
            "Tournaments" => query.Where(n =>
                n.Title.Contains("Tournament") ||
                n.Title.Contains("Race") ||
                n.Title.Contains("giải đấu") ||
                n.Title.Contains("lịch đua") ||
                n.Message.Contains("Tournament") ||
                n.Message.Contains("Race") ||
                n.Message.Contains("giải đấu") ||
                n.Message.Contains("lịch đua")),
            _ => query
        };
    }

    private static OwnerNotificationResponse MapListItem(Notification notification)
    {
        return new OwnerNotificationResponse
        {
            NotificationId = notification.NotificationId,
            Title = notification.Title,
            Message = notification.Message,
            Category = ResolveCategory(notification),
            StatusLabel = ResolveStatusLabel(notification),
            IsRead = notification.IsRead,
            CreatedAt = notification.CreatedAt,
            DisplayTime = BuildDisplayTime(notification.CreatedAt),
            RelatedType = null,
            RelatedId = null
        };
    }

    private static string ResolveCategory(Notification notification)
    {
        var text = $"{notification.Title} {notification.Message}";

        if (ContainsAny(text, "Invitation", "Jockey", "lời mời"))
        {
            return "Jockeys";
        }

        if (ContainsAny(text, "Registration", "đăng ký", "approved", "rejected", "returned"))
        {
            return "Registrations";
        }

        if (ContainsAny(text, "Tournament", "Race", "giải đấu", "lịch đua"))
        {
            return "Tournaments";
        }

        return "All";
    }

    private static string? ResolveStatusLabel(Notification notification)
    {
        var text = $"{notification.Title} {notification.Message}";

        if (ContainsAny(text, "Approved", "Accepted", "Selected"))
        {
            return "Success";
        }

        if (ContainsAny(text, "Rejected", "Cancelled"))
        {
            return "Rejected";
        }

        if (ContainsAny(text, "Returned", "Pending", "Upcoming", "schedule"))
        {
            return "Pending";
        }

        return null;
    }

    private static bool ContainsAny(string value, params string[] terms)
    {
        return terms.Any(term =>
            value.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildDisplayTime(DateTime createdAt)
    {
        var elapsed = DateTime.UtcNow - createdAt;

        if (elapsed.TotalMinutes < 1)
        {
            return "Just now";
        }

        if (elapsed.TotalHours < 1)
        {
            return $"{(int)elapsed.TotalMinutes} minutes ago";
        }

        if (elapsed.TotalDays < 1)
        {
            return $"{(int)elapsed.TotalHours} hours ago";
        }

        if (elapsed.TotalDays < 7)
        {
            return $"{(int)elapsed.TotalDays} days ago";
        }

        return createdAt.ToString("dd/MM/yyyy HH:mm");
    }
}
