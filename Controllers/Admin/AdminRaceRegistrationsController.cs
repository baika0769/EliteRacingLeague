using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Admin;
using Microsoft.AspNetCore.Authorization;
using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Extensions;
using Eliteracingleague.API.Services.Notifications;

namespace Eliteracingleague.API.Controllers.Admin
{
    [Authorize(Roles = UserRoles.Admin)]
    [ApiController]
    [Route("api/admin/registrations")]
    public class AdminRaceRegistrationsController : ControllerBase
    {
        private readonly EliteRacingLeagueContext _context;
        private readonly INotificationService _notificationService;

        public AdminRaceRegistrationsController(
            EliteRacingLeagueContext context,
            INotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
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
                .Include(r => r.Owner)
                    .ThenInclude(o => o.Owner)
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

            if (!User.TryGetUserId(out var adminId))
            {
                return Unauthorized(new AdminActionResponse
                {
                    Message = "Invalid admin token",
                    Id = id
                });
            }

            if (registration.Status != RaceRegistrationStatuses.Pending)
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "Only pending registrations can be approved",
                    Id = id,
                    Status = registration.Status
                });
            }

            var now = DateTime.UtcNow;
            var today = DateOnly.FromDateTime(now);
            var race = registration.Race;
            var tournament = race.Tournament;

            if (tournament.Status == TournamentStatuses.Cancelled || race.Status == RaceStatuses.Cancelled)
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "Cancelled race or tournament cannot accept registrations",
                    Id = id,
                    Status = registration.Status
                });
            }

            if (tournament.Status != TournamentStatuses.OpenRegistration)
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "Registration can only be approved while tournament is OpenRegistration",
                    Id = id,
                    Status = tournament.Status
                });
            }

            if (!RaceStatuses.CanRegister(race.Status))
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "Registration can no longer be approved for this race status",
                    Id = id,
                    Status = race.Status
                });
            }

            if (tournament.StartDate < today)
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "Registration deadline has passed",
                    Id = id,
                    Status = tournament.Status
                });
            }

            if (!registration.Horse.IsActive || !HorseHealthStatuses.CanRace(registration.Horse.HealthStatus))
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "Horse is not active or not healthy enough to race",
                    Id = id,
                    Status = registration.Horse.HealthStatus
                });
            }

            if (!registration.Owner.IsActive || registration.Owner.Owner.Status != UserStatuses.Active)
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "Owner account is not active",
                    Id = id,
                    Status = registration.Owner.Owner.Status
                });
            }

            var currentApprovedCount = await _context.RaceRegistrations
                .CountAsync(r =>
                    r.RaceId == race.RaceId &&
                    r.RegistrationId != registration.RegistrationId &&
                    (r.Status == RaceRegistrationStatuses.Approved ||
                     r.Status == RaceRegistrationStatuses.JockeyInvited ||
                     r.Status == RaceRegistrationStatuses.ReadyToRace));

            if (currentApprovedCount >= race.MaxHorses)
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "Race is already full",
                    Id = id,
                    Status = race.Status
                });
            }

            var statusChanged = registration.Status != RaceRegistrationStatuses.Approved;

            registration.Status = RaceRegistrationStatuses.Approved;
            registration.ReviewedBy = adminId;
            registration.ReviewedAt = now;
            registration.AdminNote = "Approved by admin";

            if (statusChanged)
            {
                var hasNames = !string.IsNullOrWhiteSpace(registration.Horse.HorseName) &&
                    !string.IsNullOrWhiteSpace(registration.Race.Tournament.TournamentName);

                await _notificationService.CreateForUserAsync(
                    registration.OwnerId,
                    "Registration Approved",
                    hasNames
                        ? $"{registration.Horse.HorseName} registered for {registration.Race.Tournament.TournamentName} has been approved."
                        : "Your registration has been approved.",
                    "JockeyAssignment",
                    $"/owner/jockey-assignment/{registration.RegistrationId}",
                    "RaceRegistration",
                    registration.RegistrationId);
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
                .Include(r => r.Horse)
                .Include(r => r.Owner)
                    .ThenInclude(o => o.Owner)
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

            if (registration.Status != RaceRegistrationStatuses.Pending)
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "Only pending registrations can be rejected",
                    Id = id,
                    Status = registration.Status
                });
            }

            if (!User.TryGetUserId(out var adminId))
            {
                return Unauthorized(new AdminActionResponse
                {
                    Message = "Invalid admin token",
                    Id = id
                });
            }

            var now = DateTime.UtcNow;

            registration.Status = RaceRegistrationStatuses.Rejected;
            registration.ReviewedBy = adminId;
            registration.ReviewedAt = now;
            registration.AdminNote = string.IsNullOrWhiteSpace(request?.AdminNote)
                ? "Rejected by admin"
                : request.AdminNote.Trim();

            var hasNames = !string.IsNullOrWhiteSpace(registration.Horse.HorseName) &&
                !string.IsNullOrWhiteSpace(registration.Race.Tournament.TournamentName);

            await _notificationService.CreateForUserAsync(
                registration.OwnerId,
                "Registration Rejected",
                hasNames
                    ? $"{registration.Horse.HorseName} registered for {registration.Race.Tournament.TournamentName} has been rejected."
                    : "Your registration has been rejected.",
                "RegistrationDetail",
                $"/owner/registrations/{registration.RegistrationId}",
                "RaceRegistration",
                registration.RegistrationId);

            await _context.SaveChangesAsync();

            return Ok(new AdminActionResponse
            {
                Message = "Horse race registration rejected successfully",
                Id = registration.RegistrationId,
                Status = registration.Status,
                Note = registration.AdminNote
            });
        }

    }
}
