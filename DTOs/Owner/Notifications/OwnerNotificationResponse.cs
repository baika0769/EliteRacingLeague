namespace Eliteracingleague.API.DTOs.Owner.Notifications;

public class OwnerNotificationResponse
{
    public int NotificationId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string? StatusLabel { get; set; }

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; }

    public string DisplayTime { get; set; } = string.Empty;

    public string? RelatedType { get; set; }

    public int? RelatedId { get; set; }
}
