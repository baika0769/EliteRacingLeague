namespace Eliteracingleague.API.DTOs.Owner.Registrations;

public class OwnerEligibleHorseResponse
{
    public int HorseId { get; set; }
    public string HorseName { get; set; } = null!;
    public string BreedName { get; set; } = null!;

    public int Age { get; set; }
    public decimal? HeightCm { get; set; }
    public decimal WeightKg { get; set; }

    public string HealthStatus { get; set; } = null!;

    public bool IsEligible { get; set; }
    public string? IneligibleReason { get; set; }
}