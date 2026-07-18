using System;
using System.Collections.Generic;

namespace Eliteracingleague.API.Models;

public partial class RaceRegistration
{
    public int RegistrationId { get; set; }

    public int RaceId { get; set; }

    public int HorseId { get; set; }

    public int OwnerId { get; set; }

    public int? JockeyId { get; set; }

    public string Status { get; set; } = null!;

    public DateTime SubmittedAt { get; set; }

    public int? ReviewedBy { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public DateTime? JockeyConfirmedAt { get; set; }

    public string? AdminNote { get; set; }

    public string? WithdrawalReason { get; set; }

    public DateTime? WithdrawnAt { get; set; }

    public int? WithdrawnByUserId { get; set; }

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public virtual Horse Horse { get; set; } = null!;

    public virtual Jockey? Jockey { get; set; }

    public virtual ICollection<JockeyInvitation> JockeyInvitations { get; set; } = new List<JockeyInvitation>();

    public virtual ICollection<JockeyRecommendation> JockeyRecommendations { get; set; } = new List<JockeyRecommendation>();

    public virtual HorseOwner Owner { get; set; } = null!;

    public virtual ICollection<PreRaceInspection> PreRaceInspections { get; set; } = new List<PreRaceInspection>();

    public virtual ICollection<PrizeAward> PrizeAwards { get; set; } = new List<PrizeAward>();

    public virtual Race Race { get; set; } = null!;

    public virtual ICollection<RacePrediction> RacePredictionActualWinnerRegistrations { get; set; } = new List<RacePrediction>();

    public virtual ICollection<RacePrediction> RacePredictionPredictedRegistrations { get; set; } = new List<RacePrediction>();

    public virtual RaceResult? RaceResult { get; set; }

    public virtual ICollection<RaceViolation> RaceViolations { get; set; } = new List<RaceViolation>();

    public virtual User? ReviewedByNavigation { get; set; }

    public virtual User? WithdrawnByUser { get; set; }

    public virtual ICollection<RaceResultRevision> RaceResultRevisions { get; set; } = new List<RaceResultRevision>();
}
