using System;
using System.Collections.Generic;

namespace Eliteracingleague.API.Models;

public partial class RaceReferee
{
    public int RefereeId { get; set; }

    public string? LicenseNo { get; set; }

    public int? ExperienceYears { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<PreRaceInspection> PreRaceInspections { get; set; } = new List<PreRaceInspection>();

    public virtual ICollection<RaceResult> RaceResults { get; set; } = new List<RaceResult>();

    public virtual ICollection<RaceViolation> RaceViolations { get; set; } = new List<RaceViolation>();

    public virtual User Referee { get; set; } = null!;

    public virtual ICollection<RefereeAssignment> RefereeAssignments { get; set; } = new List<RefereeAssignment>();

    public virtual ICollection<RefereeReport> RefereeReports { get; set; } = new List<RefereeReport>();
}
