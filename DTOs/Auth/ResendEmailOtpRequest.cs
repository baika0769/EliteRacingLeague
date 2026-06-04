using System.ComponentModel.DataAnnotations;

namespace Eliteracingleague.API.DTOs.Auth;

public class ResendEmailOtpRequest
{
    [Required(ErrorMessage = "Email không được để trống.")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
    public string Email { get; set; } = null!;
}