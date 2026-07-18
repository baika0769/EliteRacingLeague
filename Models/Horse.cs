using System;
using System.Collections.Generic;

namespace Eliteracingleague.API.Models;

public partial class Horse
{
    public int HorseId { get; set; }

    public int OwnerId { get; set; }

    public int BreedId { get; set; }

    public string HorseName { get; set; } = null!;

    public int Age { get; set; }

    public decimal? HeightCm { get; set; }

    public decimal WeightKg { get; set; }

    public string HealthStatus { get; set; } = null!;

    public string? AchievementSummary { get; set; }

    public string? ImageUrl { get; set; }

    public string? HealthCertificateImageUrl { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual HorseBreed Breed { get; set; } = null!;

    public virtual ICollection<JockeyRecommendation> JockeyRecommendations { get; set; } = new List<JockeyRecommendation>();

    public virtual HorseOwner Owner { get; set; } = null!;

    public virtual ICollection<RaceRegistration> RaceRegistrations { get; set; } = new List<RaceRegistration>();

    public virtual ICollection<TournamentStanding> TournamentStandings { get; set; } = new List<TournamentStanding>();
}
