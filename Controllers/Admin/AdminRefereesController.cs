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
    private readonly EliteRacingLeagueContext _context;
    private readonly PasswordHasher<User> _passwordHasher;

    public AdminRefereesController(EliteRacingLeagueContext context)
    {
        _context = context;
        _passwordHasher = new PasswordHasher<User>();
    }

    [HttpPost]
    public async Task<IActionResult> CreateRefereeAccount(CreateRefereeAccountRequest request)
    {
        var fullName = request.FullName.Trim();
        var email = request.Email.Trim().ToLowerInvariant();
        var phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        var licenseNo = string.IsNullOrWhiteSpace(request.LicenseNo) ? null : request.LicenseNo.Trim();

        if (string.IsNullOrWhiteSpace(fullName))
        {
            return BadRequest(new
            {
                message = "Full name is required."
            });
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest(new
            {
                message = "Email is required."
            });
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
        {
            return BadRequest(new
            {
                message = "Password must be at least 6 characters."
            });
        }

        if (request.Password != request.ConfirmPassword)
        {
            return BadRequest(new
            {
                message = "Confirm password does not match."
            });
        }

        if (request.ExperienceYears.HasValue && request.ExperienceYears.Value < 0)
        {
            return BadRequest(new
            {
                message = "Experience years cannot be negative."
            });
        }

        var emailExists = await _context.Users.AnyAsync(u => u.Email == email);

        if (emailExists)
        {
            return BadRequest(new
            {
                message = "Email already exists."
            });
        }

        if (!string.IsNullOrWhiteSpace(licenseNo))
        {
            var licenseExists = await _context.RaceReferees
                .AnyAsync(r => r.LicenseNo == licenseNo);

            if (licenseExists)
            {
                return BadRequest(new
                {
                    message = "License number already exists."
                });
            }
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var now = DateTime.UtcNow;

            var user = new User
            {
                FullName = fullName,
                Email = email,
                Phone = phone,
                Role = UserRoles.RaceReferee,
                Status = UserStatuses.Active,
                EmailVerified = true,
                CreatedAt = now,
                UpdatedAt = now
            };

            user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var referee = new RaceReferee
            {
                RefereeId = user.UserId,
                LicenseNo = licenseNo,
                ExperienceYears = request.ExperienceYears ?? 0,
                IsActive = true,
                CreatedAt = now
            };

            _context.RaceReferees.Add(referee);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(ToResponse(referee, user));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();

            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Create referee account failed.",
                error = ex.Message,
                innerError = ex.InnerException?.Message
            });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetReferees()
    {
        var referees = await _context.RaceReferees
            .AsNoTracking()
            .Where(r =>
                r.IsActive &&
                r.Referee.Role == UserRoles.RaceReferee &&
                r.Referee.Status == UserStatuses.Active)
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
        var fullName = request.FullName.Trim();
        var email = request.Email.Trim().ToLowerInvariant();
        var phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        var licenseNo = string.IsNullOrWhiteSpace(request.LicenseNo) ? null : request.LicenseNo.Trim();

        if (string.IsNullOrWhiteSpace(fullName))
        {
            return BadRequest(new
            {
                message = "Full name is required."
            });
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest(new
            {
                message = "Email is required."
            });
        }

        if (request.ExperienceYears.HasValue && request.ExperienceYears.Value < 0)
        {
            return BadRequest(new
            {
                message = "Experience years cannot be negative."
            });
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
            return BadRequest(new
            {
                message = "Email already exists."
            });
        }

        if (!string.IsNullOrWhiteSpace(licenseNo))
        {
            var licenseExists = await _context.RaceReferees
                .AsNoTracking()
                .AnyAsync(r => r.RefereeId != id && r.LicenseNo == licenseNo);

            if (licenseExists)
            {
                return BadRequest(new
                {
                    message = "License number already exists."
                });
            }
        }

        if (!targetIsActive)
        {
            var hasUnfinishedAssignment = await _context.RefereeAssignments
                .AsNoTracking()
                .AnyAsync(a =>
                    a.RefereeId == id &&
                    a.Status == RefereeAssignmentStatuses.Assigned &&
                    a.Race.Status != RaceStatuses.Published &&
                    a.Race.Status != RaceStatuses.Cancelled &&
                    a.Race.Tournament.Status != TournamentStatuses.Completed &&
                    a.Race.Tournament.Status != TournamentStatuses.Cancelled);

            if (hasUnfinishedAssignment)
            {
                return BadRequest(new
                {
                    message = "Cannot deactivate a referee who is assigned to an unfinished race. Reassign or cancel the race first.",
                    refereeId = id
                });
            }
        }

        if (targetIsActive && referee.Referee.Status == UserStatuses.Banned)
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

        await _context.SaveChangesAsync();

        return Ok(ToResponse(referee, referee.Referee));
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