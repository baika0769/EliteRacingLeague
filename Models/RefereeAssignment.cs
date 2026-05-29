using System;
using System.Collections.Generic;

namespace Eliteracingleague.API.Models;

public partial class RefereeAssignment
{
    public int RefereeAssignmentId { get; set; }

    public int RaceId { get; set; }

    public int RefereeId { get; set; }

    public int AssignedBy { get; set; }

    public string Status { get; set; } = null!;

    public DateTime AssignedAt { get; set; }

    public virtual User AssignedByNavigation { get; set; } = null!;

    public virtual Race Race { get; set; } = null!;

    public virtual RaceReferee Referee { get; set; } = null!;
}
