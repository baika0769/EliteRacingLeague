using System;
using System.Collections.Generic;

namespace Eliteracingleague.API.Models;

public partial class Race
{
    public int RaceId { get; set; }

    public int TournamentId { get; set; }

    public string RaceName { get; set; } = null!;

    public DateTime RaceDate { get; set; }

    public int DistanceMeters { get; set; }

    public string? Location { get; set; }

    public int MaxHorses { get; set; }

    public DateTime? JockeySelectionDeadline { get; set; }

    public DateTime? PredictionDeadline { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? OriginalRaceDate { get; set; }

    public DateTime? PostponedAt { get; set; }

    public string? PostponementReason { get; set; }

    public DateTime? CancelledAt { get; set; }

    public string? CancellationReason { get; set; }

    public int LifecycleVersion { get; set; }

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public virtual ICollection<JockeyRecommendation> JockeyRecommendations { get; set; } = new List<JockeyRecommendation>();

    public virtual ICollection<PreRaceInspection> PreRaceInspections { get; set; } = new List<PreRaceInspection>();

    public virtual ICollection<PrizeAward> PrizeAwards { get; set; } = new List<PrizeAward>();

    public virtual ICollection<PrizeRule> PrizeRules { get; set; } = new List<PrizeRule>();

    public virtual ICollection<RacePrediction> RacePredictions { get; set; } = new List<RacePrediction>();

    public virtual ICollection<RaceRegistration> RaceRegistrations { get; set; } = new List<RaceRegistration>();

    public virtual ICollection<RaceResult> RaceResults { get; set; } = new List<RaceResult>();

    public virtual ICollection<RaceViolation> RaceViolations { get; set; } = new List<RaceViolation>();

    public virtual ICollection<RefereeAssignment> RefereeAssignments { get; set; } = new List<RefereeAssignment>();

    public virtual ICollection<RefereeReport> RefereeReports { get; set; } = new List<RefereeReport>();

    public virtual Tournament Tournament { get; set; } = null!;

    public virtual ICollection<RaceResultRevision> RaceResultRevisions { get; set; } = new List<RaceResultRevision>();
}
