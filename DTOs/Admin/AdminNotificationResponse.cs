namespace Eliteracingleague.API.DTOs.Admin
{
    public class AdminNotificationResponse
    {
        public int Id { get; set; }

        public int NotificationId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public string Type { get; set; } = string.Empty;

        public bool IsRead { get; set; }

        public DateTime CreatedAt { get; set; }

        public string Priority { get; set; } = "Normal";

        public string? ActionUrl { get; set; }

        public string? RelatedType { get; set; }

        public int? RelatedId { get; set; }
    }
}