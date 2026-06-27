namespace Eliteracingleague.API.DTOs.Admin;

public class AdvanceSystemTimeRequest
{
    public int Days { get; set; }
    public int Hours { get; set; }
    public int Minutes { get; set; }
    public bool AutoSync { get; set; }
}
