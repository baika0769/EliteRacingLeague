using System;
using System.Collections.Generic;

namespace Eliteracingleague.API.Models;

public partial class Season
{
    public int SeasonId { get; set; }

    public string SeasonName { get; set; } = null!;

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public string Status { get; set; } = null!;

    public int PointsPerCorrectPrediction { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<Tournament> Tournaments { get; set; } = new List<Tournament>();
    public virtual ICollection<SeasonRewardRule> SeasonRewardRules { get; set; } = new List<SeasonRewardRule>();

    public virtual ICollection<SeasonReward> SeasonRewards { get; set; } = new List<SeasonReward>();

    public virtual ICollection<SpectatorSeasonWallet> SpectatorSeasonWallets { get; set; } = new List<SpectatorSeasonWallet>();
}