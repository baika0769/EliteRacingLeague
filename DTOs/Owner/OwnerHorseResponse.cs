namespace Eliteracingleague.API.DTOs.Owner;

public class OwnerHorseResponse
{
    public int HorseId { get; set; }
    public string HorseName { get; set; } = null!;
    public string BreedName { get; set; } = null!;
    public int Age { get; set; }
    public decimal WeightKg { get; set; }
    public string HealthStatus { get; set; } = null!;
    public string? ImageUrl { get; set; }
}