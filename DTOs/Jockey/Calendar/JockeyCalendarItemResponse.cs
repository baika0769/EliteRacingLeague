namespace Eliteracingleague.API.DTOs.Jockey.Calendar;

public class JockeyCalendarItemResponse
{
    public string Type { get; set; } = "Race";
    public int RaceId { get; set; }
    public string RaceName { get; set; } = null!;
    public DateTime RaceDate { get; set; }
    public string? Location { get; set; }
    public int HorseId { get; set; }
    public string HorseName { get; set; } = null!;
    public int OwnerId { get; set; }
    public string OwnerName { get; set; } = null!;
    public string RegistrationStatus { get; set; } = null!;
}
