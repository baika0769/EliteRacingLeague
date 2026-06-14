namespace Eliteracingleague.API.DTOs.Jockey.Calendar;

public class JockeyCalendarResponse
{
    public string Month { get; set; } = null!;
    public int AvailableDays { get; set; }
    public int RacingDays { get; set; }
    public List<JockeyCalendarDayResponse> Days { get; set; } = new();
    public List<JockeyNextRaceResponse> NextRaces { get; set; } = new();
}
