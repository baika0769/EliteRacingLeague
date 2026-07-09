namespace Eliteracingleague.API.Models;

public partial class SeasonReward
{
    public int SeasonRewardId { get; set; }

    public int SeasonId { get; set; }

    public int SpectatorId { get; set; }

    public int RankPosition { get; set; }

    public int FinalPoints { get; set; }

    public string RewardName { get; set; } = null!;

    public string? RewardDescription { get; set; }

    public int BonusPoints { get; set; }

    public bool IsBonusApplied { get; set; }

    public int? AppliedToSeasonId { get; set; }

    public DateTime? AppliedAt { get; set; }

    public string Status { get; set; } = "Awarded";

    public DateTime AwardedAt { get; set; }

    public virtual Season Season { get; set; } = null!;

    public virtual User Spectator { get; set; } = null!;
}