namespace Eliteracingleague.API.DTOs.Jockey.Notifications;

public class JockeyNotificationListQuery
{
    public string? Status { get; set; }
    public DateTime? Date { get; set; }
    public string? Sort { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
