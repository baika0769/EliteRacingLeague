using System;
using System.Collections.Generic;

namespace Eliteracingleague.API.Models;

public partial class PrizeAward
{
    public int PrizeAwardId { get; set; }

    public int RaceId { get; set; }

    public int RegistrationId { get; set; }

    public int OwnerId { get; set; }

    public int? JockeyId { get; set; }

    public int RankPosition { get; set; }

    public decimal PrizeAmount { get; set; }

    public string Status { get; set; } = null!;

    public DateTime? PaidAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Jockey? Jockey { get; set; }

    public virtual HorseOwner Owner { get; set; } = null!;

    public virtual Race Race { get; set; } = null!;

    public virtual RaceRegistration Registration { get; set; } = null!;

    public virtual ICollection<PrizePayout> Payouts { get; set; } = new List<PrizePayout>();
}
