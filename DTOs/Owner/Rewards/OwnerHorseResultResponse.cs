namespace Eliteracingleague.API.DTOs.Owner.Rewards;

public class OwnerHorseResultResponse
{
    public int ResultId { get; set; }

    public int RaceId { get; set; }

    public int RegistrationId { get; set; }

    public int? RankPosition { get; set; }

    public string HorseName { get; set; } = null!;

    public string HorseBreed { get; set; } = null!;

    public string TournamentName { get; set; } = null!;

    public string? JockeyName { get; set; }

    public decimal? FinishTime { get; set; }

    public string Status { get; set; } = null!;
}
