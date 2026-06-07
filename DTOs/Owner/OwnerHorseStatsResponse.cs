namespace Eliteracingleague.API.DTOs.Owner;

public class OwnerHorseStatsResponse
{
    public int TotalHorses { get; set; }
    public int ActiveHorses { get; set; }
    public int InjuredHorses { get; set; }
    public int InRaces { get; set; }
}