using System.Security.Claims;
using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Controllers.Admin
{
    [Authorize(Roles = UserRoles.Admin)]
    [ApiController]
    [Route("api/admin/notifications")]
    public class AdminNotificationsController : ControllerBase
    {
        private readonly EliteRacingLeagueContext _context;

        public AdminNotificationsController(EliteRacingLeagueContext context)
        {
            _context = context;
        }

        private int? GetCurrentAdminId()
        {
            var userIdText = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(userIdText, out var userId))
            {
                return null;
            }

            return userId;
        }

        [HttpGet]
        public async Task<IActionResult> GetNotifications()
        {
            var adminId = GetCurrentAdminId();

            if (adminId == null)
            {
                return Unauthorized(new
                {
                    message = "Invalid admin token."
                });
            }

            var notifications = await _context.Notifications
                .AsNoTracking()
                .Where(n => n.UserId == adminId.Value)
                .OrderByDescending(n => n.CreatedAt)
                .Select(n => new AdminNotificationResponse
                {
                    NotificationId = n.NotificationId,
                    Title = n.Title,
                    Message = n.Message,
                    IsRead = n.IsRead,
                    CreatedAt = n.CreatedAt,
                    RelatedType = n.RelatedType,
                    RelatedId = n.RelatedId,
                    ActionType = n.ActionType,
                    ActionUrl = n.ActionUrl
                })
                .ToListAsync();

            return Ok(notifications);
        }

        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            var adminId = GetCurrentAdminId();

            if (adminId == null)
            {
                return Unauthorized(new
                {
                    message = "Invalid admin token."
                });
            }

            var unreadCount = await _context.Notifications
                .AsNoTracking()
                .CountAsync(n => n.UserId == adminId.Value && !n.IsRead);

            return Ok(new AdminNotificationCountResponse
            {
                UnreadCount = unreadCount
            });
        }

        [HttpPut("{id:int}/read")]
        [HttpPatch("{id:int}/read")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var adminId = GetCurrentAdminId();

            if (adminId == null)
            {
                return Unauthorized(new
                {
                    message = "Invalid admin token."
                });
            }

            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n =>
                    n.NotificationId == id &&
                    n.UserId == adminId.Value);

            if (notification == null)
            {
                return NotFound(new AdminNotificationActionResponse
                {
                    Message = "Notification not found.",
                    NotificationId = id
                });
            }

            notification.IsRead = true;

            await _context.SaveChangesAsync();

            return Ok(new AdminNotificationActionResponse
            {
                Message = "Notification marked as read.",
                NotificationId = notification.NotificationId,
                IsRead = notification.IsRead
            });
        }

        [HttpPut("read-all")]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var adminId = GetCurrentAdminId();

            if (adminId == null)
            {
                return Unauthorized(new
                {
                    message = "Invalid admin token."
                });
            }

            var notifications = await _context.Notifications
                .Where(n => n.UserId == adminId.Value && !n.IsRead)
                .ToListAsync();

            foreach (var notification in notifications)
            {
                notification.IsRead = true;
            }

            await _context.SaveChangesAsync();

            return Ok(new AdminNotificationActionResponse
            {
                Message = "All admin notifications marked as read.",
                UpdatedCount = notifications.Count
            });
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteNotification(int id)
        {
            var adminId = GetCurrentAdminId();

            if (adminId == null)
            {
                return Unauthorized(new
                {
                    message = "Invalid admin token."
                });
            }

            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n =>
                    n.NotificationId == id &&
                    n.UserId == adminId.Value);

            if (notification == null)
            {
                return NotFound(new AdminNotificationActionResponse
                {
                    Message = "Notification not found.",
                    NotificationId = id
                });
            }

            _context.Notifications.Remove(notification);

            await _context.SaveChangesAsync();

            return Ok(new AdminNotificationActionResponse
            {
                Message = "Notification deleted successfully.",
                NotificationId = id
            });
        }
    }
}
