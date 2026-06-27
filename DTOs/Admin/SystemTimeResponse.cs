namespace Eliteracingleague.API.DTOs.Admin;

public class SystemTimeResponse
{
    public DateTime RealUtcNow { get; set; }
    public DateTime EffectiveUtcNow { get; set; }
    public string EffectiveLocalNow { get; set; } = string.Empty;
    public string Timezone { get; set; } = string.Empty;
    public bool IsOverridden { get; set; }
    public bool AllowTimeOverride { get; set; }
}
