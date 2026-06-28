namespace Eliteracingleague.API.DTOs.Admin
{
    public class AdminReportResponse
    {
        public int? ReportId { get; set; }
        public int? ViolationId { get; set; }

        public string Type { get; set; } = string.Empty;

        public int RaceId { get; set; }
        public string? RaceName { get; set; }

        public int? RegistrationId { get; set; }
        public int? HorseId { get; set; }
        public string? HorseName { get; set; }

        public int RefereeId { get; set; }
        public string? RefereeName { get; set; }

        public string? ReportContent { get; set; }

        public string? ViolationType { get; set; }
        public string? Description { get; set; }
        public decimal? PenaltyPoints { get; set; }
        public string? Action { get; set; }

        public DateTime SubmittedAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}