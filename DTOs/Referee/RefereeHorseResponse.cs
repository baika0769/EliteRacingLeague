namespace Eliteracingleague.API.DTOs.Referee;

public class RefereeHorseResponse
{
    public int HorseId { get; set; }
    public string HorseName { get; set; } = null!;
    public string? ImageUrl { get; set; }
    public int BreedId { get; set; }
    public string BreedName { get; set; } = null!;
    public string Breed { get; set; } = null!;
    public int Age { get; set; }
    public decimal? HeightCm { get; set; }
    public decimal WeightKg { get; set; }
    public string HealthStatus { get; set; } = null!;
    public string? HealthCertificateImageUrl { get; set; }
    public string? HealthCertificate { get; set; }
    public bool IsActive { get; set; }
    public string? AchievementSummary { get; set; }
}
