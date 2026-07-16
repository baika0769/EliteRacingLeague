using System.ComponentModel.DataAnnotations;

namespace Eliteracingleague.API.DTOs.Admin;

public class UpdateRefereeRequest
{
    [Required]
    [StringLength(150, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(255)]
    public string Email { get; set; } = string.Empty;

    [StringLength(30)]
    public string? Phone { get; set; }

    [StringLength(100, MinimumLength = 3)]
    public string? LicenseNo { get; set; }

    [Range(0, 60)]
    public int? ExperienceYears { get; set; }

    public bool? IsActive { get; set; }
}
