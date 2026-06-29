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

        private static string GetNotificationType(string? actionType, string? relatedType)
        {
            if (!string.IsNullOrWhiteSpace(actionType))
            {
                return actionType;
            }

            if (!string.IsNullOrWhiteSpace(relatedType))
            {
                return relatedType;
            }

            return "General";
        }

        private static string GetPriority(string type)
        {
            return type switch
            {
                "RaceRegistration" => "High",
                "RaceResultValidation" => "High",
                "RefereeReport" => "Normal",
                "PostRaceReport" => "High",
                _ => "Normal"
            };
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
                .OrderBy(n => n.IsRead)
                .ThenByDescending(n => n.CreatedAt)
                .Select(n => new
                {
                    n.NotificationId,
                    n.Title,
                    n.Message,
                    n.IsRead,
                    n.CreatedAt,
                    n.ActionType,
                    n.ActionUrl,
                    n.RelatedType,
                    n.RelatedId
                })
                .ToListAsync();

            var response = notifications
                .Select(n =>
                {
                    var type = GetNotificationType(n.ActionType, n.RelatedType);

                    return new AdminNotificationResponse
                    {
                        Id = n.NotificationId,
                        NotificationId = n.NotificationId,
                        Title = n.Title,
                        Message = n.Message,
                        Type = type,
                        IsRead = n.IsRead,
                        CreatedAt = n.CreatedAt,
                        Priority = GetPriority(type),
                        ActionUrl = n.ActionUrl,
                        RelatedType = n.RelatedType,
                        RelatedId = n.RelatedId
                    };
                })
                .ToList();

            return Ok(response);
        }

        [HttpPut("{id:int}/read")]
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

            return Ok(new
            {
                unreadCount
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
    }
}