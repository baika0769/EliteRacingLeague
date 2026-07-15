namespace Eliteracingleague.API.DTOs.Referee;

public class RefereeRaceSummaryResponse
{
    public int RaceId { get; set; }
    public string RaceName { get; set; } = null!;
    public int TournamentId { get; set; }
    public string TournamentName { get; set; } = null!;
    public DateTime RaceDate { get; set; }
    public int DistanceMeters { get; set; }
    public string? Location { get; set; }
    public string RaceStatus { get; set; } = null!;
    public int MaxHorses { get; set; }
}
