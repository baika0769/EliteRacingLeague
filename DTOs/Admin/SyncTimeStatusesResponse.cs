namespace Eliteracingleague.API.DTOs.Admin;

public class SyncTimeStatusesResponse
{
    public string Message { get; set; } = string.Empty;
    public DateTime EffectiveUtcNow { get; set; }
    public int ExpiredInvitations { get; set; }
    public int UpdatedRaces { get; set; }
    public int UpdatedTournaments { get; set; }
}
