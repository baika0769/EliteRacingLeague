namespace Eliteracingleague.API.DTOs.Owner.Results;

public class OwnerHorseRaceHistoryResponse
{
    public int RaceId { get; set; }

    public int ResultId { get; set; }

    public string TournamentName { get; set; } = null!;

    public DateTime RaceDate { get; set; }

    public string? Track { get; set; }

    public int DistanceMeters { get; set; }

    public string? JockeyName { get; set; }

    public int? Position { get; set; }

    public decimal? FinishTime { get; set; }

    public string Status { get; set; } = null!;
}
