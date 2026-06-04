namespace Eliteracingleague.API.DTOs.Admin
{
    public class AdminReportResponse
    {
        public int ViolationId { get; set; }
        public int RaceId { get; set; }
        public int RegistrationId { get; set; }
        public int RefereeId { get; set; }
        public string ViolationType { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal? PenaltyPoints { get; set; }
        public string Action { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}