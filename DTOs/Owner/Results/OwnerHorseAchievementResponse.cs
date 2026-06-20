namespace Eliteracingleague.API.DTOs.Owner.Results;

public class OwnerHorseAchievementResponse
{
    public int ChampionTitles { get; set; }

    public decimal? BestTime { get; set; }

    public int CurrentWinStreak { get; set; }

    public string? Award { get; set; }
}
