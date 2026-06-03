namespace Eliteracingleague.API.DTOs.Owner;

public class OwnerDashboardOverviewResponse
{
    public int TotalHorse { get; set; }
    public int Registrations { get; set; }
    public int PendingInvitations { get; set; }
    public int ApprovedRaces { get; set; }
}