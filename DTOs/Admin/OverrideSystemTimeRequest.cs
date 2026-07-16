using System.ComponentModel.DataAnnotations;

namespace Eliteracingleague.API.DTOs.Admin;

public class OverrideSystemTimeRequest
{
    [Required]
    [StringLength(32)]
    public string NowLocal { get; set; } = string.Empty;

    [StringLength(100)]
    public string Timezone { get; set; } = string.Empty;

    public bool AutoSync { get; set; }
}
