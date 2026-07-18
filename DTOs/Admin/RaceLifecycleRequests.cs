using System.ComponentModel.DataAnnotations;

namespace Eliteracingleague.API.DTOs.Admin;

public class ReopenPublishedRaceRequest
{
    [Required, StringLength(1000, MinimumLength = 10)]
    public string Reason { get; set; } = null!;
}

public class CancelRaceRequest
{
    [Required, StringLength(1000, MinimumLength = 5)]
    public string Reason { get; set; } = null!;
}

public class PostponeRaceRequest
{
    [Required]
    public DateTime NewRaceDate { get; set; }

    [Required, StringLength(1000, MinimumLength = 5)]
    public string Reason { get; set; } = null!;

    public DateTime? NewPredictionDeadline { get; set; }
    public DateTime? NewJockeySelectionDeadline { get; set; }
}

public class CreateRaceRequest
{
    [Required, StringLength(200, MinimumLength = 3)]
    public string RaceName { get; set; } = null!;

    [Required]
    public DateTime RaceDate { get; set; }

    [Range(100, 10000)]
    public int DistanceMeters { get; set; }

    [StringLength(255)]
    public string? Location { get; set; }

    [Range(2, 100)]
    public int MaxHorses { get; set; } = 10;

    public DateTime? JockeySelectionDeadline { get; set; }
    public DateTime? PredictionDeadline { get; set; }
    public int? RefereeId { get; set; }
}

public class UpdateRaceRequest : CreateRaceRequest
{
    [Required]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

public class WithdrawRegistrationRequest
{
    [Required, StringLength(500, MinimumLength = 5)]
    public string Reason { get; set; } = null!;
}

public class FinalizeTournamentStandingsRequest
{
    [Required, StringLength(1000, MinimumLength = 5)]
    public string ConfirmationNote { get; set; } = null!;
}
