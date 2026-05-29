using System;
using System.Collections.Generic;

namespace Eliteracingleague.API.Models;

public partial class JockeyDistanceExperience
{
    public int JockeyDistanceExperienceId { get; set; }

    public int JockeyId { get; set; }

    public int DistanceMeters { get; set; }

    public string SkillLevel { get; set; } = null!;

    public virtual Jockey Jockey { get; set; } = null!;
}
