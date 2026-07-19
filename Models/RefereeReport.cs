using System;

namespace Eliteracingleague.API.Models;

public partial class RefereeReport
{
    public int ReportId { get; set; }

    public int RaceId { get; set; }

    public int RefereeId { get; set; }

    public string ReportContent { get; set; } = null!;

    public DateTime SubmittedAt { get; set; }

    public string ReportType { get; set; } = null!;

    public string Status { get; set; } = null!;

    public string? ReturnReasonCategory { get; set; }

    public string? ReturnReason { get; set; }

    public int? ReviewedByAdminId { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public int RevisionNumber { get; set; }

    public DateTime? ResubmittedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Race Race { get; set; } = null!;

    public virtual RaceReferee Referee { get; set; } = null!;

    public virtual User? ReviewedByAdmin { get; set; }
}
