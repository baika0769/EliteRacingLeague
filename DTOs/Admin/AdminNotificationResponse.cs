namespace Eliteracingleague.API.DTOs.Admin
{
    public class AdminNotificationResponse
    {
        public int NotificationId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public bool IsRead { get; set; }

        public DateTime CreatedAt { get; set; }

        public string? RelatedType { get; set; }

        public int? RelatedId { get; set; }

        public string? ActionType { get; set; }

        public string? ActionUrl { get; set; }
    }
}
