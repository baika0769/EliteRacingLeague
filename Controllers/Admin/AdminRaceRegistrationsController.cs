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

        [HttpGet]
        public async Task<IActionResult> GetRegistrations()
        {
            var registrations = await _context.RaceRegistrations
                .Select(r => new AdminRegistrationResponse
                {
                    RegistrationId = r.RegistrationId,
                    RaceId = r.RaceId,
                    HorseId = r.HorseId,
                    OwnerId = r.OwnerId,
                    JockeyId = r.JockeyId,
                    Status = r.Status,
                    SubmittedAt = r.SubmittedAt,
                    ReviewedBy = r.ReviewedBy,
                    ReviewedAt = r.ReviewedAt,
                    JockeyConfirmedAt = r.JockeyConfirmedAt,
                    AdminNote = r.AdminNote
                })
                .ToListAsync();

            return Ok(registrations);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetRegistrationById(int id)
        {
            var registration = await _context.RaceRegistrations
                .Where(r => r.RegistrationId == id)
                .Select(r => new AdminRegistrationResponse
                {
                    RegistrationId = r.RegistrationId,
                    RaceId = r.RaceId,
                    HorseId = r.HorseId,
                    OwnerId = r.OwnerId,
                    JockeyId = r.JockeyId,
                    Status = r.Status,
                    SubmittedAt = r.SubmittedAt,
                    ReviewedBy = r.ReviewedBy,
                    ReviewedAt = r.ReviewedAt,
                    JockeyConfirmedAt = r.JockeyConfirmedAt,
                    AdminNote = r.AdminNote
                })
                .FirstOrDefaultAsync();

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

        [HttpPut("{id}/approve")]
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
                Message = "Registration approved successfully",
                Id = registration.RegistrationId,
                Status = registration.Status
            });
        }

        [HttpGet("pending")]
        public async Task<IActionResult> GetPendingRegistrations()
        {
            var registrations = await _context.RaceRegistrations
                .Where(r => r.Status == RaceRegistrationStatuses.Pending)
                .Select(r => new AdminRegistrationResponse
                {
                    RegistrationId = r.RegistrationId,
                    RaceId = r.RaceId,
                    HorseId = r.HorseId,
                    OwnerId = r.OwnerId,
                    JockeyId = r.JockeyId,
                    Status = r.Status,
                    SubmittedAt = r.SubmittedAt,
                    ReviewedBy = r.ReviewedBy,
                    ReviewedAt = r.ReviewedAt,
                    JockeyConfirmedAt = r.JockeyConfirmedAt,
                    AdminNote = r.AdminNote
                })
                .ToListAsync();

            return Ok(registrations);
        }


        [HttpPut("{id}/reject")]
        public async Task<IActionResult> RejectRegistration(int id)
        {
            var registration = await _context.RaceRegistrations
                .Include(r => r.Horse)
                .FirstOrDefaultAsync(r => r.RegistrationId == id);

            if (registration == null)
            {
                return NotFound(new AdminActionResponse
                {
                    Message = "Registration not found",
                    Id = id
                });
            }

            var statusChanged = registration.Status != RaceRegistrationStatuses.Rejected;

            registration.Status = RaceRegistrationStatuses.Rejected;
            registration.ReviewedAt = DateTime.UtcNow;
            registration.AdminNote = "Rejected by admin";

            if (statusChanged)
            {
                _context.Notifications.Add(CreateOwnerNotification(
                    registration.OwnerId,
                    "Registration Rejected",
                    $"Your registration for {registration.Horse.HorseName} has been rejected."));
            }

            await _context.SaveChangesAsync();

            return Ok(new AdminActionResponse
            {
                Message = "Registration rejected successfully",
                Id = registration.RegistrationId,
                Status = registration.Status
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
