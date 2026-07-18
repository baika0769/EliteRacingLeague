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

    public int? RewardItemId { get; set; }

    public int Quantity { get; set; } = 1;

    public bool InventoryReserved { get; set; }

    public bool IsBonusApplied { get; set; }

    public int? AppliedToSeasonId { get; set; }

    public DateTime? AppliedAt { get; set; }

    public string Status { get; set; } = "Eligible";

    public DateTime AwardedAt { get; set; }

    public DateTime? ClaimDeadline { get; set; }

    public DateTime? ClaimedAt { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public DateTime? PreparingAt { get; set; }

    public DateTime? DeliveredAt { get; set; }

    public DateTime? RejectedAt { get; set; }

    public string? ReceiverName { get; set; }

    public string? ReceiverPhone { get; set; }

    public string? DeliveryAddress { get; set; }

    public string? AdminNote { get; set; }

    public virtual Season Season { get; set; } = null!;

    public virtual User Spectator { get; set; } = null!;

    public virtual RewardItem? RewardItem { get; set; }
}