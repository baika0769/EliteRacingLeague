namespace Eliteracingleague.API.DTOs.Jockey.Calendar;

public class JockeyCalendarDayResponse
{
    public DateOnly Date { get; set; }
    public int DayNumber { get; set; }
    public string Status { get; set; } = null!;
    public bool IsCurrentMonth { get; set; }
    public List<JockeyCalendarRaceResponse> Races { get; set; } = new();
}
