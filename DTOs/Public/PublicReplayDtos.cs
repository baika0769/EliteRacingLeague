namespace Eliteracingleague.API.DTOs.Public;

public class PublicRaceReplayResponse
{
    public int RaceId { get; set; }
    public int TournamentId { get; set; }
    public string RaceName { get; set; } = string.Empty;
    public string TournamentName { get; set; } = string.Empty;
    public int DistanceMeters { get; set; }
    public string RaceStatus { get; set; } = string.Empty;
    public string TournamentStatus { get; set; } = string.Empty;
    public int Seed { get; set; }
    public int TotalDurationMs { get; set; }
    public DateTime? OfficialAt { get; set; }
    public List<PublicRaceReplayRunnerResponse> Runners { get; set; } = new();
}

public class PublicRaceReplayRunnerResponse
{
    public int RegistrationId { get; set; }
    public int HorseId { get; set; }
    public string HorseName { get; set; } = string.Empty;
    public string? HorseImageUrl { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public int? JockeyId { get; set; }
    public string? JockeyName { get; set; }
    public int? Rank { get; set; }
    public decimal? FinishTimeSeconds { get; set; }
    public int? FinishTimeMs { get; set; }
    public string OutcomeStatus { get; set; } = string.Empty;
    public int Lane { get; set; }
    public string Color { get; set; } = string.Empty;
}
