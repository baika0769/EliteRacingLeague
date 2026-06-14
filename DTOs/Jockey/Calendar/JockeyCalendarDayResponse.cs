namespace Eliteracingleague.API.DTOs.Jockey.Calendar;

public class JockeyCalendarDayResponse
{
    public DateOnly Date { get; set; }
    public bool IsAvailable { get; set; }
    public string? AvailabilityStatus { get; set; }
    public bool HasRace { get; set; }
    public List<JockeyCalendarItemResponse> Items { get; set; } = new();
}
