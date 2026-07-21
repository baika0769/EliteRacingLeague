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

    Task CreateForRaceSpectatorsAsync(
        int raceId,
        string title,
        string message,
        string? actionType = null,
        string? actionUrl = null,
        string? relatedType = null,
        int? relatedId = null,
        CancellationToken cancellationToken = default,
        bool preventDuplicates = true);

    Task CreateForAssignedRaceRefereesAsync(
        int raceId,
        string title,
        string message,
        string? actionType = null,
        string? actionUrl = null,
        string? relatedType = null,
        int? relatedId = null,
        CancellationToken cancellationToken = default,
        bool preventDuplicates = true);

    Task CreateForTournamentSpectatorsAsync(
        int tournamentId,
        string title,
        string message,
        string? actionType = null,
        string? actionUrl = null,
        string? relatedType = null,
        int? relatedId = null,
        CancellationToken cancellationToken = default,
        bool preventDuplicates = true);

    Task CreateForTournamentRefereesAsync(
        int tournamentId,
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
