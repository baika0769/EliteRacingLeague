namespace Eliteracingleague.API.DTOs.Referee;

public class RefereeRaceLifecycleResponse
{
    public int RaceId { get; set; }
    public int TournamentId { get; set; }
    public string TournamentName { get; set; } = null!;
    public string RaceStatus { get; set; } = null!;
    public string TournamentStatus { get; set; } = null!;
    public string CurrentStage { get; set; } = null!;
    public string? NextStage { get; set; }
    public RefereeAllowedActionsResponse AllowedActions { get; set; } = new();
    public RefereeLifecycleCountsResponse Counts { get; set; } = new();
    public string? BlockingReason { get; set; }
}

public class RefereeAllowedActionsResponse
{
    public bool CanInspect { get; set; }
    public bool CanSubmitPreRaceReport { get; set; }
    public bool CanMarkReady { get; set; }
    public bool CanStartRace { get; set; }
    public bool CanFinishRace { get; set; }
    public bool CanEnterResults { get; set; }
    public bool CanConfirmResults { get; set; }
    public bool CanSubmitPostRaceReport { get; set; }
    public bool CanReportViolation { get; set; }
}

public class RefereeLifecycleCountsResponse
{
    public int TotalRegistrations { get; set; }
    public int PassedInspections { get; set; }
    public int FailedInspections { get; set; }
    public int PendingInspections { get; set; }
    public int DraftResults { get; set; }
    public int RefereeConfirmedResults { get; set; }
    public int AdminApprovedResults { get; set; }
}
