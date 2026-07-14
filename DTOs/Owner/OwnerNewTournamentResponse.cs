namespace Eliteracingleague.API.DTOs.Owner;

public class OwnerNewTournamentResponse
{
    public int TournamentId { get; set; }
    public string TournamentName { get; set; } = null!;

    public int SeasonId { get; set; }
    public string SeasonName { get; set; } = null!;
    public string SeasonStatus { get; set; } = null!;
    public string RegistrationDeadline { get; set; } = null!;

    public int RaceId { get; set; }
    public string RaceDate { get; set; } = null!;
    public string? Location { get; set; }
}