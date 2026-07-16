using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Admin;
using Eliteracingleague.API.Extensions;
using Eliteracingleague.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Controllers.Admin;

[Authorize(Roles = UserRoles.Admin)]
[ApiController]
[Route("api/admin/users")]
public class AdminUsersController : ControllerBase
{
    private readonly EliteRacingLeagueContext _context;

    public AdminUsersController(EliteRacingLeagueContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetUsers()
    {
        var users = await _context.Users
            .AsNoTracking()
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new AdminUserResponse
            {
                UserId = u.UserId,
                FullName = u.FullName,
                Email = u.Email,
                Role = u.Role,
                Status = u.Status,
                EmailVerified = u.EmailVerified,
                CreatedAt = u.CreatedAt
            })
            .ToListAsync();

        return Ok(users);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetUserById(int id)
    {
        var user = await _context.Users
            .AsNoTracking()
            .Where(u => u.UserId == id)
            .Select(u => new AdminUserResponse
            {
                UserId = u.UserId,
                FullName = u.FullName,
                Email = u.Email,
                Role = u.Role,
                Status = u.Status,
                EmailVerified = u.EmailVerified,
                CreatedAt = u.CreatedAt
            })
            .FirstOrDefaultAsync();

        if (user == null)
        {
            return NotFound(new AdminActionResponse
            {
                Message = "User not found",
                Id = id
            });
        }

        return Ok(user);
    }

    [HttpPut("{id:int}/approve")]
    public async Task<IActionResult> ApproveUser(int id)
    {
        var guardResult = await GetStatusChangeTargetAsync(id, false);
        if (guardResult.Error != null)
        {
            return guardResult.Error;
        }

        var user = guardResult.User!;

        if (user.Status == UserStatuses.Banned)
        {
            return BadRequest(new AdminActionResponse
            {
                Message = "Banned users must be unblocked instead of approved",
                Id = user.UserId,
                Name = user.FullName,
                Status = user.Status
            });
        }

        var now = DateTime.UtcNow;

        user.Status = UserStatuses.Active;
        user.EmailVerified = true;
        user.UpdatedAt = now;

        await SyncRoleProfileStatusAsync(user, true);
        await _context.SaveChangesAsync();

        return Ok(new AdminActionResponse
        {
            Message = "User approved successfully",
            Id = user.UserId,
            Name = user.FullName,
            Status = user.Status
        });
    }

    [HttpPut("{id:int}/reject")]
    public async Task<IActionResult> RejectUser(int id)
    {
        var guardResult = await GetStatusChangeTargetAsync(id, true);
        if (guardResult.Error != null)
        {
            return guardResult.Error;
        }

        var user = guardResult.User!;
        var commitmentError = await GetUnfinishedCommitmentErrorAsync(user);

        if (commitmentError != null)
        {
            return BadRequest(new AdminActionResponse
            {
                Message = commitmentError,
                Id = user.UserId,
                Name = user.FullName,
                Status = user.Status
            });
        }

        user.Status = UserStatuses.Inactive;
        user.UpdatedAt = DateTime.UtcNow;

        await SyncRoleProfileStatusAsync(user, false);
        await _context.SaveChangesAsync();

        return Ok(new AdminActionResponse
        {
            Message = "User rejected successfully",
            Id = user.UserId,
            Name = user.FullName,
            Status = user.Status
        });
    }

    [HttpPut("{id:int}/block")]
    public async Task<IActionResult> BlockUser(int id)
    {
        var guardResult = await GetStatusChangeTargetAsync(id, true);
        if (guardResult.Error != null)
        {
            return guardResult.Error;
        }

        var user = guardResult.User!;
        var commitmentError = await GetUnfinishedCommitmentErrorAsync(user);

        if (commitmentError != null)
        {
            return BadRequest(new AdminActionResponse
            {
                Message = commitmentError,
                Id = user.UserId,
                Name = user.FullName,
                Status = user.Status
            });
        }

        user.Status = UserStatuses.Banned;
        user.UpdatedAt = DateTime.UtcNow;

        await SyncRoleProfileStatusAsync(user, false);
        await _context.SaveChangesAsync();

        return Ok(new AdminActionResponse
        {
            Message = "User blocked successfully",
            Id = user.UserId,
            Name = user.FullName,
            Status = user.Status
        });
    }

    [HttpPut("{id:int}/unblock")]
    public async Task<IActionResult> UnblockUser(int id)
    {
        var guardResult = await GetStatusChangeTargetAsync(id, false);
        if (guardResult.Error != null)
        {
            return guardResult.Error;
        }

        var user = guardResult.User!;

        if (user.Status != UserStatuses.Banned)
        {
            return BadRequest(new AdminActionResponse
            {
                Message = "Only banned users can be unblocked",
                Id = user.UserId,
                Name = user.FullName,
                Status = user.Status
            });
        }

        user.Status = UserStatuses.Active;
        user.EmailVerified = true;
        user.UpdatedAt = DateTime.UtcNow;

        await SyncRoleProfileStatusAsync(user, true);
        await _context.SaveChangesAsync();

        return Ok(new AdminActionResponse
        {
            Message = "User unblocked successfully",
            Id = user.UserId,
            Name = user.FullName,
            Status = user.Status
        });
    }

    private async Task<(User? User, IActionResult? Error)>
        GetStatusChangeTargetAsync(int id, bool preventSelfAction)
    {
        if (id <= 0)
        {
            return (null, BadRequest(new AdminActionResponse
            {
                Message = "Invalid user id",
                Id = id
            }));
        }

        if (!User.TryGetUserId(out var currentAdminId))
        {
            return (null, Unauthorized(new AdminActionResponse
            {
                Message = "Invalid admin token",
                Id = id
            }));
        }

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.UserId == id);

        if (user == null)
        {
            return (null, NotFound(new AdminActionResponse
            {
                Message = "User not found",
                Id = id
            }));
        }

        if (user.Role == UserRoles.Admin)
        {
            return (null, BadRequest(new AdminActionResponse
            {
                Message = "Admin accounts cannot be changed from User Management",
                Id = user.UserId,
                Name = user.FullName,
                Status = user.Status
            }));
        }

        if (preventSelfAction && user.UserId == currentAdminId)
        {
            return (null, BadRequest(new AdminActionResponse
            {
                Message = "You cannot reject or block your own account",
                Id = user.UserId,
                Name = user.FullName,
                Status = user.Status
            }));
        }

        return (user, null);
    }

