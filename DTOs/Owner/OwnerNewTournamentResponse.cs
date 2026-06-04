namespace Eliteracingleague.API.DTOs.Owner;

public class OwnerNewTournamentResponse
{
    public int TournamentId { get; set; }
    public string TournamentName { get; set; } = null!;
    public int RaceId { get; set; }
    public string RaceDate { get; set; } = null!;
    public string? Location { get; set; }
}