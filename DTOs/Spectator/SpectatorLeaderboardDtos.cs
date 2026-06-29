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