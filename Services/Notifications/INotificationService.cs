namespace Eliteracingleague.API.Services.Notifications;

public interface INotificationService
{
    Task CreateForUserAsync(
        int userId,
        string title,
        string message,
        string? actionType = null,
        string? actionUrl = null,
        string? relatedType = null,
        int? relatedId = null,
        CancellationToken cancellationToken = default,
        bool preventDuplicates = true);

    Task CreateForRoleAsync(
        string role,
        string title,
        string message,
        string? actionType = null,
        string? actionUrl = null,
        string? relatedType = null,
        int? relatedId = null,
        CancellationToken cancellationToken = default,
        bool preventDuplicates = true);

    Task CreateForAdminsAsync(
        string title,
        string message,
        string? actionType = null,
        string? actionUrl = null,
        string? relatedType = null,
        int? relatedId = null,
        CancellationToken cancellationToken = default,
        bool preventDuplicates = true);
}
