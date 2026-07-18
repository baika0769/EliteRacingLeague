namespace Eliteracingleague.API.Models;

public class SpectatorSeasonWallet
{
    public int SpectatorSeasonWalletId { get; set; }

    public int SeasonId { get; set; }

    public int SpectatorId { get; set; }

    public int OpeningBettingPoints { get; set; }

    public int CurrentBettingPoints { get; set; }

    // Điểm thành tích chỉ tăng từ phần thưởng dự đoán đúng, không lấy điểm mở ví để đổi quà.
    public int SeasonScore { get; set; }

    public int? FinalBettingPoints { get; set; }

    public int? FinalSeasonScore { get; set; }

    public int? FinalRank { get; set; }

    public string Status { get; set; } = null!;

    public DateTime OpenedAt { get; set; }

    public DateTime? FrozenAt { get; set; }

    public DateTime? SettledAt { get; set; }

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public virtual Season Season { get; set; } = null!;

    public virtual User Spectator { get; set; } = null!;

    public virtual ICollection<PointTransaction> PointTransactions { get; set; } = new List<PointTransaction>();
}
