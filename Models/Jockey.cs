using System;
using System.Collections.Generic;

namespace Eliteracingleague.API.Models;

public partial class Jockey
{
    public int JockeyId { get; set; }

    public decimal WeightKg { get; set; }

    public int YearsOfExperience { get; set; }

    public string HealthStatus { get; set; } = null!;

    public string? CertificateNo { get; set; }

    public string? CertificateFileUrl { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<JockeyAvailability> JockeyAvailabilities { get; set; } = new List<JockeyAvailability>();

    public virtual ICollection<JockeyBreedExperience> JockeyBreedExperiences { get; set; } = new List<JockeyBreedExperience>();

    public virtual ICollection<JockeyDistanceExperience> JockeyDistanceExperiences { get; set; } = new List<JockeyDistanceExperience>();

    public virtual ICollection<JockeyInvitation> JockeyInvitations { get; set; } = new List<JockeyInvitation>();

    public virtual User JockeyNavigation { get; set; } = null!;

    public virtual ICollection<JockeyRecommendation> JockeyRecommendations { get; set; } = new List<JockeyRecommendation>();

    public virtual ICollection<PrizeAward> PrizeAwards { get; set; } = new List<PrizeAward>();

    public virtual ICollection<RaceRegistration> RaceRegistrations { get; set; } = new List<RaceRegistration>();
}
