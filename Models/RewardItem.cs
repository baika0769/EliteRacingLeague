namespace Eliteracingleague.API.Models;

public class RewardItem
{
    public int RewardItemId { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public string Sku { get; set; } = null!;
    public int StockQuantity { get; set; }
    public int ReservedQuantity { get; set; }
    public int DeliveredQuantity { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public virtual ICollection<RewardInventoryTransaction> InventoryTransactions { get; set; } = new List<RewardInventoryTransaction>();
    public virtual ICollection<SeasonRewardRule> SeasonRewardRules { get; set; } = new List<SeasonRewardRule>();
    public virtual ICollection<SeasonReward> SeasonRewards { get; set; } = new List<SeasonReward>();
}
