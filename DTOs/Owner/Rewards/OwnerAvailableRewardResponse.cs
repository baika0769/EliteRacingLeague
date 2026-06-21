namespace Eliteracingleague.API.DTOs.Owner.Rewards;

public class OwnerAvailableRewardResponse
{
    public int PrizeAwardId { get; set; }

    public string TournamentName { get; set; } = null!;

    public DateTime RaceDate { get; set; }

    public string HorseName { get; set; } = null!;

    public int RankPosition { get; set; }

    public decimal PrizeAmount { get; set; }

    public string Status { get; set; } = null!;

    public bool CanClaim { get; set; }
}
