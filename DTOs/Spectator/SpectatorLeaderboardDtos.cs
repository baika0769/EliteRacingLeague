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
    public string RegistrationStatus { get; set; } = null!;

    public int HorseId { get; set; }
    public string HorseName { get; set; } = null!;
    public string? ImageUrl { get; set; }
    public string? HorseImageUrl { get; set; }
    public int BreedId { get; set; }
    public string? BreedName { get; set; }
    public int? Age { get; set; }
    public int? HorseAge { get; set; }
    public decimal? HeightCm { get; set; }
    public decimal WeightKg { get; set; }
    public decimal HorseWeightKg { get; set; }
    public string? HealthStatus { get; set; }
    public string? HorseHealthStatus { get; set; }
    public string? AchievementSummary { get; set; }
    public bool IsActive { get; set; }

    public int OwnerId { get; set; }
    public string? OwnerName { get; set; }

    public int? JockeyId { get; set; }
    public string? JockeyName { get; set; }
    public string? JockeyProfileImageUrl { get; set; }

    public string Status { get; set; } = null!;
    public int TournamentId { get; set; }
    public string? TournamentName { get; set; }
    public string? TournamentImageUrl { get; set; }
    public SpectatorHorseResponse Horse { get; set; } = null!;
    public SpectatorOwnerResponse Owner { get; set; } = null!;
    public SpectatorJockeyResponse? Jockey { get; set; }
}

public class SpectatorHorseResponse
{
    public int HorseId { get; set; }
    public string HorseName { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public int BreedId { get; set; }
    public string BreedName { get; set; } = string.Empty;
    public int Age { get; set; }
    public decimal? HeightCm { get; set; }
    public decimal WeightKg { get; set; }
    public string HealthStatus { get; set; } = string.Empty;
    public string? AchievementSummary { get; set; }
    public bool IsActive { get; set; }
}

public class SpectatorOwnerResponse
{
    public int OwnerId { get; set; }
    public string OwnerName { get; set; } = string.Empty;
}

public class SpectatorJockeyResponse
{
    public int JockeyId { get; set; }
    public string JockeyName { get; set; } = string.Empty;
    public string? ProfileImageUrl { get; set; }
    public decimal WeightKg { get; set; }
    public int YearsOfExperience { get; set; }
    public string HealthStatus { get; set; } = string.Empty;
    public string? CertificateNo { get; set; }
    public bool IsActive { get; set; }
}

public class SpectatorRaceRegistrationResponse : TournamentHorseItem
{
}

public class SpectatorRewardSummary
{
    public bool HasActiveSeason { get; set; }
    public int RewardPoints { get; set; }
    public int BettingPoints { get; set; }
    public int BaseOpeningPoints { get; set; }
    public int CarriedBonusPoints { get; set; }
    public int OpeningTotalPoints { get; set; }
    public string? WalletStatus { get; set; }
    public int TotalStakePoints { get; set; }
    public int TotalPayoutPoints { get; set; }
    public int NetPoints { get; set; }
    public int CorrectPredictions { get; set; }
    public decimal PredictionAccuracy { get; set; }
    public int TotalPredictions { get; set; }
}
