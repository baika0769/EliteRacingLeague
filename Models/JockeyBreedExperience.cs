using System;
using System.Collections.Generic;

namespace Eliteracingleague.API.Models;

public partial class JockeyBreedExperience
{
    public int JockeyBreedExperienceId { get; set; }

    public int JockeyId { get; set; }

    public int BreedId { get; set; }

    public string ExperienceLevel { get; set; } = null!;

    public virtual HorseBreed Breed { get; set; } = null!;

    public virtual Jockey Jockey { get; set; } = null!;
}
