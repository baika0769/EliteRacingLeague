using System.ComponentModel.DataAnnotations;

namespace Eliteracingleague.API.DTOs.Admin;

public class CreateRefereeAccountRequest
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

    [Required]
    [StringLength(72, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [Compare(nameof(Password), ErrorMessage = "Confirm password does not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [StringLength(100, MinimumLength = 3)]
    public string? LicenseNo { get; set; }

    [Range(0, 60)]
    public int? ExperienceYears { get; set; }

    [Required]
    [RegularExpression("^(Active|Inactive)$",
        ErrorMessage = "Status must be Active or Inactive.")]
    public string Status { get; set; } = "Active";
}
