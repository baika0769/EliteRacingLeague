using System;
using System.Collections.Generic;

namespace Eliteracingleague.API.Models;

public partial class RaceViolation
{
    public int ViolationId { get; set; }

    public int RaceId { get; set; }

    public int RegistrationId { get; set; }

    public int RefereeId { get; set; }

    public string ViolationType { get; set; } = null!;

    public string? Description { get; set; }

    public string Action { get; set; } = null!;

    public decimal? PenaltyPoints { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Race Race { get; set; } = null!;

    public virtual RaceReferee Referee { get; set; } = null!;

    public virtual RaceRegistration Registration { get; set; } = null!;
}
