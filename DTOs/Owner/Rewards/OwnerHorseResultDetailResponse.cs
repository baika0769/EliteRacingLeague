namespace Eliteracingleague.API.DTOs.Owner.Rewards;

public class OwnerHorseResultDetailResponse
{
    public int ResultId { get; set; }

    public int RaceId { get; set; }

    public int RegistrationId { get; set; }

    public string TournamentName { get; set; } = null!;

    public string RaceName { get; set; } = null!;

    public DateTime RaceDate { get; set; }

    public string HorseName { get; set; } = null!;

    public string HorseBreed { get; set; } = null!;

    public string? JockeyName { get; set; }

    public int? RankPosition { get; set; }

    public decimal? FinishTime { get; set; }

    public decimal? Score { get; set; }

    public string ResultStatus { get; set; } = null!;

    public decimal? PrizeAmount { get; set; }

    public string? RewardStatus { get; set; }

    public DateTime? PublishedAt { get; set; }
}
