using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Eliteracingleague.API.DTOs.Admin;

public class AdminTournamentRequest
{
    [Required]
    [StringLength(200, MinimumLength = 3)]
    public string TournamentName { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [Required]
    [StringLength(255, MinimumLength = 3)]
    public string? Location { get; set; }

    public DateOnly RaceDate { get; set; }

    [Required]
    [RegularExpression(@"^(?:[01]?\d|2[0-3]):[0-5]\d(?::[0-5]\d)?$",
        ErrorMessage = "Race start time must be in HH:mm format.")]
    public string RaceStartTime { get; set; } = string.Empty;

    public DateOnly RegistrationDeadline { get; set; }

    [Range(1, 10000)]
    public int DistanceMeters { get; set; }

    [Range(2, 20)]
    public int MaxHorses { get; set; }

    [Range(typeof(decimal), "0", "1000000000")]
    public decimal PrizePool { get; set; }

    [Range(typeof(decimal), "0.01", "1000000000")]
    public decimal GoldPrize { get; set; }

    [Range(typeof(decimal), "0.01", "1000000000")]
    public decimal SilverPrize { get; set; }

    [Range(typeof(decimal), "0.01", "1000000000")]
    public decimal BronzePrize { get; set; }

    public IFormFile? TournamentImage { get; set; }

    [StringLength(500)]
    public string? ImageUrl { get; set; }

    [StringLength(10000)]
    public string? Rules { get; set; }

    [Range(1, int.MaxValue)]
    public int? RefereeId { get; set; }
}
