using System.ComponentModel.DataAnnotations;

namespace Eliteracingleague.API.DTOs.Auth;

public class VerifyEmailRequest
{
    [Required(ErrorMessage = "Email không được để trống.")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
    public string Email { get; set; } = null!;

    [Required(ErrorMessage = "Mã xác thực không được để trống.")]
    public string Code { get; set; } = null!;
}