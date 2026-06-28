namespace Eliteracingleague.API.DTOs.Referee;

public class CreateInspectionRequest
{
    public int RegistrationId { get; set; }
    public string Status { get; set; } = null!;
    public string? Note { get; set; }
}

public class CreateRaceResultRequest
{
    public int RegistrationId { get; set; }
    public decimal? FinishTimeSeconds { get; set; }
    public int? FinishPosition { get; set; }
    public decimal? Score { get; set; }
    public string? Note { get; set; }
}

public class CreateViolationRequest
{
    public int RegistrationId { get; set; }
    public string ViolationType { get; set; } = null!;
    public string? Description { get; set; }
    public string Action { get; set; } = null!;
    public decimal? PenaltyPoints { get; set; }
}

public class CreateRefereeReportRequest
{
    public string ReportContent { get; set; } = null!;

    public string ReportType { get; set; } = null!;
}