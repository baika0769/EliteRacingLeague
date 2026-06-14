namespace Eliteracingleague.API.DTOs.Jockey.Calendar;

public class UpdateJockeyAvailabilityItemRequest
{
    public DateOnly Date { get; set; }
    public string Status { get; set; } = null!;
}
