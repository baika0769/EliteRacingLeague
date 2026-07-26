namespace Eliteracingleague.API.DTOs.Public;

public class PublicPagedResponse<T>
{
    public List<T> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
}

public class PublicResultListItemResponse
{
    public int RaceId { get; set; }
    public string RaceName { get; set; } = string.Empty;
    public int TournamentId { get; set; }
    public string TournamentName { get; set; } = string.Empty;
    public int SeasonId { get; set; }
    public string SeasonName { get; set; } = string.Empty;
    public DateTime RaceDate { get; set; }
    public DateTime? PublishedAt { get; set; }
    public int DistanceMeters { get; set; }
    public string? Location { get; set; }
    public string? TournamentImageUrl { get; set; }
    public bool ReplayAvailable { get; set; }
    public List<PublicStandingResponse> TopThree { get; set; } = new();
}

public class PublicReplayListItemResponse
{
    public int RaceId { get; set; }
    public string RaceName { get; set; } = string.Empty;
    public int TournamentId { get; set; }
    public string TournamentName { get; set; } = string.Empty;
    public int SeasonId { get; set; }
    public string SeasonName { get; set; } = string.Empty;
    public DateTime RaceDate { get; set; }
    public DateTime? PublishedAt { get; set; }
    public int DistanceMeters { get; set; }
    public string? Location { get; set; }
    public string? ThumbnailUrl { get; set; }
    public int RunnerCount { get; set; }
}

public class PublicSpectatorLeaderboardItemResponse
{
    public int Rank { get; set; }
    public int SpectatorId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public int RewardPoints { get; set; }
    public int CorrectPredictions { get; set; }
    public int TotalPredictions { get; set; }
    public decimal AccuracyPercentage { get; set; }
}
