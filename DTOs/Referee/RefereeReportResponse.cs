namespace Eliteracingleague.API.DTOs.Referee;

public class RefereeReportResponse
{
    public int ReportId { get; set; }
    public int RaceId { get; set; }
    public string ReportContent { get; set; } = string.Empty;
    public string ReportType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int RevisionNumber { get; set; }
    public string? ReturnReasonCategory { get; set; }
    public string? ReturnReason { get; set; }
    public DateTime SubmittedAt { get; set; }
    public DateTime? ResubmittedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool CanEdit { get; set; }
    public bool IsLocked { get; set; }
}
