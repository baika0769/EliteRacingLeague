using System;
using System.Collections.Generic;

namespace Eliteracingleague.API.Models;

public partial class HorseBreed
{
    public int BreedId { get; set; }

    public string BreedName { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<Horse> Horses { get; set; } = new List<Horse>();

    public virtual ICollection<JockeyBreedExperience> JockeyBreedExperiences { get; set; } = new List<JockeyBreedExperience>();
}
