namespace Eliteracingleague.API.DTOs.Spectator;

public class SpectatorRaceDetailResponse
{
    public int RaceId { get; set; }
    public string RaceName { get; set; } = string.Empty;
    public DateTime RaceDate { get; set; }
    public int DistanceMeters { get; set; }
    public string? Location { get; set; }
    public string Status { get; set; } = string.Empty;
    public int TournamentId { get; set; }
    public string TournamentName { get; set; } = string.Empty;
    public string? TournamentImageUrl { get; set; }
    public SpectatorTournamentSummaryResponse Tournament { get; set; } = null!;
}

public class SpectatorTournamentSummaryResponse
{
    public int TournamentId { get; set; }
    public string TournamentName { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
}
