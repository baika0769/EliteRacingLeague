namespace Eliteracingleague.API.DTOs.Admin
{
    public class AdminDashboardResponse
    {
        public int TotalUsers { get; set; }
        public int TotalHorses { get; set; }
        public int TotalRaces { get; set; }
        public int TotalTournaments { get; set; }
        public int PendingRegistrations { get; set; }
        public int PendingResults { get; set; }
    }
}