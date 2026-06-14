namespace Eliteracingleague.API.DTOs.Jockey.Notifications;

public class JockeyNotificationListResponse
{
    public List<JockeyNotificationItemResponse> Items { get; set; } = new();
    public int TotalItems { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
