namespace Eliteracingleague.API.DTOs.Admin
{
    public class AdminPredictionResponse
    {
        public int PredictionId { get; set; }
        public int RaceId { get; set; }
        public string RaceName { get; set; } = string.Empty;
        public string TournamentName { get; set; } = string.Empty;

        public int SpectatorId { get; set; }
        public string SpectatorName { get; set; } = string.Empty;

        public int PredictedRegistrationId { get; set; }
        public string PredictedHorseName { get; set; } = string.Empty;

        public int? ActualWinnerRegistrationId { get; set; }
        public string? ActualWinnerHorseName { get; set; }

        public string Status { get; set; } = string.Empty;
        public bool? IsCorrect { get; set; }
        public int PointsAwarded { get; set; }
        public decimal? RewardAmount { get; set; }
        public string? RewardStatus { get; set; }

        public DateTime PredictedAt { get; set; }
        public DateTime? LockedAt { get; set; }
        public DateTime? EvaluatedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}