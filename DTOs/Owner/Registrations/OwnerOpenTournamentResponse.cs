namespace Eliteracingleague.API.DTOs.Owner.Registrations;

public class OwnerOpenTournamentResponse
{
    public int TournamentId { get; set; }
    public string TournamentName { get; set; } = null!;

    public int SeasonId { get; set; }
    public string SeasonName { get; set; } = null!;
    public string SeasonStatus { get; set; } = null!;
    public string RegistrationDeadline { get; set; } = null!;

    public int RaceId { get; set; }
    public string RaceDate { get; set; } = null!;

    public string? Location { get; set; }
    public int DistanceMeters { get; set; }

    public decimal? PrizePool { get; set; }

    public int MaxHorses { get; set; }
    public int RegisteredCount { get; set; }
    public int AvailableSlots { get; set; }

    public bool OwnerAlreadyRegistered { get; set; }

    public string? ImageUrl { get; set; }
}