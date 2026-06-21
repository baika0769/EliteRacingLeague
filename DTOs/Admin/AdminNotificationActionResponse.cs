namespace Eliteracingleague.API.DTOs.Admin
{
    public class AdminNotificationActionResponse
    {
        public string Message { get; set; } = string.Empty;

        public int? NotificationId { get; set; }

        public bool? IsRead { get; set; }

        public int? UpdatedCount { get; set; }
    }
}