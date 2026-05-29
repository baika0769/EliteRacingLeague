using System;
using System.Collections.Generic;

namespace Eliteracingleague.API.Models;

public partial class JockeyRecommendation
{
    public int RecommendationId { get; set; }

    public int RegistrationId { get; set; }

    public int RaceId { get; set; }

    public int HorseId { get; set; }

    public int JockeyId { get; set; }

    public int AvailabilityScore { get; set; }

    public int WeightScore { get; set; }

    public int ExperienceScore { get; set; }

    public int DistanceScore { get; set; }

    public int BreedExperienceScore { get; set; }

    public int TotalScore { get; set; }

    public int RankNo { get; set; }

    public string RecommendationLevel { get; set; } = null!;

    public string OwnerDecision { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual Horse Horse { get; set; } = null!;

    public virtual Jockey Jockey { get; set; } = null!;

    public virtual Race Race { get; set; } = null!;

    public virtual RaceRegistration Registration { get; set; } = null!;
}
