using System.Security.Claims;
using Eliteracingleague.API.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Controllers.Spectator;

[Authorize]
[ApiController]
[Route("api/spectator/notifications")]
public class SpectatorNotificationsController : ControllerBase
{
    private readonly EliteRacingLeagueContext _context;

    public SpectatorNotificationsController(EliteRacingLeagueContext context)
    {
        _context = context;
    }

    private int GetUserId()
        => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    [HttpGet]
    public async Task<IActionResult> GetNotifications()
    {
        var userId = GetUserId();

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
        var userId = GetUserId();

        var count = await _context.Notifications
            .CountAsync(n => n.UserId == userId && !n.IsRead);

        return Ok(new { unreadCount = count });
    }

    [HttpPut("{id}/read")]
    public async Task<IActionResult> MarkNotificationAsRead(int id)
    {
        var userId = GetUserId();

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
}