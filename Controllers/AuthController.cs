using Eliteracingleague.API.Services.Email;
using Microsoft.AspNetCore.Authorization;
using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Auth;
using Eliteracingleague.API.Models;
using Eliteracingleague.API.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
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
    private readonly SpectatorWalletService _spectatorWalletService;
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
     IWebHostEnvironment environment,
     SpectatorWalletService spectatorWalletService)
    {
        _context = context;
        _configuration = configuration;
        _emailService = emailService;
        _environment = environment;
        _spectatorWalletService = spectatorWalletService;
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
                Status = UserStatuses.Pending,
                EmailVerified = false,
                BettingPoints = role == UserRoles.Spectator ? SpectatorBettingRules.InitialBettingPoints : 0,
                CreatedAt = DateTime.UtcNow
            };

            user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            if (role == UserRoles.Spectator)
            {
                var activeSeasonId = await _context.Seasons
                    .Where(item => item.Status == SeasonStatuses.Active)
                    .OrderByDescending(item => item.StartDate)
                    .Select(item => (int?)item.SeasonId)
                    .FirstOrDefaultAsync();

                if (activeSeasonId.HasValue)
                {
                    await _spectatorWalletService.GetOrCreateWalletAsync(
                        activeSeasonId.Value,
                        user,
                        SpectatorBettingRules.InitialBettingPoints,
                        user.CreatedAt);
                }
            }

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
        catch (Exception ex)
        {
            await transaction.RollbackAsync();

            if (_environment.IsDevelopment())
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    message = "Đăng ký thất bại. Không thể tạo user và profile tương ứng.",
                    error = ex.Message,
                    innerError = ex.InnerException?.Message
                });
            }

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

        if (user.Role == UserRoles.Spectator)
        {
            user.Status = UserStatuses.Active;
        }
        else if (user.Role == UserRoles.HorseOwner || user.Role == UserRoles.Jockey)
        {
            user.Status = UserStatuses.Pending;
        }
        else
        {
            user.Status = UserStatuses.Pending;
        }

        user.TokenVersion++;
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var nextStep = await GetNextStepAsync(user);

        return Ok(new
        {
            message = "Xác thực email thành công.",
            role = user.Role,
            status = user.Status,
            nextStep
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
    [EnableRateLimiting("LoginRateLimit")]
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

        var now = DateTime.UtcNow;
        if (user.LockoutEndAt.HasValue && user.LockoutEndAt.Value > now)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new
            {
                message = "Tài khoản tạm khóa do đăng nhập sai nhiều lần.",
                lockoutEndAt = user.LockoutEndAt
            });
        }

        var result = _passwordHasher.VerifyHashedPassword(
            user,
            user.PasswordHash,
            request.Password
        );

        if (result == PasswordVerificationResult.Failed)
        {
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= 5)
            {
                user.LockoutEndAt = now.AddMinutes(15);
                user.FailedLoginAttempts = 0;
            }
            await _context.SaveChangesAsync();
            return Unauthorized(new
            {
                message = "Email hoặc mật khẩu không đúng."
            });
        }

        user.FailedLoginAttempts = 0;
        user.LockoutEndAt = null;
        user.LastLoginAt = now;
        user.UpdatedAt = now;
        await _context.SaveChangesAsync();

        if (!user.EmailVerified)
        {
            return BadRequest(new
            {
                message = "Email chưa được xác thực.",
                nextStep = AuthNextSteps.VerifyEmail
            });
        }

        if (user.Status == UserStatuses.Inactive)
        {
            return BadRequest(new
            {
                message = "Tài khoản đang bị vô hiệu hóa.",
                nextStep = AuthNextSteps.ContactSupport
            });
        }

        if (user.Status == UserStatuses.Banned)
        {
            return BadRequest(new
            {
                message = "Tài khoản đã bị khóa.",
                nextStep = AuthNextSteps.AccountBlocked
            });
        }

        if (user.Status != UserStatuses.Active && user.Status != UserStatuses.Pending)
        {
            return BadRequest(new
            {
                message = "Trạng thái tài khoản không hợp lệ.",
                status = user.Status,
                nextStep = AuthNextSteps.Unknown
            });
        }

        var token = GenerateJwtToken(user);
        var expiresInMinutes = int.Parse(_configuration["Jwt:ExpireMinutes"]!);
        var nextStep = await GetNextStepAsync(user);

        var response = new LoginResponse
        {
            Message = user.Role == UserRoles.Jockey
                && user.Status == UserStatuses.Pending
                && nextStep == AuthNextSteps.CompleteJockeyProfile
                ? "Đăng nhập thành công. Vui lòng hoàn thiện hồ sơ."
                : "Đăng nhập thành công.",
            Token = token,
            ExpiresInMinutes = expiresInMinutes,
            NextStep = nextStep,
            User = new LoginUserResponse
            {
                UserId = user.UserId,
                FullName = user.FullName,
                Email = user.Email,
                Phone = user.Phone,
                Role = user.Role,
                Status = user.Status,
                EmailVerified = user.EmailVerified,
                BettingPoints = user.BettingPoints
            }
        };

        return Ok(response);
    }


    // PUT: /api/auth/change-password
    [HttpPut("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        var userIdText = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!int.TryParse(userIdText, out var userId))
        {
            return Unauthorized(new
            {
                message = "Invalid or missing user token."
            });
        }

        if (request.NewPassword != request.ConfirmPassword)
        {
            return BadRequest(new
            {
                message = "Confirm password does not match the new password."
            });
        }

        if (request.CurrentPassword == request.NewPassword)
        {
            return BadRequest(new
            {
                message = "New password must be different from current password."
            });
        }

        var user = await _context.Users
            .FirstOrDefaultAsync(item => item.UserId == userId);

        if (user == null)
        {
            return NotFound(new
            {
                message = "Account not found."
            });
        }

        var verificationResult = _passwordHasher.VerifyHashedPassword(
            user,
            user.PasswordHash,
            request.CurrentPassword);

        if (verificationResult == PasswordVerificationResult.Failed)
        {
            return BadRequest(new
            {
                message = "Current password is incorrect."
            });
        }

        user.PasswordHash = _passwordHasher.HashPassword(
            user,
            request.NewPassword);
        user.TokenVersion++; // Revoke all previously issued access tokens.
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Password changed successfully."
        });
    }


    [HttpPost("forgot-password")]
    [EnableRateLimiting("LoginRateLimit")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

        // Always return the same response to prevent account enumeration.
        if (user != null && user.EmailVerified && user.Status != UserStatuses.Banned)
        {
            var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
            var now = DateTime.UtcNow;

            var oldTokens = await _context.PasswordResetTokens
                .Where(t => t.UserId == user.UserId && !t.IsUsed && t.ExpiresAt > now)
                .ToListAsync();
            foreach (var oldToken in oldTokens)
            {
                oldToken.IsUsed = true;
                oldToken.UsedAt = now;
            }

            _context.PasswordResetTokens.Add(new PasswordResetToken
            {
                UserId = user.UserId,
                TokenHash = tokenHash,
                ExpiresAt = now.AddMinutes(30),
                IsUsed = false,
                CreatedAt = now,
                RequestedIp = HttpContext.Connection.RemoteIpAddress?.ToString()
            });
            await _context.SaveChangesAsync();

            var frontendBaseUrl = _configuration["Frontend:BaseUrl"]?.TrimEnd('/') ?? "http://localhost:5173";
            var resetUrl = $"{frontendBaseUrl}/reset-password?token={Uri.EscapeDataString(rawToken)}";
            await _emailService.SendEmailAsync(user.Email, "Elite Racing League - Reset Password",
                $"<p>Hello {System.Net.WebUtility.HtmlEncode(user.FullName)},</p>" +
                $"<p>Use this link within 30 minutes to reset your password:</p>" +
                $"<p><a href=\"{resetUrl}\">Reset password</a></p>" +
                "<p>If you did not request this, ignore the email.</p>");
        }

        return Ok(new { message = "If the email is valid, a password reset link has been sent." });
    }

    [HttpPost("reset-password")]
    [EnableRateLimiting("LoginRateLimit")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
    {
        if (request.NewPassword != request.ConfirmPassword)
            return BadRequest(new { message = "Confirm password does not match." });

        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.Token)));
        var now = DateTime.UtcNow;
        var resetToken = await _context.PasswordResetTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash && !t.IsUsed && t.ExpiresAt > now);

        if (resetToken == null)
            return BadRequest(new { message = "Password reset token is invalid or expired." });

        resetToken.User.PasswordHash = _passwordHasher.HashPassword(resetToken.User, request.NewPassword);
        resetToken.User.TokenVersion++;
        resetToken.User.FailedLoginAttempts = 0;
        resetToken.User.LockoutEndAt = null;
        resetToken.User.UpdatedAt = now;
        resetToken.IsUsed = true;
        resetToken.UsedAt = now;

        var siblingTokens = await _context.PasswordResetTokens
            .Where(t => t.UserId == resetToken.UserId && !t.IsUsed)
            .ToListAsync();
        foreach (var token in siblingTokens)
        {
            token.IsUsed = true;
            token.UsedAt = now;
        }

        await _context.SaveChangesAsync();
        return Ok(new { message = "Password reset successfully. Please login again." });
    }

    [HttpPost("logout-all")]
    [Authorize]
    public async Task<IActionResult> LogoutAll()
    {
        var userIdText = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdText, out var userId)) return Unauthorized();
        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
        if (user == null) return Unauthorized();
        user.TokenVersion++;
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return Ok(new { message = "All active tokens have been revoked." });
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

        var nextStep = await GetNextStepAsync(user);

        return Ok(new
        {
            userId = user.UserId,
            fullName = user.FullName,
            email = user.Email,
            phone = user.Phone,
            role = user.Role,
            status = user.Status,
            emailVerified = user.EmailVerified,
            bettingPoints = user.BettingPoints,
            nextStep
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
                    IsActive = false,
                    CreatedAt = DateTime.UtcNow
                });
                break;

            case UserRoles.Jockey:
                _context.Jockeys.Add(new Eliteracingleague.API.Models.Jockey
                {
                    JockeyId = user.UserId,
                    WeightKg = 50m,
                    YearsOfExperience = 0,
                    HealthStatus = JockeyHealthStatuses.Unknown,
                    CertificateNo = null,
                    CertificateFileUrl = null,
                    ProfileImageUrl = null,
                    IdCardFrontUrl = null,
                    IdCardBackUrl = null,
                    HealthCertificateUrl = null,
                    IsActive = false,
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


    private async Task<string> GetNextStepAsync(User user)
    {
        var jockey = await LoadJockeyForNextStepAsync(user);

        return GetNextStep(user, jockey);
    }

    private async Task<Eliteracingleague.API.Models.Jockey?> LoadJockeyForNextStepAsync(User user)
    {
        if (user.Role != UserRoles.Jockey)
        {
            return null;
        }

        return await _context.Jockeys
            .AsNoTracking()
            .Include(j => j.JockeyDistanceExperiences)
            .FirstOrDefaultAsync(j => j.JockeyId == user.UserId);
    }

    private static string GetNextStep(User user, Eliteracingleague.API.Models.Jockey? jockey)
    {
        if (user.Role != UserRoles.Jockey)
        {
            return GetNextStepForNonJockey(user);
        }

        return GetNextStepForJockey(user, jockey);
    }

    private static string GetNextStepForNonJockey(User user)
    {
        if (!user.EmailVerified)
        {
            return AuthNextSteps.VerifyEmail;
        }

        if (user.Status == UserStatuses.Active)
        {
            return AuthNextSteps.GoToDashboard;
        }

        if (user.Status == UserStatuses.Pending && user.Role == UserRoles.HorseOwner)
        {
            return AuthNextSteps.AddHorse;
        }

        if (user.Status == UserStatuses.Pending)
        {
            return AuthNextSteps.WaitForActivation;
        }

        if (user.Status == UserStatuses.Inactive)
        {
            return AuthNextSteps.ContactSupport;
        }

        if (user.Status == UserStatuses.Banned)
        {
            return AuthNextSteps.AccountBlocked;
        }

        return AuthNextSteps.Unknown;
    }

    private static string GetNextStepForJockey(User user, Eliteracingleague.API.Models.Jockey? jockey)
    {
        if (!user.EmailVerified)
        {
            return AuthNextSteps.VerifyEmail;
        }

        if (user.Status == UserStatuses.Inactive)
        {
            return AuthNextSteps.ContactSupport;
        }

        if (user.Status == UserStatuses.Banned)
        {
            return AuthNextSteps.AccountBlocked;
        }

        if (user.Status != UserStatuses.Active && user.Status != UserStatuses.Pending)
        {
            return AuthNextSteps.Unknown;
        }

        if (jockey == null || !JockeyProfileService.IsJockeyProfileCompleted(jockey))
        {
            return AuthNextSteps.CompleteJockeyProfile;
        }

        if (user.Status == UserStatuses.Pending || !jockey.IsActive)
        {
            return AuthNextSteps.WaitForActivation;
        }

        return AuthNextSteps.GoToDashboard;
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
        new Claim(ClaimTypes.Role, user.Role),
        new Claim("token_version", user.TokenVersion.ToString())
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