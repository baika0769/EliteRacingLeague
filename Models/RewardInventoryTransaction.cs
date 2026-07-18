namespace Eliteracingleague.API.Models;

public class RewardInventoryTransaction
{
    public long RewardInventoryTransactionId { get; set; }
    public int RewardItemId { get; set; }
    public int QuantityDelta { get; set; }
    public int ReservedDelta { get; set; }
    public int DeliveredDelta { get; set; }
    public string TransactionType { get; set; } = null!;
    public string? ReferenceType { get; set; }
    public int? ReferenceId { get; set; }
    public string IdempotencyKey { get; set; } = null!;
    public string? Note { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public virtual RewardItem RewardItem { get; set; } = null!;
    public virtual User CreatedByUser { get; set; } = null!;
}
