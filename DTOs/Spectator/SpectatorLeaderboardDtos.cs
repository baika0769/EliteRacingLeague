namespace Eliteracingleague.API.DTOs.Spectator;

public class CurrentSeasonResponse
{
    public int SeasonId { get; set; }
    public string SeasonName { get; set; } = null!;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int DaysLeft { get; set; }
    public int TotalDays { get; set; }
    public int TotalPredictors { get; set; }
    public int TotalPredictions { get; set; }
}

public class PredictorLeaderboardItem
{
    public int Rank { get; set; }
    public int SpectatorId { get; set; }
    public string SpectatorName { get; set; } = null!;
    public int Points { get; set; }
    public int CorrectPredictions { get; set; }
    public decimal Accuracy { get; set; }
    public int TotalPredictions { get; set; }
}

public class HorseLeaderboardItem
{
    public int Rank { get; set; }
    public int HorseId { get; set; }
    public string HorseName { get; set; } = null!;
    public string? OwnerName { get; set; }
    public string? ImageUrl { get; set; }
    public string? BreedName { get; set; }
    public int Wins { get; set; }
    public int TotalRaces { get; set; }
    public decimal WinRate { get; set; }
}

public class TournamentHorseItem
{
    public int RegistrationId { get; set; }
    public int HorseId { get; set; }
    public string HorseName { get; set; } = null!;
    public string? ImageUrl { get; set; }
    public string? BreedName { get; set; }
    public int? Age { get; set; }
    public string? HealthStatus { get; set; }
    public string RegistrationStatus { get; set; } = null!;
    public string? OwnerName { get; set; }
    public string? JockeyName { get; set; }
}

public class SpectatorRewardSummary
{
    public int RewardPoints { get; set; }
    public int CorrectPredictions { get; set; }
    public decimal PredictionAccuracy { get; set; }
    public int TotalPredictions { get; set; }
}

public class SpectatorRaceReplayResponse
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
    public List<SpectatorRaceReplayRunnerResponse> Runners { get; set; } = new();
}

public class SpectatorRaceReplayRunnerResponse
{
    public int ResultId { get; set; }
    public int RegistrationId { get; set; }
    public int HorseId { get; set; }
    public string HorseName { get; set; } = string.Empty;
    public string? HorseImageUrl { get; set; }
    public int OwnerId { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public int? JockeyId { get; set; }
    public string? JockeyName { get; set; }
    public int Rank { get; set; }
    public decimal FinishTimeSeconds { get; set; }
    public int FinishTimeMs { get; set; }
    public int Lane { get; set; }
    public string Color { get; set; } = string.Empty;
}
