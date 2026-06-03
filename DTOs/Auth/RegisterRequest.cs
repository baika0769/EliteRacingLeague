using System.ComponentModel.DataAnnotations;

namespace Eliteracingleague.API.DTOs.Auth;

public class RegisterRequest
{
    [Required(ErrorMessage = "Họ tên không được để trống.")]
    public string FullName { get; set; } = null!;

    [Required(ErrorMessage = "Email không được để trống.")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
    public string Email { get; set; } = null!;

    public string? Phone { get; set; }

    [Required(ErrorMessage = "Mật khẩu không được để trống.")]
    [MinLength(6, ErrorMessage = "Mật khẩu phải có ít nhất 6 ký tự.")]
    public string Password { get; set; } = null!;

    [Required(ErrorMessage = "Xác nhận mật khẩu không được để trống.")]
    public string ConfirmPassword { get; set; } = null!;

    [Required(ErrorMessage = "Role không được để trống.")]
    public string Role { get; set; } = null!;
}