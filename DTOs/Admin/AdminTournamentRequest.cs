using Microsoft.AspNetCore.Http;

namespace Eliteracingleague.API.DTOs.Admin;

public class AdminTournamentRequest
{
    public string TournamentName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Location { get; set; }

    public DateOnly RaceDate { get; set; }
    public string RaceStartTime { get; set; } = string.Empty;
    public DateOnly RegistrationDeadline { get; set; }

    public int DistanceMeters { get; set; }
    public int MaxHorses { get; set; }
    public decimal PrizePool { get; set; }

    public IFormFile? TournamentImage { get; set; }
    public string? ImageUrl { get; set; }
    public string? Rules { get; set; }
}
