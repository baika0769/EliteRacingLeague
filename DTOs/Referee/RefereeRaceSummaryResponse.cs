namespace Eliteracingleague.API.DTOs.Referee;

public class RefereeRaceSummaryResponse
{
    public int RaceId { get; set; }
    public string RaceName { get; set; } = null!;
    public int TournamentId { get; set; }
    public string TournamentName { get; set; } = null!;
    public string? TournamentImageUrl { get; set; }
    public string? ImageUrl { get; set; }
    public DateTime RaceDate { get; set; }
    public int DistanceMeters { get; set; }
    public string? Location { get; set; }
    public string RaceStatus { get; set; } = null!;
    public int MaxHorses { get; set; }
}
