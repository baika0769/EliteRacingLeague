namespace Eliteracingleague.API.DTOs.Jockey;

public class JockeyPendingInvitationResponse
{
    public int InvitationId { get; set; }
    public int RegistrationId { get; set; }
    public int TournamentId { get; set; }
    public string TournamentName { get; set; } = null!;
    public int RaceId { get; set; }
    public string RaceName { get; set; } = null!;
    public DateTime RaceDate { get; set; }
    public string? Location { get; set; }
    public int DistanceMeters { get; set; }
    public string? SurfaceType { get; set; }
    public DateTime? JockeySelectionDeadline { get; set; }
    public int HorseId { get; set; }
    public string HorseName { get; set; } = null!;
    public string? HorseImageUrl { get; set; }
    public string BreedName { get; set; } = null!;
    public int Age { get; set; }
    public string HorseHealthStatus { get; set; } = null!;
    public int OwnerId { get; set; }
    public string OwnerName { get; set; } = null!;
    public string? OwnerMessage { get; set; }
    public decimal? FeeAmount { get; set; }
    public string Status { get; set; } = null!;
    public DateTime SentAt { get; set; }
    public int MatchScore { get; set; }
    public List<string> MatchReasons { get; set; } = new();
}
