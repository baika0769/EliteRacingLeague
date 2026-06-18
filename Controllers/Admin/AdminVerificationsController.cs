using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.Constants;
using Eliteracingleague.API.DTOs.Admin;
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
            var jockeys = await LoadPendingJockeyVerificationsAsync();
            return Ok(jockeys);
        }

        [HttpGet("owners")]
        public IActionResult GetOwnerVerifications()
        {
            // Flow mới: HorseOwner không cần Admin duyệt.
            // Giữ endpoint này để FE cũ không lỗi.
            return Ok(new List<AdminVerificationResponse>());
        }

        [HttpGet("jockeys")]
        public async Task<IActionResult> GetJockeyVerifications()
        {
            var jockeys = await LoadPendingJockeyVerificationsAsync();
            return Ok(jockeys);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetVerificationById(int id)
        {
            var jockey = await LoadJockeyVerificationQuery()
                .AsNoTracking()
                .FirstOrDefaultAsync(j =>
                    j.JockeyId == id &&
                    j.JockeyNavigation.Role == UserRoles.Jockey);

            if (jockey == null)
            {
                return NotFound(new AdminActionResponse
                {
                    Message = "Jockey verification not found",
                    Id = id
                });
            }

            return Ok(MapJockeyVerification(jockey));
        }

        [HttpPut("{id}/approve")]
        public async Task<IActionResult> ApproveVerification(int id)
        {
            var jockey = await _context.Jockeys
                .Include(j => j.JockeyNavigation)
                .FirstOrDefaultAsync(j =>
                    j.JockeyId == id &&
                    j.JockeyNavigation.Role == UserRoles.Jockey);

            if (jockey == null)
            {
                return NotFound(new AdminActionResponse
                {
                    Message = "Jockey verification not found",
                    Id = id
                });
            }

            var user = jockey.JockeyNavigation;

            if (user.Status == UserStatuses.Banned)
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "Banned account cannot be approved",
                    Id = id,
                    Name = user.FullName,
                    Status = user.Status,
                    IsActive = jockey.IsActive
                });
            }

            // Flow mới:
            // Admin duyệt tài khoản Jockey trước.
            // Không bắt Jockey hoàn thiện hồ sơ trước khi approve.
            user.Status = UserStatuses.Active;
            user.EmailVerified = true;
            user.UpdatedAt = DateTime.UtcNow;

            jockey.IsActive = true;

            await _context.SaveChangesAsync();

            return Ok(new AdminActionResponse
            {
                Message = "Jockey verification approved successfully",
                Id = user.UserId,
                Name = user.FullName,
                Status = user.Status,
                IsActive = jockey.IsActive
            });
        }

        [HttpPut("{id}/reject")]
        public async Task<IActionResult> RejectVerification(int id)
        {
            var jockey = await _context.Jockeys
                .Include(j => j.JockeyNavigation)
                .FirstOrDefaultAsync(j =>
                    j.JockeyId == id &&
                    j.JockeyNavigation.Role == UserRoles.Jockey);

            if (jockey == null)
            {
                return NotFound(new AdminActionResponse
                {
                    Message = "Jockey verification not found",
                    Id = id
                });
            }

            var user = jockey.JockeyNavigation;

            user.Status = UserStatuses.Inactive;
            user.UpdatedAt = DateTime.UtcNow;

            jockey.IsActive = false;

            await _context.SaveChangesAsync();

            return Ok(new AdminActionResponse
            {
                Message = "Jockey verification rejected successfully",
                Id = user.UserId,
                Name = user.FullName,
                Status = user.Status,
                IsActive = jockey.IsActive
            });
        }

        private async Task<List<AdminVerificationResponse>> LoadPendingJockeyVerificationsAsync()
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
                .Select(MapJockeyVerification)
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