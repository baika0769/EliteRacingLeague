using System.Security.Claims;
using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Controllers.Spectator;

[Authorize(Roles = UserRoles.Spectator)]
[ApiController]
[Route("api/spectator/notifications")]
public class SpectatorNotificationsController : ControllerBase
{
    private readonly EliteRacingLeagueContext _context;

    public SpectatorNotificationsController(EliteRacingLeagueContext context)
    {
        _context = context;
    }

    private bool TryGetUserId(out int userId)
        => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out userId);

    [HttpGet]
    public async Task<IActionResult> GetNotifications()
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Token không hợp lệ hoặc thiếu UserId." });
        }

        var notifications = await _context.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new
            {
                notificationId = n.NotificationId,
                title = n.Title,
                message = n.Message,
                isRead = n.IsRead,
                createdAt = n.CreatedAt
            })
            .ToListAsync();

        return Ok(notifications);
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Token không hợp lệ hoặc thiếu UserId." });
        }

        var count = await _context.Notifications
            .CountAsync(n => n.UserId == userId && !n.IsRead);

        return Ok(new { unreadCount = count });
    }

    [HttpPut("{id}/read")]
    public async Task<IActionResult> MarkNotificationAsRead(int id)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Token không hợp lệ hoặc thiếu UserId." });
        }

        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.NotificationId == id && n.UserId == userId);

        if (notification == null)
            return NotFound("Notification not found.");

        notification.IsRead = true;
        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Notification marked as read",
            notificationId = notification.NotificationId
        });
    }
    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Token không hợp lệ hoặc thiếu UserId." });
        }

        var notifications = await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync();

        foreach (var notification in notifications)
        {
            notification.IsRead = true;
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "All notifications marked as read",
            updatedCount = notifications.Count
        });
    }
}
