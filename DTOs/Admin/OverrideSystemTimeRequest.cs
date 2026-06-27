namespace Eliteracingleague.API.DTOs.Admin;

public class OverrideSystemTimeRequest
{
    public string NowLocal { get; set; } = string.Empty;
    public string Timezone { get; set; } = string.Empty;
    public bool AutoSync { get; set; }
}
