namespace Eliteracingleague.API.DTOs.Jockey.Notifications;

public class JockeyNotificationRaceDetailResponse
{
    public int RaceId { get; set; }
    public string RaceName { get; set; } = null!;
    public DateTime RaceDate { get; set; }
    public string? Location { get; set; }
    public int HorseId { get; set; }
    public string HorseName { get; set; } = null!;
    public int HorseAge { get; set; }
    public string? HorseImageUrl { get; set; }
    public string? HorseHealthStatus { get; set; }
    public string? HealthCertificateImageUrl { get; set; }
    public string OwnerName { get; set; } = null!;
    public string? OwnerMessage { get; set; }
}
