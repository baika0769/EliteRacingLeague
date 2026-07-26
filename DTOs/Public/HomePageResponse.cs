namespace Eliteracingleague.API.DTOs.Public;

public class HomePageResponse
{
    public PublicServerClockResponse ServerClock { get; set; } = new();
    public PublicSeasonResponse? CurrentSeason { get; set; }
    public PublicFeaturedRaceResponse? FeaturedRace { get; set; }
    public List<PublicTournamentResponse> UpcomingTournaments { get; set; } = new();
    public PublicLatestResultResponse? LatestResult { get; set; }
    public PublicStatisticsResponse Statistics { get; set; } = new();
}

public class PublicServerClockResponse
{
    public DateTime UtcNow { get; set; }
    public DateTime LocalNow { get; set; }
    public string TimeZoneId { get; set; } = "Asia/Ho_Chi_Minh";
    public bool IsOverridden { get; set; }
}

public class PublicSeasonResponse
{
    public int SeasonId { get; set; }
    public string SeasonName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class PublicFeaturedRaceResponse
{
    public int TournamentId { get; set; }
    public string TournamentName { get; set; } = string.Empty;
    public string? TournamentImageUrl { get; set; }
    public decimal? PrizePool { get; set; }
    public int SeasonId { get; set; }
    public string SeasonName { get; set; } = string.Empty;

    public int RaceId { get; set; }
    public string RaceName { get; set; } = string.Empty;
    public DateTime RaceDate { get; set; }
    public DateTime? PredictionDeadline { get; set; }
    public DateOnly RegistrationDeadline { get; set; }
    public string Location { get; set; } = string.Empty;
    public int DistanceMeters { get; set; }

    public int MaxHorses { get; set; }
    public int ReservedHorseCount { get; set; }
    public int ConfirmedHorseCount { get; set; }
    public int ReadyHorseCount { get; set; }
    public int AvailableSlots { get; set; }

    public string TournamentStatus { get; set; } = string.Empty;
    public string RaceStatus { get; set; } = string.Empty;
    public string RegistrationState { get; set; } = string.Empty;
    public string PredictionState { get; set; } = string.Empty;
    public bool ReplayAvailable { get; set; }
}

public class PublicTournamentResponse
{
    public int TournamentId { get; set; }
    public string TournamentName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Location { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public DateOnly RegistrationDeadline { get; set; }
    public decimal? PrizePool { get; set; }
    public string? ImageUrl { get; set; }
    public string Status { get; set; } = string.Empty;
    public int SeasonId { get; set; }
    public string SeasonName { get; set; } = string.Empty;

    // Kept for backward compatibility with the current FE.
    public int RegisteredHorseCount { get; set; }

    public int MaxHorses { get; set; }
    public int ReservedHorseCount { get; set; }
    public int ConfirmedHorseCount { get; set; }
    public int ReadyHorseCount { get; set; }
    public int AvailableSlots { get; set; }
    public string RegistrationState { get; set; } = string.Empty;
    public string PredictionState { get; set; } = string.Empty;
    public bool ReplayAvailable { get; set; }
    public PublicRaceResponse? Race { get; set; }
}

public class PublicRaceResponse
{
    public int RaceId { get; set; }
    public string RaceName { get; set; } = string.Empty;
    public DateTime RaceDate { get; set; }
    public DateTime? PredictionDeadline { get; set; }
    public DateOnly RegistrationDeadline { get; set; }
    public int DistanceMeters { get; set; }
    public string? Location { get; set; }
    public string Status { get; set; } = string.Empty;
    public int MaxHorses { get; set; }
    public int ReservedHorseCount { get; set; }
    public int ConfirmedHorseCount { get; set; }
    public int ReadyHorseCount { get; set; }
    public int AvailableSlots { get; set; }
    public string RegistrationState { get; set; } = string.Empty;
    public string PredictionState { get; set; } = string.Empty;
    public bool ReplayAvailable { get; set; }
}

public class PublicLatestResultResponse
{
    public int TournamentId { get; set; }
    public string TournamentName { get; set; } = string.Empty;
    public int RaceId { get; set; }
    public string RaceName { get; set; } = string.Empty;
    public DateTime? PublishedAt { get; set; }
    public bool ReplayAvailable { get; set; }
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

public class PublicStatisticsResponse
{
    public int ActiveSeasons { get; set; }
    public int ActiveTournaments { get; set; }
    public int ActiveHorses { get; set; }
    public int ActiveJockeys { get; set; }
    public int PublishedRaces { get; set; }
    public int TotalPredictions { get; set; }
    public int TotalSpectators { get; set; }
}
