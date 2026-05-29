using System;
using System.Collections.Generic;

namespace Eliteracingleague.API.Models;

public partial class JockeyAvailability
{
    public int AvailabilityId { get; set; }

    public int JockeyId { get; set; }

    public DateOnly AvailableDate { get; set; }

    public string Status { get; set; } = null!;

    public virtual Jockey Jockey { get; set; } = null!;
}
