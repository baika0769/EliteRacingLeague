namespace Eliteracingleague.API.DTOs.Owner.Rewards;

public class OwnerRewardSummaryResponse
{
    public decimal TotalPrizeEarned { get; set; }

    public decimal ClaimedRewards { get; set; }

    public int TournamentWins { get; set; }
}
