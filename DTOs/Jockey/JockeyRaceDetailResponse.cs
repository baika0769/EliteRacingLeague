namespace Eliteracingleague.API.DTOs.Jockey;

public class JockeyRaceDetailResponse
{
    public int RaceRegistrationId { get; set; }
    public int RaceId { get; set; }
    public string RaceName { get; set; } = null!;
    public DateTime RaceDate { get; set; }
    public string? Location { get; set; }
    public int HorseId { get; set; }
    public string HorseName { get; set; } = null!;
    public string OwnerName { get; set; } = null!;
    public string Status { get; set; } = null!;
}
