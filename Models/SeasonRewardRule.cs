namespace Eliteracingleague.API.Models;

public partial class SeasonRewardRule
{
    public int SeasonRewardRuleId { get; set; }

    public int SeasonId { get; set; }

    public int RankPosition { get; set; }

    public string RewardName { get; set; } = null!;

    public string? RewardDescription { get; set; }

    public int BonusPoints { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Season Season { get; set; } = null!;
}