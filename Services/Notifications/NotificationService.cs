using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Services.Notifications;

public class NotificationService : INotificationService
{
    private readonly EliteRacingLeagueContext _context;

    public NotificationService(EliteRacingLeagueContext context)
    {
        _context = context;
    }

    public async Task CreateForUserAsync(
        int userId,
        string title,
        string message,
        string? actionType = null,
        string? actionUrl = null,
        string? relatedType = null,
        int? relatedId = null,
        CancellationToken cancellationToken = default,
        bool preventDuplicates = true)
    {
        if (preventDuplicates &&
            await NotificationExistsAsync(userId, actionType, relatedType, relatedId, cancellationToken))
        {
            return;
        }

        _context.Notifications.Add(new Notification
        {
            UserId = userId,
            Title = title,
            Message = message,
            IsRead = false,
            CreatedAt = DateTime.UtcNow,
            ActionType = actionType,
            ActionUrl = actionUrl,
            RelatedType = relatedType,
            RelatedId = relatedId
        });
    }

    public async Task CreateForRoleAsync(
        string role,
        string title,
        string message,
        string? actionType = null,
        string? actionUrl = null,
        string? relatedType = null,
        int? relatedId = null,
        CancellationToken cancellationToken = default,
        bool preventDuplicates = true)
    {
        var userIds = await _context.Users
            .AsNoTracking()
            .Where(u => u.Role == role && u.Status == UserStatuses.Active)
            .Select(u => u.UserId)
            .ToListAsync(cancellationToken);

        foreach (var userId in userIds)
        {
            await CreateForUserAsync(
                userId,
                title,
                message,
                actionType,
                actionUrl,
                relatedType,
                relatedId,
                cancellationToken,
                preventDuplicates);
        }
    }

    public Task CreateForAdminsAsync(
        string title,
        string message,
        string? actionType = null,
        string? actionUrl = null,
        string? relatedType = null,
        int? relatedId = null,
        CancellationToken cancellationToken = default,
        bool preventDuplicates = true)
    {
        return CreateForRoleAsync(
            UserRoles.Admin,
            title,
            message,
            actionType,
            actionUrl,
            relatedType,
            relatedId,
            cancellationToken,
            preventDuplicates);
    }

    private async Task<bool> NotificationExistsAsync(
        int userId,
        string? actionType,
        string? relatedType,
        int? relatedId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(actionType) || relatedId == null)
        {
            return false;
        }

        var alreadyAdded = _context.ChangeTracker
            .Entries<Notification>()
            .Any(e =>
                e.Entity.UserId == userId &&
                e.Entity.ActionType == actionType &&
                e.Entity.RelatedType == relatedType &&
                e.Entity.RelatedId == relatedId);

        if (alreadyAdded)
        {
            return true;
        }

        var duplicateWindowStart = DateTime.UtcNow.AddMinutes(-5);
        return await _context.Notifications
            .AsNoTracking()
            .AnyAsync(n =>
                n.UserId == userId &&
                n.ActionType == actionType &&
                n.RelatedType == relatedType &&
                n.RelatedId == relatedId &&
                n.CreatedAt >= duplicateWindowStart,
                cancellationToken);
    }
}
