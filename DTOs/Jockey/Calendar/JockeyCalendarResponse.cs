namespace Eliteracingleague.API.DTOs.Jockey.Calendar;

public class JockeyCalendarResponse
{
    public int Month { get; set; }
    public int Year { get; set; }
    public string ProfileStatus { get; set; } = null!;
    public bool IsActive { get; set; }
    public List<JockeyCalendarDayResponse> Days { get; set; } = new();
}
