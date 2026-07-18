namespace Eliteracingleague.API.DTOs.Admin;

public class AdminDashboardResponse
{
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int TotalSpectators { get; set; }
    public int TotalOwners { get; set; }
    public int TotalJockeys { get; set; }
    public int TotalReferees { get; set; }
    public int TotalHorses { get; set; }
    public int TotalSeasons { get; set; }
    public int ActiveSeasons { get; set; }
    public int TotalRaces { get; set; }
    public int UpcomingRaces { get; set; }
    public int OngoingRaces { get; set; }
    public int TotalTournaments { get; set; }
    public int ActiveTournaments { get; set; }
    public int PendingRegistrations { get; set; }
    public int PendingResults { get; set; }
    public int PendingSeasonRewards { get; set; }
    public int PendingPrizeClaims { get; set; }
    public int TotalPredictions { get; set; }
    public long TotalStakePoints { get; set; }
    public long TotalPayoutPoints { get; set; }
    public int UnreadAdminNotifications { get; set; }
    public List<DashboardRaceItem> NextRaces { get; set; } = new();
    public List<DashboardActivityItem> RecentActivities { get; set; } = new();
}

public class DashboardRaceItem
{
    public int RaceId { get; set; }
    public string RaceName { get; set; } = null!;
    public string TournamentName { get; set; } = null!;
    public DateTime RaceDate { get; set; }
    public string Status { get; set; } = null!;
    public int RegisteredCount { get; set; }
}

public class DashboardActivityItem
{
    public long AuditLogId { get; set; }
    public string Action { get; set; } = null!;
    public string EntityType { get; set; } = null!;
    public string? EntityId { get; set; }
    public string? UserName { get; set; }
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; }
}
