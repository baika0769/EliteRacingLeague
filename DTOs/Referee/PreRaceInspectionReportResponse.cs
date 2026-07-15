namespace Eliteracingleague.API.DTOs.Referee;

public class PreRaceInspectionReportResponse
{
    public RefereeRaceSummaryResponse Race { get; set; } = null!;
    public PreRaceInspectionReportCountsResponse Counts { get; set; } = new();
    public List<RefereeRaceRegistrationResponse> Items { get; set; } = new();
}

public class PreRaceInspectionReportCountsResponse
{
    public int All { get; set; }
    public int Flagged { get; set; }
    public int Pending { get; set; }
}
