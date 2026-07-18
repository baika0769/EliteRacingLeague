using System.ComponentModel.DataAnnotations;

namespace Eliteracingleague.API.DTOs.Auth;

public class ForgotPasswordRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = null!;
}

public class ResetPasswordRequest
{
    [Required]
    public string Token { get; set; } = null!;

    [Required, MinLength(8), MaxLength(100)]
    public string NewPassword { get; set; } = null!;

    [Required]
    public string ConfirmPassword { get; set; } = null!;
}
