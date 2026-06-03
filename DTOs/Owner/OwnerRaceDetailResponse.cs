namespace Eliteracingleague.API.DTOs.Owner;

public class OwnerRaceDetailResponse
{
    public int RaceId { get; set; }
    public int TournamentId { get; set; }
    public string TournamentName { get; set; } = null!;
    public string RaceName { get; set; } = null!;
    public string RaceDate { get; set; } = null!;
    public string? Location { get; set; }
    public int Distance { get; set; }
    public string Status { get; set; } = null!;
}