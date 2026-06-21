namespace Eliteracingleague.API.DTOs.Owner.Notifications;

public class OwnerNotificationMarkReadResponse
{
    public string Message { get; set; } = string.Empty;
    public int NotificationId { get; set; }
    public bool IsRead { get; set; }
}
