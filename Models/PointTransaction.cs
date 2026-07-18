namespace Eliteracingleague.API.Models;

public class PointTransaction
{
    public int PointTransactionId { get; set; }

    public int SpectatorSeasonWalletId { get; set; }

    public string TransactionType { get; set; } = null!;

    // Số thay đổi của số dư cược. Âm là trừ, dương là cộng.
    public int Amount { get; set; }

    // Số thay đổi của điểm thành tích season.
    public int ScoreDelta { get; set; }

    public int BalanceBefore { get; set; }

    public int BalanceAfter { get; set; }

    public string? ReferenceType { get; set; }

    public int? ReferenceId { get; set; }

    // Chống cộng/trừ lặp khi endpoint bị gọi lại.
    public string IdempotencyKey { get; set; } = null!;

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual SpectatorSeasonWallet SpectatorSeasonWallet { get; set; } = null!;
}
