using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.Constants;
using Eliteracingleague.API.DTOs.Admin;
using Eliteracingleague.API.Services;
using JockeyEntity = Eliteracingleague.API.Models.Jockey;

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
            var owners = await _context.Users
                .AsNoTracking()
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

            var jockeys = await LoadPendingCompletedJockeyVerificationsAsync();

            return Ok(owners.Concat(jockeys)
                .OrderByDescending(v => v.CreatedAt)
                .ToList());
        }

        [HttpGet("owners")]
        public async Task<IActionResult> GetOwnerVerifications()
        {
            var users = await _context.Users
                .AsNoTracking()
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
            var users = await LoadPendingCompletedJockeyVerificationsAsync();
            return Ok(users);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetVerificationById(int id)
        {
            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == id);

            if (user == null)
            {
                return NotFound(new AdminActionResponse
                {
                    Message = "Verification user not found",
                    Id = id
                });
            }

            if (user.Role == UserRoles.Jockey)
            {
                var jockey = await LoadJockeyVerificationQuery()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(j => j.JockeyId == id);

                if (jockey == null)
                {
                    return NotFound(new AdminActionResponse
                    {
                        Message = "Jockey profile not found",
                        Id = id
                    });
                }

                return Ok(MapJockeyVerification(jockey));
            }

            if (user.Role == UserRoles.HorseOwner)
            {
                var ownerAddress = await _context.HorseOwners
                    .AsNoTracking()
                    .Where(o => o.OwnerId == user.UserId)
                    .Select(o => o.Address)
                    .FirstOrDefaultAsync();

                return Ok(new AdminVerificationResponse
                {
                    UserId = user.UserId,
                    FullName = user.FullName,
                    Email = user.Email,
                    Phone = user.Phone,
                    Role = user.Role,
                    Status = user.Status,
                    EmailVerified = user.EmailVerified,
                    CreatedAt = user.CreatedAt,
                    Address = ownerAddress
                });
            }

            return BadRequest(new AdminActionResponse
            {
                Message = "Only HorseOwner or Jockey can be verified",
                Id = id
            });
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

            bool? isActive = null;

            if (user.Role == UserRoles.Jockey)
            {
                var jockey = await _context.Jockeys
                    .Include(j => j.JockeyDistanceExperiences)
                    .FirstOrDefaultAsync(j => j.JockeyId == user.UserId);

                if (jockey == null)
                {
                    return NotFound(new AdminActionResponse
                    {
                        Message = "Jockey profile not found",
                        Id = id
                    });
                }

                if (!JockeyProfileService.IsJockeyProfileCompleted(jockey))
                {
                    return BadRequest(new AdminActionResponse
                    {
                        Message = "Jockey profile is not completed",
                        Id = id,
                        Status = user.Status,
                        IsActive = jockey.IsActive
                    });
                }

                jockey.IsActive = true;
                isActive = jockey.IsActive;
            }

            user.Status = UserStatuses.Active;
            user.EmailVerified = true;
            user.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new AdminActionResponse
            {
                Message = "Verification approved successfully",
                Id = user.UserId,
                Name = user.FullName,
                Status = user.Status,
                IsActive = isActive
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

            bool? isActive = null;

            if (user.Role == UserRoles.Jockey)
            {
                var jockey = await _context.Jockeys
                    .FirstOrDefaultAsync(j => j.JockeyId == user.UserId);

                if (jockey == null)
                {
                    return NotFound(new AdminActionResponse
                    {
                        Message = "Jockey profile not found",
                        Id = id
                    });
                }

                jockey.IsActive = false;
                isActive = jockey.IsActive;
            }

            user.Status = UserStatuses.Inactive;
            user.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new AdminActionResponse
            {
                Message = "Verification rejected successfully",
                Id = user.UserId,
                Name = user.FullName,
                Status = user.Status,
                IsActive = isActive
            });
        }

        private async Task<List<AdminVerificationResponse>> LoadPendingCompletedJockeyVerificationsAsync()
        {
            var jockeys = await LoadJockeyVerificationQuery()
                .AsNoTracking()
                .Where(j =>
                    j.JockeyNavigation.Role == UserRoles.Jockey &&
                    j.JockeyNavigation.Status == UserStatuses.Pending &&
                    !j.IsActive)
                .OrderByDescending(j => j.JockeyNavigation.CreatedAt)
                .ToListAsync();

            return jockeys
                .Where(j => JockeyProfileService.IsJockeyProfileCompleted(j))
                .Select(j => MapJockeyVerification(j))
                .ToList();
        }

        private IQueryable<JockeyEntity> LoadJockeyVerificationQuery()
        {
            return _context.Jockeys
                .Include(j => j.JockeyNavigation)
                .Include(j => j.JockeyDistanceExperiences)
                .Include(j => j.JockeyBreedExperiences)
                    .ThenInclude(e => e.Breed);
        }

        private static AdminVerificationResponse MapJockeyVerification(JockeyEntity jockey)
        {
            return new AdminVerificationResponse
            {
                UserId = jockey.JockeyId,
                JockeyId = jockey.JockeyId,
                JockeyCode = $"J-{jockey.JockeyId:D5}",

                FullName = jockey.JockeyNavigation.FullName,
                Email = jockey.JockeyNavigation.Email,
                Phone = jockey.JockeyNavigation.Phone,
                Role = jockey.JockeyNavigation.Role,
                Status = jockey.JockeyNavigation.Status,
                EmailVerified = jockey.JockeyNavigation.EmailVerified,
                CreatedAt = jockey.JockeyNavigation.CreatedAt,

                IsActive = jockey.IsActive,
                ProfileImageUrl = jockey.ProfileImageUrl,
                IdCardFrontUrl = jockey.IdCardFrontUrl,
                IdCardBackUrl = jockey.IdCardBackUrl,
                CertificateNo = jockey.CertificateNo,
                CertificateFileUrl = jockey.CertificateFileUrl,
                HealthCertificateUrl = jockey.HealthCertificateUrl,
                WeightKg = jockey.WeightKg,
                HealthStatus = jockey.HealthStatus,
                YearsOfExperience = jockey.YearsOfExperience,

                DistanceExperiences = jockey.JockeyDistanceExperiences
                    .OrderBy(e => e.DistanceMeters)
                    .Select(e => new AdminVerificationDistanceExperienceResponse
                    {
                        DistanceMeters = e.DistanceMeters,
                        Label = GetDistanceLabel(e.DistanceMeters),
                        SkillLevel = e.SkillLevel
                    })
                    .ToList(),

                BreedExperiences = jockey.JockeyBreedExperiences
                    .OrderBy(e => e.Breed.BreedName)
                    .Select(e => new AdminVerificationBreedExperienceResponse
                    {
                        BreedId = e.BreedId,
                        BreedName = e.Breed.BreedName,
                        ExperienceLevel = e.ExperienceLevel
                    })
                    .ToList()
            };
        }

        private static string GetDistanceLabel(int distanceMeters)
        {
            return distanceMeters switch
            {
                1000 => "1000m Sprint",
                1500 => "1500m Mid",
                2400 => "2400m Endurance",
                _ => $"{distanceMeters}m"
            };
        }
    }
}