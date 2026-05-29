using System;
using System.Collections.Generic;

namespace Eliteracingleague.API.Models;

public partial class RacePrediction
{
    public int PredictionId { get; set; }

    public int RaceId { get; set; }

    public int SpectatorId { get; set; }

    public int PredictedRegistrationId { get; set; }

    public int? ActualWinnerRegistrationId { get; set; }

    public string Status { get; set; } = null!;

    public bool? IsCorrect { get; set; }

    public int PointsAwarded { get; set; }

    public decimal? RewardAmount { get; set; }

    public string? RewardStatus { get; set; }

    public DateTime PredictedAt { get; set; }

    public DateTime? LockedAt { get; set; }

    public DateTime? EvaluatedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual RaceRegistration? ActualWinnerRegistration { get; set; }

    public virtual RaceRegistration PredictedRegistration { get; set; } = null!;

    public virtual Race Race { get; set; } = null!;

    public virtual User Spectator { get; set; } = null!;
}
