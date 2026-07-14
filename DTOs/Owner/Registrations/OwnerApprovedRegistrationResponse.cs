namespace Eliteracingleague.API.DTOs.Owner.Registrations;

public class OwnerApprovedRegistrationResponse
{
    public int RegistrationId { get; set; }
    public int RaceId { get; set; }

    public string TournamentName { get; set; } = null!;

    public int SeasonId { get; set; }
    public string SeasonName { get; set; } = null!;
    public string SeasonStatus { get; set; } = null!;
    public string RegistrationDeadline { get; set; } = null!;

    public string HorseName { get; set; } = null!;
    public string? JockeyName { get; set; }

    public string RaceDate { get; set; } = null!;
    public string Status { get; set; } = null!;
}