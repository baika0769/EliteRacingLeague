namespace Eliteracingleague.API.DTOs.Jockey.Notifications;

public class JockeyNotificationDetailResponse
{
    public int NotificationId { get; set; }
    public string Title { get; set; } = null!;
    public string Message { get; set; } = null!;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
    public string DisplayTime { get; set; } = null!;
    public JockeyNotificationRaceDetailResponse? RaceDetail { get; set; }
}
