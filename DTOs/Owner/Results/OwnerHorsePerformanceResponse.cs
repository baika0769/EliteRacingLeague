namespace Eliteracingleague.API.DTOs.Owner.Results;

public class OwnerHorsePerformanceResponse
{
    public OwnerHorsePerformanceInfoResponse Horse { get; set; } = null!;

    public OwnerHorseAchievementResponse Achievements { get; set; } = null!;

    public List<OwnerHorseRaceHistoryResponse> RaceHistory { get; set; } = new();
}
