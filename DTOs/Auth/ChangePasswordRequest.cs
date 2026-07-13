using System.ComponentModel.DataAnnotations;

namespace Eliteracingleague.API.DTOs.Auth;

public class ChangePasswordRequest
{
    [Required(ErrorMessage = "Current password is required.")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "New password is required.")]
    [MinLength(6, ErrorMessage = "New password must have at least 6 characters.")]
    [MaxLength(100, ErrorMessage = "New password cannot exceed 100 characters.")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirm password is required.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}