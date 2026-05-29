using Eliteracingleague.API.Data;
using Eliteracingleague.API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly EliteRacingLeagueContext _context;
    private readonly PasswordHasher<User> _passwordHasher;

    public AuthController(EliteRacingLeagueContext context)
    {
        _context = context;
        _passwordHasher = new PasswordHasher<User>();
    }

    // POST: /api/auth/register
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var email = request.Email.Trim().ToLower();

        var emailExists = await _context.Users.AnyAsync(u => u.Email == email);

        if (emailExists)
        {
            return BadRequest(new
            {
                message = "Email đã tồn tại."
            });
        }

        var allowedRoles = new[]
        {
            "HorseOwner",
            "Jockey",
            "RaceReferee",
            "Spectator"
        };

        if (!allowedRoles.Contains(request.Role))
        {
            return BadRequest(new
            {
                message = "Role không hợp lệ."
            });
        }

        if (request.Password != request.ConfirmPassword)
        {
            return BadRequest(new
            {
                message = "Mật khẩu xác nhận không khớp."
            });
        }

        var user = new User
        {
            FullName = request.FullName,
            Email = email,
            Phone = request.Phone,
            Role = request.Role,
            Status = "Active",
            EmailVerified = false,
            CreatedAt = DateTime.UtcNow
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Đăng ký thành công. Vui lòng xác thực email.",
            email = user.Email,
            verifyCodeDemo = "123456"
        });
    }

    // POST: /api/auth/verify-email
    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail(VerifyEmailRequest request)
    {
        var email = request.Email.Trim().ToLower();

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user == null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy tài khoản."
            });
        }

        // Demo: mã xác thực cố định
        if (request.Code != "123456")
        {
            return BadRequest(new
            {
                message = "Mã xác thực không đúng."
            });
        }

        user.EmailVerified = true;
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Xác thực email thành công. Bạn có thể đăng nhập."
        });
    }

    // POST: /api/auth/login
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var email = request.Email.Trim().ToLower();

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user == null)
        {
            return Unauthorized(new
            {
                message = "Email hoặc mật khẩu không đúng."
            });
        }

        var result = _passwordHasher.VerifyHashedPassword(
            user,
            user.PasswordHash,
            request.Password
        );

        if (result == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new
            {
                message = "Email hoặc mật khẩu không đúng."
            });
        }

        if (user.Status != "Active")
        {
            return BadRequest(new
            {
                message = "Tài khoản không hoạt động."
            });
        }

        if (!user.EmailVerified)
        {
            return BadRequest(new
            {
                message = "Email chưa được xác thực."
            });
        }

        return Ok(new
        {
            message = "Đăng nhập thành công.",
            user = new
            {
                user.UserId,
                user.FullName,
                user.Email,
                user.Phone,
                user.Role,
                user.Status,
                user.EmailVerified
            }
        });
    }
}

public class RegisterRequest
{
    public string FullName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? Phone { get; set; }
    public string Password { get; set; } = null!;
    public string ConfirmPassword { get; set; } = null!;
    public string Role { get; set; } = null!;
}

public class VerifyEmailRequest
{
    public string Email { get; set; } = null!;
    public string Code { get; set; } = null!;
}

public class LoginRequest
{
    public string Email { get; set; } = null!;
    public string Password { get; set; } = null!;
}