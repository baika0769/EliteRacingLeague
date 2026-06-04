namespace Eliteracingleague.API.DTOs.Admin
{
    public class AdminRaceResultResponse
    {
        public int ResultId { get; set; }
        public int RaceId { get; set; }
        public int RegistrationId { get; set; }
        public decimal? FinishTimeSeconds { get; set; }
        public int? FinishPosition { get; set; }
        public decimal? Score { get; set; }
        public string Status { get; set; } = string.Empty;
        public int EnteredByRefereeId { get; set; }
        public int? AdminConfirmedBy { get; set; }
        public DateTime? PublishedAt { get; set; }
        public string? Note { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}