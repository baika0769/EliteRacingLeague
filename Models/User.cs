using System;
using System.Collections.Generic;

namespace Eliteracingleague.API.Models;

public partial class User
{
    public int UserId { get; set; }

    public string FullName { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string? Phone { get; set; }

    public string Role { get; set; } = null!;

    public string Status { get; set; } = null!;

    public bool EmailVerified { get; set; }

    public int BettingPoints { get; set; }

    public int TokenVersion { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public int FailedLoginAttempts { get; set; }

    public DateTime? LockoutEndAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<EmailVerificationOtp> EmailVerificationOtps { get; set; } = new List<EmailVerificationOtp>();

    public virtual HorseOwner? HorseOwner { get; set; }

    public virtual Jockey? Jockey { get; set; }

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual ICollection<RacePrediction> RacePredictions { get; set; } = new List<RacePrediction>();

    public virtual RaceReferee? RaceReferee { get; set; }

    public virtual ICollection<RaceRegistration> RaceRegistrations { get; set; } = new List<RaceRegistration>();

    public virtual ICollection<RaceResult> RaceResults { get; set; } = new List<RaceResult>();

    public virtual ICollection<RefereeAssignment> RefereeAssignments { get; set; } = new List<RefereeAssignment>();

    public virtual ICollection<Tournament> Tournaments { get; set; } = new List<Tournament>();
    public virtual ICollection<SeasonReward> SeasonRewards { get; set; } = new List<SeasonReward>();

    public virtual ICollection<SpectatorSeasonWallet> SpectatorSeasonWallets { get; set; } = new List<SpectatorSeasonWallet>();

    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();

    public virtual ICollection<PasswordResetToken> PasswordResetTokens { get; set; } = new List<PasswordResetToken>();

    public virtual ICollection<RaceResultRevision> RaceResultRevisions { get; set; } = new List<RaceResultRevision>();

    public virtual ICollection<RewardInventoryTransaction> RewardInventoryTransactions { get; set; } = new List<RewardInventoryTransaction>();
}