    private async Task<string?> GetUnfinishedCommitmentErrorAsync(
        User user)
    {
        if (user.Role == UserRoles.RaceReferee)
        {
            var hasAssignment = await _context.RefereeAssignments
                .AsNoTracking()
                .AnyAsync(a =>
                    a.RefereeId == user.UserId &&
                    a.Status == RefereeAssignmentStatuses.Assigned &&
                    a.Race.Status != RaceStatuses.Published &&
                    a.Race.Status != RaceStatuses.Cancelled &&
                    a.Race.Tournament.Status != TournamentStatuses.Completed &&
                    a.Race.Tournament.Status != TournamentStatuses.Cancelled);

            if (hasAssignment)
            {
                return "Cannot deactivate or block a referee assigned to an unfinished race. Reassign or cancel the race first.";
            }
        }

        if (user.Role == UserRoles.Jockey)
        {
            var hasRegistration = await _context.RaceRegistrations
                .AsNoTracking()
                .AnyAsync(r =>
                    r.JockeyId == user.UserId &&
                    r.Status != RaceRegistrationStatuses.Rejected &&
                    r.Status != RaceRegistrationStatuses.Cancelled &&
                    r.Status != RaceRegistrationStatuses.Completed &&
                    r.Race.Status != RaceStatuses.Published &&
                    r.Race.Status != RaceStatuses.Cancelled);

            if (hasRegistration)
            {
                return "Cannot deactivate or block a jockey assigned to an unfinished race registration.";
            }
        }

        if (user.Role == UserRoles.HorseOwner)
        {
            var hasRegistration = await _context.RaceRegistrations
                .AsNoTracking()
                .AnyAsync(r =>
                    r.OwnerId == user.UserId &&
                    r.Status != RaceRegistrationStatuses.Rejected &&
                    r.Status != RaceRegistrationStatuses.Cancelled &&
                    r.Status != RaceRegistrationStatuses.Completed &&
                    r.Race.Status != RaceStatuses.Published &&
                    r.Race.Status != RaceStatuses.Cancelled);

            if (hasRegistration)
            {
                return "Cannot deactivate or block an owner with unfinished race registrations.";
            }
        }

        return null;
    }

    private async Task SyncRoleProfileStatusAsync(
        User user,
        bool isActive)
    {
        if (user.Role == UserRoles.Jockey)
        {
            var jockey = await _context.Jockeys
                .FirstOrDefaultAsync(j => j.JockeyId == user.UserId);

            if (jockey != null)
            {
                jockey.IsActive = isActive;
            }
        }
        else if (user.Role == UserRoles.RaceReferee)
        {
            var referee = await _context.RaceReferees
                .FirstOrDefaultAsync(r => r.RefereeId == user.UserId);

            if (referee != null)
            {
                referee.IsActive = isActive;
            }
        }
        else if (user.Role == UserRoles.HorseOwner)
        {
            var owner = await _context.HorseOwners
                .FirstOrDefaultAsync(o => o.OwnerId == user.UserId);

            if (owner != null)
            {
                owner.IsActive = isActive;
            }
        }
    }
}
