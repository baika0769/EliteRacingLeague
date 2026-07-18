namespace Eliteracingleague.API.DTOs.Public;

public class HomePageResponse
{
    public PublicSeasonResponse? CurrentSeason { get; set; }
    public List<PublicTournamentResponse> UpcomingTournaments { get; set; } = new();
    public PublicLatestResultResponse? LatestResult { get; set; }
}

public class PublicSeasonResponse
{
    public int SeasonId { get; set; }
    public string SeasonName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class PublicTournamentResponse
{
    public int TournamentId { get; set; }
    public string TournamentName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Location { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal? PrizePool { get; set; }
    public string? ImageUrl { get; set; }
    public string Status { get; set; } = string.Empty;
    public int SeasonId { get; set; }
    public string SeasonName { get; set; } = string.Empty;
    public int RegisteredHorseCount { get; set; }
    public PublicRaceResponse? Race { get; set; }
}

public class PublicRaceResponse
{
    public int RaceId { get; set; }
    public string RaceName { get; set; } = string.Empty;
    public DateTime RaceDate { get; set; }
    public int DistanceMeters { get; set; }
    public string? Location { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class PublicLatestResultResponse
{
    public int TournamentId { get; set; }
    public string TournamentName { get; set; } = string.Empty;
    public int RaceId { get; set; }
    public string RaceName { get; set; } = string.Empty;
    public DateTime? PublishedAt { get; set; }
    public List<PublicStandingResponse> Standings { get; set; } = new();
}

public class PublicStandingResponse
{
    public int Position { get; set; }
    public int HorseId { get; set; }
    public string HorseName { get; set; } = string.Empty;
    public string? HorseImageUrl { get; set; }
    public string? JockeyName { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public decimal? FinishTimeSeconds { get; set; }
}
