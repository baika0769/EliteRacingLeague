namespace Eliteracingleague.API.DTOs.Referee;

public class RefereeInspectionResponse
{
    public int? InspectionId { get; set; }
    public string Status { get; set; } = null!;
    public string? Note { get; set; }
    public DateTime? InspectedAt { get; set; }
    public int? InspectedByRefereeId { get; set; }
}
