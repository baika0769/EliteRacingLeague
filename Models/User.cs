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
}
