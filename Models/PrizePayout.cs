namespace Eliteracingleague.API.Models;

public class PrizePayout
{
    public int PrizePayoutId { get; set; }
    public int PrizeAwardId { get; set; }
    public int RecipientUserId { get; set; }
    public string RecipientType { get; set; } = null!;
    public decimal Amount { get; set; }
    public string Status { get; set; } = null!;
    public DateTime? ClaimedAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime? RejectedAt { get; set; }
    public string? PaymentReference { get; set; }
    public string? AdminNote { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public virtual PrizeAward PrizeAward { get; set; } = null!;
    public virtual User RecipientUser { get; set; } = null!;
}
