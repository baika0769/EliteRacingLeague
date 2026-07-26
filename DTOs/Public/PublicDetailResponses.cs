namespace Eliteracingleague.API.DTOs.Public;

public class PublicTournamentDetailResponse
{
    public int TournamentId { get; set; }
    public string TournamentName { get; set; } = null!;
    public string? Description { get; set; }
    public string Location { get; set; } = null!;
    public string Status { get; set; } = null!;
    public int SeasonId { get; set; }
    public string SeasonName { get; set; } = null!;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public DateOnly RegistrationDeadline { get; set; }
    public decimal? PrizePool { get; set; }
    public string? ImageUrl { get; set; }
    public List<PublicRaceSummaryResponse> Races { get; set; } = new();
    public List<PublicTournamentStandingResponse> Standings { get; set; } = new();
}

public class PublicRaceSummaryResponse
{
    public int RaceId { get; set; }
    public string RaceName { get; set; } = null!;
    public DateTime RaceDate { get; set; }
    public DateTime? PredictionDeadline { get; set; }
    public DateOnly RegistrationDeadline { get; set; }
    public int DistanceMeters { get; set; }
    public string? Location { get; set; }
    public string Status { get; set; } = null!;

    // Kept for compatibility with the current FE.
    public int RegisteredCount { get; set; }

    public int MaxHorses { get; set; }
    public int ReservedCount { get; set; }
    public int ConfirmedCount { get; set; }
    public int ReadyCount { get; set; }
    public int AvailableSlots { get; set; }
    public string RegistrationState { get; set; } = string.Empty;
    public string PredictionState { get; set; } = string.Empty;
    public bool ReplayAvailable { get; set; }
}

public class PublicRaceDetailResponse : PublicRaceSummaryResponse
{
    public int TournamentId { get; set; }
    public string TournamentName { get; set; } = null!;
    public int SeasonId { get; set; }
    public string SeasonName { get; set; } = null!;
    public List<PublicParticipantResponse> Participants { get; set; } = new();
    public List<PublicRaceResultResponse> Results { get; set; } = new();
}

public class PublicParticipantResponse
{
    public int RegistrationId { get; set; }
    public int HorseId { get; set; }
    public string HorseName { get; set; } = null!;
    public string? HorseImageUrl { get; set; }
    public string OwnerName { get; set; } = null!;
    public string? JockeyName { get; set; }
    public string RegistrationStatus { get; set; } = null!;
}

public class PublicRaceResultResponse
{
    public int RegistrationId { get; set; }
    public int HorseId { get; set; }
    public string HorseName { get; set; } = null!;
    public string? HorseImageUrl { get; set; }
    public string OwnerName { get; set; } = null!;
    public string? JockeyName { get; set; }
    public int? FinishPosition { get; set; }
    public decimal? FinishTimeSeconds { get; set; }
    public string OutcomeStatus { get; set; } = null!;
}

public class PublicTournamentStandingResponse
{
    public int FinalRank { get; set; }
    public int HorseId { get; set; }
    public string HorseName { get; set; } = null!;
    public string? HorseImageUrl { get; set; }
    public string OwnerName { get; set; } = null!;
    public string? JockeyName { get; set; }
    public int TotalPoints { get; set; }
    public int Wins { get; set; }
    public int CompletedRaces { get; set; }
}
