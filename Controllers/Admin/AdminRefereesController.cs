using System.Net.Mail;
using System.Text.RegularExpressions;
using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Admin;
using Eliteracingleague.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Controllers.Admin;

[Authorize(Roles = UserRoles.Admin)]
[ApiController]
[Route("api/admin/referees")]
public class AdminRefereesController : ControllerBase
{
    // Prevent regular-expression denial-of-service by limiting every regex execution.
    private static readonly TimeSpan RegexTimeout =
        TimeSpan.FromMilliseconds(500);

    private static readonly Regex FullNameRegex = new(
        @"^[\p{L}\p{M}][\p{L}\p{M}\s'.-]*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        RegexTimeout);

    private static readonly Regex LicenseRegex = new(
        @"^[A-Z0-9][A-Z0-9./-]{2,99}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        RegexTimeout);

    private static readonly Regex PhoneValidationRegex = new(
        @"^\+?\d{9,15}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        RegexTimeout);

    private static readonly Regex WhitespaceRegex = new(
        @"\s+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        RegexTimeout);

    private static readonly Regex PhoneInputRegex = new(
        @"^\+?[0-9\s().-]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        RegexTimeout);

    private readonly EliteRacingLeagueContext _context;
    private readonly PasswordHasher<User> _passwordHasher;

    public AdminRefereesController(EliteRacingLeagueContext context)
    {
        _context = context;
        _passwordHasher = new PasswordHasher<User>();
    }

    [HttpPost]
    public async Task<IActionResult> CreateRefereeAccount(
        [FromBody] CreateRefereeAccountRequest request)
    {
        var fullName = NormalizeWhitespace(request.FullName);
        var email = request.Email.Trim().ToLowerInvariant();
        var phone = NormalizePhone(request.Phone);
        var licenseNo = NormalizeLicense(request.LicenseNo);
        var status = request.Status.Trim();

        var validationError = ValidateRefereeProfile(
            fullName,
            email,
            phone,
            licenseNo,
            request.ExperienceYears);

        if (validationError != null)
        {
            return BadRequest(new { message = validationError });
        }

        var passwordError = ValidatePassword(request.Password);
        if (passwordError != null)
        {
            return BadRequest(new { message = passwordError });
        }

        if (!string.Equals(
                request.Password,
                request.ConfirmPassword,
                StringComparison.Ordinal))
        {
            return BadRequest(new
            {
                message = "Confirm password does not match."
            });
        }

        if (status is not UserStatuses.Active and not UserStatuses.Inactive)
        {
            return BadRequest(new
            {
                message = "Status must be Active or Inactive."
            });
        }

        var emailExists = await _context.Users
            .AsNoTracking()
            .AnyAsync(u => u.Email == email);

        if (emailExists)
        {
            return Conflict(new
            {
                message = "Email already exists."
            });
        }

        if (licenseNo != null)
        {
            var licenseExists = await _context.RaceReferees
                .AsNoTracking()
                .AnyAsync(r => r.LicenseNo == licenseNo);

            if (licenseExists)
            {
                return Conflict(new
                {
                    message = "License number already exists."
                });
            }
        }

        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        try
        {
            var now = DateTime.UtcNow;
            var isActive = status == UserStatuses.Active;

            var user = new User
            {
                FullName = fullName,
                Email = email,
                Phone = phone,
                Role = UserRoles.RaceReferee,
                Status = status,
                EmailVerified = true,
                CreatedAt = now,
                UpdatedAt = now
            };

            user.PasswordHash = _passwordHasher.HashPassword(
                user,
                request.Password);

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var referee = new RaceReferee
            {
                RefereeId = user.UserId,
                LicenseNo = licenseNo,
                ExperienceYears = request.ExperienceYears ?? 0,
                IsActive = isActive,
                CreatedAt = now
            };

            _context.RaceReferees.Add(referee);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(ToResponse(referee, user));
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();

            return Conflict(new
            {
                message = "Email or license number already exists."
            });
        }
        catch
        {
            await transaction.RollbackAsync();

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    message = "Create referee account failed."
                });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetReferees()
    {
        var referees = await _context.RaceReferees
            .AsNoTracking()
            .Where(r => r.Referee.Role == UserRoles.RaceReferee)
            .Select(r => new AdminRefereeResponse
            {
                RefereeId = r.RefereeId,
                UserId = r.RefereeId,
                FullName = r.Referee.FullName,
                Email = r.Referee.Email,
                Phone = r.Referee.Phone,
                Role = r.Referee.Role,
                Status = r.Referee.Status,
                EmailVerified = r.Referee.EmailVerified,
                LicenseNo = r.LicenseNo,
                ExperienceYears = r.ExperienceYears,
                IsActive = r.IsActive,
                CreatedAt = r.CreatedAt
            })
            .OrderBy(r => r.FullName)
            .ToListAsync();

        return Ok(referees);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetRefereeById(int id)
    {
        var referee = await _context.RaceReferees
            .AsNoTracking()
            .Where(r =>
                r.RefereeId == id &&
                r.Referee.Role == UserRoles.RaceReferee)
            .Select(r => new AdminRefereeResponse
            {
                RefereeId = r.RefereeId,
                UserId = r.RefereeId,
                FullName = r.Referee.FullName,
                Email = r.Referee.Email,
                Phone = r.Referee.Phone,
                Role = r.Referee.Role,
                Status = r.Referee.Status,
                EmailVerified = r.Referee.EmailVerified,
                LicenseNo = r.LicenseNo,
                ExperienceYears = r.ExperienceYears,
                IsActive = r.IsActive,
                CreatedAt = r.CreatedAt
            })
            .FirstOrDefaultAsync();

        if (referee == null)
        {
            return NotFound(new
            {
                message = "Referee not found.",
                refereeId = id
            });
        }

        return Ok(referee);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateReferee(
        int id,
        [FromBody] UpdateRefereeRequest request)
    {
        var fullName = NormalizeWhitespace(request.FullName);
        var email = request.Email.Trim().ToLowerInvariant();
        var phone = NormalizePhone(request.Phone);
        var licenseNo = NormalizeLicense(request.LicenseNo);

        var validationError = ValidateRefereeProfile(
            fullName,
            email,
            phone,
            licenseNo,
            request.ExperienceYears);

        if (validationError != null)
        {
            return BadRequest(new { message = validationError });
        }

        var referee = await _context.RaceReferees
            .Include(r => r.Referee)
            .FirstOrDefaultAsync(r =>
                r.RefereeId == id &&
                r.Referee.Role == UserRoles.RaceReferee);

        if (referee == null)
        {
            return NotFound(new
            {
                message = "Referee not found.",
                refereeId = id
            });
        }

        var targetIsActive = request.IsActive ?? referee.IsActive;

        var emailExists = await _context.Users
            .AsNoTracking()
            .AnyAsync(u => u.UserId != id && u.Email == email);

        if (emailExists)
        {
            return Conflict(new
            {
                message = "Email already exists."
            });
        }

        if (licenseNo != null)
        {
            var licenseExists = await _context.RaceReferees
                .AsNoTracking()
                .AnyAsync(r =>
                    r.RefereeId != id &&
                    r.LicenseNo == licenseNo);

            if (licenseExists)
            {
                return Conflict(new
                {
                    message = "License number already exists."
                });
            }
        }

        if (!targetIsActive &&
            await HasUnfinishedAssignmentAsync(id))
        {
            return BadRequest(new
            {
                message = "Cannot deactivate a referee who is assigned to an unfinished race. Reassign or cancel the race first.",
                refereeId = id
            });
        }

        if (targetIsActive &&
            referee.Referee.Status == UserStatuses.Banned)
        {
            return BadRequest(new
            {
                message = "This referee account is banned. Unblock the user in User Management before activating the referee profile.",
                refereeId = id
            });
        }

        var now = DateTime.UtcNow;

        referee.Referee.FullName = fullName;
        referee.Referee.Email = email;
        referee.Referee.Phone = phone;
        referee.Referee.Status = targetIsActive
            ? UserStatuses.Active
            : UserStatuses.Inactive;
        referee.Referee.UpdatedAt = now;

        referee.LicenseNo = licenseNo;
        referee.ExperienceYears = request.ExperienceYears ?? 0;
        referee.IsActive = targetIsActive;

        try
        {
            await _context.SaveChangesAsync();
            return Ok(ToResponse(referee, referee.Referee));
        }
        catch (DbUpdateException)
        {
            return Conflict(new
            {
                message = "Email or license number already exists."
            });
        }
    }

    private async Task<bool> HasUnfinishedAssignmentAsync(int refereeId)
    {
        return await _context.RefereeAssignments
            .AsNoTracking()
            .AnyAsync(a =>
                a.RefereeId == refereeId &&
                a.Status == RefereeAssignmentStatuses.Assigned &&
                a.Race.Status != RaceStatuses.Published &&
                a.Race.Status != RaceStatuses.Cancelled &&
                a.Race.Tournament.Status != TournamentStatuses.Completed &&
                a.Race.Tournament.Status != TournamentStatuses.Cancelled);
    }

    private static string? ValidateRefereeProfile(
        string fullName,
        string email,
        string? phone,
        string? licenseNo,
        int? experienceYears)
    {
        if (fullName.Length < 2 || fullName.Length > 150)
        {
            return "Full name must be between 2 and 150 characters.";
        }

        if (!FullNameRegex.IsMatch(fullName))
        {
            return "Full name can only contain letters, spaces, apostrophes, dots, and hyphens.";
        }

        if (email.Length > 255 ||
            !MailAddress.TryCreate(email, out var mailAddress) ||
            !string.Equals(
                mailAddress.Address,
                email,
                StringComparison.OrdinalIgnoreCase))
        {
            return "Email format is invalid.";
        }

        if (phone != null &&
            (!IsSafeMatch(PhoneValidationRegex, phone) ||
             phone.Length > 16))
        {
            return "Phone must contain 9 to 15 digits and may start with +.";
        }

        if (licenseNo != null && !LicenseRegex.IsMatch(licenseNo))
        {
            return "License number must be 3 to 100 characters and contain only letters, numbers, dots, slashes, or hyphens.";
        }

        if (experienceYears is < 0 or > 60)
        {
            return "Experience years must be between 0 and 60.";
        }

        return null;
    }

    private static string? ValidatePassword(string password)
    {
        if (password.Length < 8 || password.Length > 72)
        {
            return "Password must be between 8 and 72 characters.";
        }

        if (password.Any(char.IsWhiteSpace))
        {
            return "Password cannot contain spaces.";
        }

        if (!password.Any(char.IsUpper) ||
            !password.Any(char.IsLower) ||
            !password.Any(char.IsDigit) ||
            !password.Any(character => !char.IsLetterOrDigit(character)))
        {
            return "Password must include uppercase, lowercase, number, and special character.";
        }

        return null;
    }

    private static string NormalizeWhitespace(string value)
    {
        var trimmed = value.Trim();

        try
        {
            return WhitespaceRegex.Replace(trimmed, " ");
        }
        catch (RegexMatchTimeoutException)
        {
            // Keep validation deterministic even for unexpectedly large input.
            return trimmed;
        }
    }

    private static string? NormalizePhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        if (!IsSafeMatch(PhoneInputRegex, trimmed))
        {
            return trimmed;
        }

        var prefix = trimmed.StartsWith('+') ? "+" : string.Empty;
        var digits = new string(trimmed.Where(char.IsDigit).ToArray());

        return prefix + digits;
    }

    private static bool IsSafeMatch(Regex regex, string value)
    {
        try
        {
            return regex.IsMatch(value);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    private static string? NormalizeLicense(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().ToUpperInvariant();
    }

    private static AdminRefereeResponse ToResponse(
        RaceReferee referee,
        User user)
    {
        return new AdminRefereeResponse
        {
            RefereeId = referee.RefereeId,
            UserId = user.UserId,
            FullName = user.FullName,
            Email = user.Email,
            Phone = user.Phone,
            Role = user.Role,
            Status = user.Status,
            EmailVerified = user.EmailVerified,
            LicenseNo = referee.LicenseNo,
            ExperienceYears = referee.ExperienceYears,
            IsActive = referee.IsActive,
            CreatedAt = user.CreatedAt
        };
    }
}