using System.ComponentModel.DataAnnotations;

namespace Eliteracingleague.API.DTOs.Admin;

public class AdvanceSystemTimeRequest
{
    [Range(0, 365)]
    public int Days { get; set; }

    [Range(0, 23)]
    public int Hours { get; set; }

    [Range(0, 59)]
    public int Minutes { get; set; }

    public bool AutoSync { get; set; }
}
