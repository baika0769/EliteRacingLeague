using System;
using System.Collections.Generic;

namespace Eliteracingleague.API.Models;

public partial class Tournament
{
    public int TournamentId { get; set; }

    public string TournamentName { get; set; } = null!;

    public string? Description { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public string Location { get; set; } = null!;

    public int MaxHorses { get; set; }

    public int? MinHorseAge { get; set; }

    public int? MaxHorseAge { get; set; }

    public decimal? MinHorseWeightKg { get; set; }

    public decimal? MaxHorseWeightKg { get; set; }

    public decimal? PrizePool { get; set; }

    public string? Rules { get; set; }

    public string Status { get; set; } = null!;

    public int CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual User CreatedByNavigation { get; set; } = null!;

    public virtual Race? Race { get; set; }
}
