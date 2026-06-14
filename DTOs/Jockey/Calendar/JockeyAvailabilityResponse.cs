namespace Eliteracingleague.API.DTOs.Jockey.Calendar;

public class JockeyAvailabilityResponse
{
    public DateOnly Date { get; set; }
    public string Status { get; set; } = null!;
}
