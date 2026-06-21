namespace Eliteracingleague.API.DTOs.Owner.Results;

public class OwnerHorsePerformanceInfoResponse
{
    public int HorseId { get; set; }

    public string HorseName { get; set; } = null!;

    public string BreedName { get; set; } = null!;

    public string? ImageUrl { get; set; }

    public int Age { get; set; }

    public decimal WeightKg { get; set; }

    public string OwnerName { get; set; } = null!;

    public string? AssignedJockeyName { get; set; }
}
