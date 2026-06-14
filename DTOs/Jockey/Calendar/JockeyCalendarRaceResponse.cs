namespace Eliteracingleague.API.DTOs.Jockey.Calendar;

public class JockeyCalendarRaceResponse
{
    public int RaceId { get; set; }
    public string RaceName { get; set; } = null!;
    public DateTime RaceDate { get; set; }
    public string? Location { get; set; }
    public string HorseName { get; set; } = null!;
    public string Status { get; set; } = null!;
}
