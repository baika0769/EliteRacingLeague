namespace Eliteracingleague.API.DTOs.Owner.Notifications;

public class OwnerNotificationListResponse
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
    public List<OwnerNotificationResponse> Items { get; set; } = new();
}
