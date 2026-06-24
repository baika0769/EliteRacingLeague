namespace Eliteracingleague.API.DTOs.Owner;

public class CreateOwnerHorseRequest
{
    public int BreedId { get; set; }
    public string HorseName { get; set; } = null!;
    public int Age { get; set; }
    public decimal? HeightCm { get; set; }
    public decimal WeightKg { get; set; }
    public string HealthStatus { get; set; } = null!;
    public string? AchievementSummary { get; set; }
    public string? ImageUrl { get; set; }
    public string? HealthCertificateImageUrl { get; set; }
}
