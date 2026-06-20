namespace Eliteracingleague.API.DTOs.Jockey;

public class JockeyInvitationRaceResponse
{
    public int RaceId { get; set; }
    public string RaceName { get; set; } = null!;
    public DateTime RaceDate { get; set; }
    public string? Location { get; set; }
    public int DistanceMeters { get; set; }
    public string? SurfaceType { get; set; }
    public DateTime? JockeySelectionDeadline { get; set; }
}
