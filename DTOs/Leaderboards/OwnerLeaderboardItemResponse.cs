namespace Eliteracingleague.API.DTOs.Leaderboards;

public class OwnerLeaderboardItemResponse
{
    public int Rank { get; set; }
    public int OwnerId { get; set; }
    public string OwnerName { get; set; } = null!;
    public int TotalRaces { get; set; }
    public int Wins { get; set; }
    public int Top3Finishes { get; set; }
    public decimal WinRate { get; set; }
    public decimal TotalPrize { get; set; }
    public decimal? AverageFinishPosition { get; set; }
    public decimal? BestFinishTimeSeconds { get; set; }
}
