namespace Eliteracingleague.API.DTOs.Jockey;

public class JockeyDashboardResponse
{
    public int PendingInvitations { get; set; }
    public int AcceptedRaces { get; set; }
    public int UpcomingRaces { get; set; }
    public int CompletedRaces { get; set; }
    public string ProfileStatus { get; set; } = null!;
    public string HealthStatus { get; set; } = null!;
}
