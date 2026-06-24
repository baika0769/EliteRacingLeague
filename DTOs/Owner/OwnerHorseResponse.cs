namespace Eliteracingleague.API.DTOs.Owner;

public class OwnerHorseResponse
{
    public int HorseId { get; set; }
    public string HorseName { get; set; } = null!;

    public int BreedId { get; set; }
    public string BreedName { get; set; } = null!;

    public int Age { get; set; }
    public decimal? HeightCm { get; set; }
    public decimal WeightKg { get; set; }

    public string HealthStatus { get; set; } = null!;
    public string? ImageUrl { get; set; }
    public string? HealthCertificateImageUrl { get; set; }

    public bool IsActive { get; set; }
    public string Status { get; set; } = null!;

    public int InRaceCount { get; set; }
}
