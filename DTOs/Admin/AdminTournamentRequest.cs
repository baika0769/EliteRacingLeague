namespace Eliteracingleague.API.DTOs.Admin;

public class AdminTournamentRequest
{
    public string TournamentName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Location { get; set; }

    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }

    public int MaxHorses { get; set; }
    public decimal PrizePool { get; set; }

    public int? MinHorseAge { get; set; }
    public int? MaxHorseAge { get; set; }

    public decimal? MinHorseWeightKg { get; set; }
    public decimal? MaxHorseWeightKg { get; set; }

    public string? Rules { get; set; }
}