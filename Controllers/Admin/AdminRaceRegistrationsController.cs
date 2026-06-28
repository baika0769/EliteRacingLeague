using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Admin;
using Microsoft.AspNetCore.Authorization;
using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Models;
using System.Security.Claims;

namespace Eliteracingleague.API.Controllers.Admin
{
    [Authorize(Roles = UserRoles.Admin)]
    [ApiController]
    [Route("api/admin/registrations")]
    public class AdminRaceRegistrationsController : ControllerBase
    {
        private readonly EliteRacingLeagueContext _context;

        public AdminRaceRegistrationsController(EliteRacingLeagueContext context)
        {
            _context = context;
        }

        private IQueryable<AdminRegistrationResponse> BuildRegistrationQuery()
        {
            return _context.RaceRegistrations
                .AsNoTracking()
                .Select(r => new AdminRegistrationResponse
                {
                    RegistrationId = r.RegistrationId,

                    RaceId = r.RaceId,
                    RaceName = r.Race.RaceName,
                    RaceDate = r.Race.RaceDate,
                    DistanceMeters = r.Race.DistanceMeters,
                    RaceStatus = r.Race.Status,

                    TournamentId = r.Race.TournamentId,
                    TournamentName = r.Race.Tournament.TournamentName,
                    TournamentLocation = r.Race.Tournament.Location,

                    HorseId = r.HorseId,
                    HorseName = r.Horse.HorseName,
                    BreedName = r.Horse.Breed.BreedName,
                    Age = r.Horse.Age,
                    HeightCm = r.Horse.HeightCm,
                    WeightKg = r.Horse.WeightKg,
                    HealthStatus = r.Horse.HealthStatus,
                    HorseIsActive = r.Horse.IsActive,
                    HorseImageUrl = r.Horse.ImageUrl,
                    HealthCertificateImageUrl = r.Horse.HealthCertificateImageUrl,

                    OwnerId = r.OwnerId,
                    OwnerName = r.Owner.Owner.FullName,
                    OwnerEmail = r.Owner.Owner.Email,
                    OwnerPhone = r.Owner.Owner.Phone,

                    JockeyId = r.JockeyId,
                    JockeyName = r.Jockey == null ? null : r.Jockey.JockeyNavigation.FullName,

                    Status = r.Status,
                    SubmittedAt = r.SubmittedAt,

                    ReviewedBy = r.ReviewedBy,
                    ReviewedByName = r.ReviewedByNavigation == null
                        ? null
                        : r.ReviewedByNavigation.FullName,
                    ReviewedAt = r.ReviewedAt,

                    JockeyConfirmedAt = r.JockeyConfirmedAt,
                    AdminNote = r.AdminNote
                });
        }

        [HttpGet]
        public async Task<IActionResult> GetRegistrations()
        {
            var registrations = await BuildRegistrationQuery()
                .OrderByDescending(r => r.SubmittedAt)
                .ToListAsync();

            return Ok(registrations);
        }

        [HttpGet("pending")]
        public async Task<IActionResult> GetPendingRegistrations()
        {
            var registrations = await BuildRegistrationQuery()
                .Where(r => r.Status == RaceRegistrationStatuses.Pending)
                .OrderByDescending(r => r.SubmittedAt)
                .ToListAsync();

            return Ok(registrations);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetRegistrationById(int id)
        {
            var registration = await BuildRegistrationQuery()
                .FirstOrDefaultAsync(r => r.RegistrationId == id);

            if (registration == null)
            {
                return NotFound(new AdminActionResponse
                {
                    Message = "Registration not found",
                    Id = id
                });
            }

            return Ok(registration);
        }

        [HttpPut("{id:int}/approve")]
        public async Task<IActionResult> ApproveRegistration(int id)
        {
            var registration = await _context.RaceRegistrations
                .Include(r => r.Horse)
                .Include(r => r.Race)
                    .ThenInclude(r => r.Tournament)
                .FirstOrDefaultAsync(r => r.RegistrationId == id);

            if (registration == null)
            {
                return NotFound(new AdminActionResponse
                {
                    Message = "Registration not found",
                    Id = id
                });
            }

            var adminId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var statusChanged = registration.Status != RaceRegistrationStatuses.Approved;

            registration.Status = RaceRegistrationStatuses.Approved;
            registration.ReviewedBy = adminId;
            registration.ReviewedAt = DateTime.UtcNow;
            registration.AdminNote = "Approved by admin";

            if (statusChanged)
            {
                var hasNames = !string.IsNullOrWhiteSpace(registration.Horse.HorseName) &&
                    !string.IsNullOrWhiteSpace(registration.Race.Tournament.TournamentName);

                _context.Notifications.Add(new Notification
                {
                    UserId = registration.OwnerId,
                    Title = "Registration Approved",
                    Message = hasNames
                        ? $"{registration.Horse.HorseName} registered for {registration.Race.Tournament.TournamentName} has been approved."
                        : "Your registration has been approved.",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                });
            }

            if (statusChanged)
            {
                _context.Notifications.Add(CreateOwnerNotification(
                    registration.OwnerId,
                    "Registration Approved",
                    $"{registration.Horse.HorseName} registered for {registration.Race.Tournament.TournamentName} has been approved."));
            }

            await _context.SaveChangesAsync();

            return Ok(new AdminActionResponse
            {
                Message = "Horse race registration approved successfully",
                Id = registration.RegistrationId,
                Status = registration.Status,
                Note = registration.AdminNote
            });
        }

        [HttpPut("{id:int}/reject")]
        public async Task<IActionResult> RejectRegistration(
    int id,
    [FromBody] AdminRejectRegistrationRequest? request)
        {
            var registration = await _context.RaceRegistrations
                .FirstOrDefaultAsync(r => r.RegistrationId == id);

            if (registration == null)
            {
                return NotFound(new AdminActionResponse
                {
                    Message = "Registration not found",
                    Id = id
                });
            }

            if (registration.Status != RaceRegistrationStatuses.Pending)
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "Only pending registrations can be rejected",
                    Id = id,
                    Status = registration.Status
                });
            }

            var adminIdText = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(adminIdText, out var adminId))
            {
                return Unauthorized(new AdminActionResponse
                {
                    Message = "Invalid admin token",
                    Id = id
                });
            }

            registration.Status = RaceRegistrationStatuses.Rejected;
            registration.ReviewedBy = adminId;
            registration.ReviewedAt = DateTime.UtcNow;
            registration.AdminNote = string.IsNullOrWhiteSpace(request?.AdminNote)
                ? "Rejected by admin"
                : request.AdminNote.Trim();

            await _context.SaveChangesAsync();

            return Ok(new AdminActionResponse
            {
                Message = "Horse race registration rejected successfully",
                Id = registration.RegistrationId,
                Status = registration.Status,
                Note = registration.AdminNote
            });
        }

        private static Notification CreateOwnerNotification(
            int ownerId,
            string title,
            string message)
        {
            return new Notification
            {
                UserId = ownerId,
                Title = title,
                Message = message,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };
        }
    }
}
