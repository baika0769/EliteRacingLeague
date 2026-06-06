namespace Eliteracingleague.API.DTOs.Auth;

public class LoginResponse
{
    public string Message { get; set; } = "Đăng nhập thành công.";
    public string Token { get; set; } = null!;
    public int ExpiresInMinutes { get; set; }
    public string NextStep { get; set; } = null!;
    public LoginUserResponse User { get; set; } = null!;
}

public class LoginUserResponse
{
    public int UserId { get; set; }
    public string FullName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? Phone { get; set; }
    public string Role { get; set; } = null!;
    public string Status { get; set; } = null!;
    public bool EmailVerified { get; set; }
    
}
