using Eliteracingleague.API.Services.Email;
using Microsoft.AspNetCore.Authorization;
using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Auth;
using Eliteracingleague.API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Security.Cryptography;


namespace Eliteracingleague.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly EliteRacingLeagueContext _context;
    private readonly IConfiguration _configuration;
    private readonly IEmailService _emailService;
    private readonly IWebHostEnvironment _environment;
    private readonly PasswordHasher<User> _passwordHasher;
    private readonly PasswordHasher<EmailVerificationOtp> _otpHasher;

    private const int OtpExpireMinutes = 10;
    private const int MaxOtpFailedAttempts = 5;
    private const int MaxOtpResendPerWindow = 3;
    private const int OtpResendWindowMinutes = 10;



    public AuthController(
     EliteRacingLeagueContext context,
     IConfiguration configuration,
     IEmailService emailService,
     IWebHostEnvironment environment)
    {
        _context = context;
        _configuration = configuration;
        _emailService = emailService;
        _environment = environment;
        _passwordHasher = new PasswordHasher<User>();
        _otpHasher = new PasswordHasher<EmailVerificationOtp>();
    }


    // POST: /api/auth/register
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var email = request.Email.Trim().ToLower();
        var role = request.Role.Trim();

        var emailExists = await _context.Users.AnyAsync(u => u.Email == email);

        if (emailExists)
        {
            return BadRequest(new
            {
                message = "Email đã tồn tại."
            });
        }

        if (!UserRoles.IsRegisterable(role))
        {
            return BadRequest(new
            {
                message = "Không hợp lệ hoặc không được phép tự đăng ký."
            });
        }

        if (request.Password != request.ConfirmPassword)
        {
            return BadRequest(new
            {
                message = "Mật khẩu xác nhận không khớp."
            });
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var user = new User
            {
                FullName = request.FullName.Trim(),
                Email = email,
                Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
                Role = role,
                Status = UserStatuses.Active,
                EmailVerified = false,
                CreatedAt = DateTime.UtcNow
            };

            user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            CreateProfileForRole(user);
            var otpCode = await CreateEmailVerificationOtpAsync(user.UserId);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            try
            {
                await SendVerificationOtpEmailAsync(user.Email, user.FullName, otpCode);
            }
            catch (Exception ex)
            {
                if (_environment.IsDevelopment())
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, new
                    {
                        message = "Đăng ký thành công nhưng gửi email OTP thất bại. Vui lòng kiểm tra SMTP hoặc dùng API gửi lại OTP.",
                        email = user.Email,
                        error = ex.Message,
                        otpDemo = otpCode
                    });
                }

                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    message = "Đăng ký thành công nhưng gửi email OTP thất bại. Vui lòng thử gửi lại OTP.",
                    email = user.Email
                });
            }

            if (_environment.IsDevelopment())
            {
                return Ok(new
                {
                    message = "Đăng ký thành công. OTP đã được gửi đến email. Mã OTP demo chỉ hiển thị trong môi trường Development.",
                    email = user.Email,
                    role = user.Role,
                    userId = user.UserId,
                    expiresInMinutes = OtpExpireMinutes,
                    otpDemo = otpCode
                });
            }

            return Ok(new
            {
                message = "Đăng ký thành công. OTP đã được gửi đến email.",
                email = user.Email,
                role = user.Role,
                userId = user.UserId,
                expiresInMinutes = OtpExpireMinutes
            });
        }
        catch
        {
            await transaction.RollbackAsync();

            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Đăng ký thất bại. Không thể tạo user và profile tương ứng."
            });
        }
    }

    
    // POST: /api/auth/verify-email
    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail(VerifyEmailRequest request)
    {
        var email = request.Email.Trim().ToLower();
        var inputOtp = request.Code.Trim();

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user == null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy tài khoản."
            });
        }

        if (user.EmailVerified)
        {
            return BadRequest(new
            {
                message = "Email này đã được xác thực, không cần xác thực lại."
            });
        }

        var otp = await _context.EmailVerificationOtps
            .Where(o => o.UserId == user.UserId)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();

        if (otp == null)
        {
            return BadRequest(new
            {
                message = "Không tìm thấy OTP. Vui lòng gửi lại OTP."
            });
        }

        if (otp.IsUsed)
        {
            return BadRequest(new
            {
                message = "OTP đã được sử dụng hoặc đã bị vô hiệu hóa. Vui lòng gửi lại OTP."
            });
        }

        if (otp.ExpiresAt <= DateTime.UtcNow)
        {
            otp.IsUsed = true;
            otp.UsedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return BadRequest(new
            {
                message = "OTP đã hết hạn. Vui lòng gửi lại OTP."
            });
        }

        var verificationResult = _otpHasher.VerifyHashedPassword(
            otp,
            otp.OtpHash,
            inputOtp
        );

        if (verificationResult == PasswordVerificationResult.Failed)
        {
            otp.FailedAttempts++;

            if (otp.FailedAttempts >= MaxOtpFailedAttempts)
            {
                otp.IsUsed = true;
                otp.UsedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            return BadRequest(new
            {
                message = "OTP không đúng.",
                failedAttempts = otp.FailedAttempts,
                maxFailedAttempts = MaxOtpFailedAttempts
            });
        }

        otp.IsUsed = true;
        otp.UsedAt = DateTime.UtcNow;

        user.EmailVerified = true;
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Xác thực email thành công. Bạn có thể đăng nhập."
        });
    }


    // POST: /api/auth/resend-verification-otp
    [HttpPost("resend-verification-otp")]
    public async Task<IActionResult> ResendVerificationOtp(ResendEmailOtpRequest request)
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

        if (user.EmailVerified)
        {
            return BadRequest(new
            {
                message = "Email này đã được xác thực, không thể gửi lại OTP."
            });
        }

        var resendWindowStart = DateTime.UtcNow.AddMinutes(-OtpResendWindowMinutes);

        var resendCount = await _context.EmailVerificationOtps
            .CountAsync(o => o.UserId == user.UserId && o.CreatedAt >= resendWindowStart);

        if (resendCount >= MaxOtpResendPerWindow)
        {
            return BadRequest(new
            {
                message = $"Bạn đã gửi lại OTP quá nhiều lần. Vui lòng thử lại sau {OtpResendWindowMinutes} phút.",
                maxResendPerWindow = MaxOtpResendPerWindow,
                windowMinutes = OtpResendWindowMinutes
            });
        }
        var otpCode = await CreateEmailVerificationOtpAsync(user.UserId);
        await _context.SaveChangesAsync();

        try
        {
            await SendVerificationOtpEmailAsync(user.Email, user.FullName, otpCode);
        }
        catch (Exception ex)
        {
            if (_environment.IsDevelopment())
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    message = "Tạo OTP mới thành công nhưng gửi email thất bại. Vui lòng kiểm tra SMTP.",
                    email = user.Email,
                    error = ex.Message,
                    otpDemo = otpCode
                });
            }

            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Tạo OTP mới thành công nhưng gửi email thất bại. Vui lòng thử lại sau.",
                email = user.Email
            });
        }

        if (_environment.IsDevelopment())
        {
            return Ok(new
            {
                message = "Đã gửi lại OTP xác thực email. Mã OTP demo chỉ hiển thị trong môi trường Development.",
                email = user.Email,
                expiresInMinutes = OtpExpireMinutes,
                otpDemo = otpCode
            });
        }

        return Ok(new
        {
            message = "Đã gửi lại OTP xác thực email.",
            email = user.Email,
            expiresInMinutes = OtpExpireMinutes
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

        if (user.Status != UserStatuses.Active)
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

        var token = GenerateJwtToken(user);
        var expiresInMinutes = int.Parse(_configuration["Jwt:ExpireMinutes"]!);

        var response = new LoginResponse
        {
            Message = "Đăng nhập thành công.",
            Token = token,
            ExpiresInMinutes = expiresInMinutes,
            User = new LoginUserResponse
            {
                UserId = user.UserId,
                FullName = user.FullName,
                Email = user.Email,
                Phone = user.Phone,
                Role = user.Role,
                Status = user.Status,
                EmailVerified = user.EmailVerified
            }
        };

        return Ok(response);
    }


    // GET: /api/auth/me
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var userIdText = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!int.TryParse(userIdText, out var userId))
        {
            return Unauthorized(new
            {
                message = "Token không hợp lệ hoặc thiếu UserId."
            });
        }

        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == userId);

        if (user == null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy tài khoản."
            });
        }

        return Ok(new
        {
            userId = user.UserId,
            fullName = user.FullName,
            email = user.Email,
            phone = user.Phone,
            role = user.Role,
            status = user.Status,
            emailVerified = user.EmailVerified
        });
    }

    private void CreateProfileForRole(User user)
    {
        switch (user.Role)
        {
            case UserRoles.HorseOwner:
                _context.HorseOwners.Add(new HorseOwner
                {
                    OwnerId = user.UserId,
                    Address = null,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });
                break;

            case UserRoles.Jockey:
                _context.Jockeys.Add(new Jockey
                {
                    JockeyId = user.UserId,
                    WeightKg = 50m,
                    YearsOfExperience = 0,
                    HealthStatus = HorseHealthStatuses.Healthy,
                    CertificateNo = null,
                    CertificateFileUrl = null,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });
                break;

            case UserRoles.RaceReferee:
                _context.RaceReferees.Add(new RaceReferee
                {
                    RefereeId = user.UserId,
                    LicenseNo = null,
                    ExperienceYears = 0,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });
                break;
        }
    }


    private async Task<string> CreateEmailVerificationOtpAsync(int userId)
    {
        await InvalidateUnusedOtpsAsync(userId);

        var otpCode = GenerateSixDigitOtp();

        var otp = new EmailVerificationOtp
        {
            UserId = userId,
            OtpHash = string.Empty,
            ExpiresAt = DateTime.UtcNow.AddMinutes(OtpExpireMinutes),
            IsUsed = false,
            FailedAttempts = 0,
            CreatedAt = DateTime.UtcNow,
            UsedAt = null
        };

        otp.OtpHash = _otpHasher.HashPassword(otp, otpCode);

        _context.EmailVerificationOtps.Add(otp);

        return otpCode;
    }

    private async Task InvalidateUnusedOtpsAsync(int userId)
    {
        var oldOtps = await _context.EmailVerificationOtps
            .Where(o => o.UserId == userId && !o.IsUsed)
            .ToListAsync();

        foreach (var oldOtp in oldOtps)
        {
            oldOtp.IsUsed = true;
            oldOtp.UsedAt = DateTime.UtcNow;
        }
    }

    private static string GenerateSixDigitOtp()
    {
        return RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
    }

    private async Task SendVerificationOtpEmailAsync(string email, string fullName, string otpCode)
    {
        var subject = "Mã xác thực email - Elite Racing League";

        var safeFullName = System.Net.WebUtility.HtmlEncode(fullName);
        var safeOtpCode = System.Net.WebUtility.HtmlEncode(otpCode);

        var htmlBody = $@"
        <div style='font-family: Arial, sans-serif; line-height: 1.6; color: #222;'>
            <h2>Elite Racing League</h2>
            <p>Xin chào <strong>{safeFullName}</strong>,</p>
            <p>Mã xác thực email của bạn là:</p>

            <div style='font-size: 28px; font-weight: bold; letter-spacing: 6px; margin: 16px 0;'>
                {safeOtpCode}
            </div>

            <p>Mã này có hiệu lực trong <strong>{OtpExpireMinutes} phút</strong>.</p>
            <p>Nếu bạn không thực hiện đăng ký, vui lòng bỏ qua email này.</p>
        </div>
    ";

        await _emailService.SendEmailAsync(email, subject, htmlBody);
    }



    private string GenerateJwtToken(User user)
    {
        var jwtKey = _configuration["Jwt:Key"];
        var issuer = _configuration["Jwt:Issuer"];
        var audience = _configuration["Jwt:Audience"];
        var expireMinutes = int.Parse(_configuration["Jwt:ExpireMinutes"]!);

        var claims = new List<Claim>
    {
        new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
        new Claim(ClaimTypes.Name, user.FullName),
        new Claim(ClaimTypes.Email, user.Email),
        new Claim(ClaimTypes.Role, user.Role)
    };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expireMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}