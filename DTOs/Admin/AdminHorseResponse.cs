namespace Eliteracingleague.API.DTOs.Admin
{
    public class AdminHorseResponse
    {
        public int HorseId { get; set; }
        public string HorseName { get; set; } = string.Empty;
        public int Age { get; set; }
        public decimal? HeightCm { get; set; }
        public decimal WeightKg { get; set; }
        public string HealthStatus { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public int OwnerId { get; set; }
        public int BreedId { get; set; }
        public string? HealthCertificateImageUrl { get; set; }
        public string? AchievementSummary { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
