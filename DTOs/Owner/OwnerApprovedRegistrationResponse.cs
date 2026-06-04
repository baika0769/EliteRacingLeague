namespace Eliteracingleague.API.DTOs.Owner;

public class OwnerApprovedRegistrationResponse
{
    public int RegistrationId { get; set; }
    public int RaceId { get; set; }
    public string TournamentName { get; set; } = null!;
    public string HorseName { get; set; } = null!;
    public string? JockeyName { get; set; }
    public string RaceDate { get; set; } = null!;
    public string Status { get; set; } = null!;
}