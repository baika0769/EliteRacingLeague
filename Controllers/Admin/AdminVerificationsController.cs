using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.Constants;
using Eliteracingleague.API.DTOs.Admin;

namespace Eliteracingleague.API.Controllers.Admin
{
    [Authorize(Roles = UserRoles.Admin)]
    [ApiController]
    [Route("api/admin/verifications")]
    public class AdminVerificationsController : ControllerBase
    {
        private readonly EliteRacingLeagueContext _context;

        public AdminVerificationsController(EliteRacingLeagueContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetVerifications()
        {
            var users = await _context.Users
                .Where(u =>
                    u.Status == UserStatuses.Pending &&
                    (u.Role == UserRoles.HorseOwner || u.Role == UserRoles.Jockey))
                .Select(u => new AdminVerificationResponse
                {
                    UserId = u.UserId,
                    FullName = u.FullName,
                    Email = u.Email,
                    Phone = u.Phone,
                    Role = u.Role,
                    Status = u.Status,
                    EmailVerified = u.EmailVerified,
                    CreatedAt = u.CreatedAt,

                    Address = _context.HorseOwners
                        .Where(o => o.OwnerId == u.UserId)
                        .Select(o => o.Address)
                        .FirstOrDefault(),

                    WeightKg = _context.Jockeys
                        .Where(j => j.JockeyId == u.UserId)
                        .Select(j => (decimal?)j.WeightKg)
                        .FirstOrDefault(),

                    YearsOfExperience = _context.Jockeys
                        .Where(j => j.JockeyId == u.UserId)
                        .Select(j => (int?)j.YearsOfExperience)
                        .FirstOrDefault(),

                    HealthStatus = _context.Jockeys
                        .Where(j => j.JockeyId == u.UserId)
                        .Select(j => j.HealthStatus)
                        .FirstOrDefault(),

                    CertificateNo = _context.Jockeys
                        .Where(j => j.JockeyId == u.UserId)
                        .Select(j => j.CertificateNo)
                        .FirstOrDefault(),

                    CertificateFileUrl = _context.Jockeys
                        .Where(j => j.JockeyId == u.UserId)
                        .Select(j => j.CertificateFileUrl)
                        .FirstOrDefault()
                })
                .ToListAsync();

            return Ok(users);
        }

        [HttpGet("owners")]
        public async Task<IActionResult> GetOwnerVerifications()
        {
            var users = await _context.Users
                .Where(u => u.Status == UserStatuses.Pending && u.Role == UserRoles.HorseOwner)
                .Select(u => new AdminVerificationResponse
                {
                    UserId = u.UserId,
                    FullName = u.FullName,
                    Email = u.Email,
                    Phone = u.Phone,
                    Role = u.Role,
                    Status = u.Status,
                    EmailVerified = u.EmailVerified,
                    CreatedAt = u.CreatedAt,

                    Address = _context.HorseOwners
                        .Where(o => o.OwnerId == u.UserId)
                        .Select(o => o.Address)
                        .FirstOrDefault()
                })
                .ToListAsync();

            return Ok(users);
        }

        [HttpGet("jockeys")]
        public async Task<IActionResult> GetJockeyVerifications()
        {
            var users = await _context.Users
                .Where(u => u.Status == UserStatuses.Pending && u.Role == UserRoles.Jockey)
                .Select(u => new AdminVerificationResponse
                {
                    UserId = u.UserId,
                    FullName = u.FullName,
                    Email = u.Email,
                    Phone = u.Phone,
                    Role = u.Role,
                    Status = u.Status,
                    EmailVerified = u.EmailVerified,
                    CreatedAt = u.CreatedAt,

                    WeightKg = _context.Jockeys
                        .Where(j => j.JockeyId == u.UserId)
                        .Select(j => (decimal?)j.WeightKg)
                        .FirstOrDefault(),

                    YearsOfExperience = _context.Jockeys
                        .Where(j => j.JockeyId == u.UserId)
                        .Select(j => (int?)j.YearsOfExperience)
                        .FirstOrDefault(),

                    HealthStatus = _context.Jockeys
                        .Where(j => j.JockeyId == u.UserId)
                        .Select(j => j.HealthStatus)
                        .FirstOrDefault(),

                    CertificateNo = _context.Jockeys
                        .Where(j => j.JockeyId == u.UserId)
                        .Select(j => j.CertificateNo)
                        .FirstOrDefault(),

                    CertificateFileUrl = _context.Jockeys
                        .Where(j => j.JockeyId == u.UserId)
                        .Select(j => j.CertificateFileUrl)
                        .FirstOrDefault()
                })
                .ToListAsync();

            return Ok(users);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetVerificationById(int id)
        {
            var user = await _context.Users
                .Where(u => u.UserId == id)
                .Select(u => new AdminVerificationResponse
                {
                    UserId = u.UserId,
                    FullName = u.FullName,
                    Email = u.Email,
                    Phone = u.Phone,
                    Role = u.Role,
                    Status = u.Status,
                    EmailVerified = u.EmailVerified,
                    CreatedAt = u.CreatedAt,

                    Address = _context.HorseOwners
                        .Where(o => o.OwnerId == u.UserId)
                        .Select(o => o.Address)
                        .FirstOrDefault(),

                    WeightKg = _context.Jockeys
                        .Where(j => j.JockeyId == u.UserId)
                        .Select(j => (decimal?)j.WeightKg)
                        .FirstOrDefault(),

                    YearsOfExperience = _context.Jockeys
                        .Where(j => j.JockeyId == u.UserId)
                        .Select(j => (int?)j.YearsOfExperience)
                        .FirstOrDefault(),

                    HealthStatus = _context.Jockeys
                        .Where(j => j.JockeyId == u.UserId)
                        .Select(j => j.HealthStatus)
                        .FirstOrDefault(),

                    CertificateNo = _context.Jockeys
                        .Where(j => j.JockeyId == u.UserId)
                        .Select(j => j.CertificateNo)
                        .FirstOrDefault(),

                    CertificateFileUrl = _context.Jockeys
                        .Where(j => j.JockeyId == u.UserId)
                        .Select(j => j.CertificateFileUrl)
                        .FirstOrDefault()
                })
                .FirstOrDefaultAsync();

            if (user == null)
            {
                return NotFound(new AdminActionResponse
                {
                    Message = "Verification user not found",
                    Id = id
                });
            }

            return Ok(user);
        }

        [HttpPut("{id}/approve")]
        public async Task<IActionResult> ApproveVerification(int id)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == id);

            if (user == null)
            {
                return NotFound(new AdminActionResponse
                {
                    Message = "User not found",
                    Id = id
                });
            }

            if (user.Role != UserRoles.HorseOwner && user.Role != UserRoles.Jockey)
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "Only HorseOwner or Jockey can be verified",
                    Id = id
                });
            }

            user.Status = UserStatuses.Active;
            user.EmailVerified = true;
            user.UpdatedAt = DateTime.UtcNow;

            if (user.Role == UserRoles.Jockey)
            {
                var jockey = await _context.Jockeys.FirstOrDefaultAsync(j => j.JockeyId == user.UserId);

                if (jockey == null)
                {
                    return NotFound(new AdminActionResponse
                    {
                        Message = "Jockey profile not found",
                        Id = id
                    });
                }

                jockey.IsActive = true;
            }

            await _context.SaveChangesAsync();

            return Ok(new AdminActionResponse
            {
                Message = "Verification approved successfully",
                Id = user.UserId,
                Name = user.FullName,
                Status = user.Status
            });
        }

        [HttpPut("{id}/reject")]
        public async Task<IActionResult> RejectVerification(int id)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == id);

            if (user == null)
            {
                return NotFound(new AdminActionResponse
                {
                    Message = "User not found",
                    Id = id
                });
            }

            if (user.Role != UserRoles.HorseOwner && user.Role != UserRoles.Jockey)
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "Only HorseOwner or Jockey can be verified",
                    Id = id
                });
            }

            user.Status = UserStatuses.Inactive;
            user.UpdatedAt = DateTime.UtcNow;

            if (user.Role == UserRoles.Jockey)
            {
                var jockey = await _context.Jockeys.FirstOrDefaultAsync(j => j.JockeyId == user.UserId);

                if (jockey == null)
                {
                    return NotFound(new AdminActionResponse
                    {
                        Message = "Jockey profile not found",
                        Id = id
                    });
                }

                jockey.IsActive = false;
            }

            await _context.SaveChangesAsync();

            return Ok(new AdminActionResponse
            {
                Message = "Verification rejected successfully",
                Id = user.UserId,
                Name = user.FullName,
                Status = user.Status
            });
        }
    }
}
