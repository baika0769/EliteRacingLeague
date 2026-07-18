using System;
using System.Collections.Generic;

namespace Eliteracingleague.API.Models;

public partial class HorseOwner
{
    public int OwnerId { get; set; }

    public string? Address { get; set; }

    public DateTime CreatedAt { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<Horse> Horses { get; set; } = new List<Horse>();

    public virtual ICollection<JockeyInvitation> JockeyInvitations { get; set; } = new List<JockeyInvitation>();

    public virtual User Owner { get; set; } = null!;

    public virtual ICollection<PrizeAward> PrizeAwards { get; set; } = new List<PrizeAward>();

    public virtual ICollection<RaceRegistration> RaceRegistrations { get; set; } = new List<RaceRegistration>();

    public virtual ICollection<TournamentStanding> TournamentStandings { get; set; } = new List<TournamentStanding>();
}
