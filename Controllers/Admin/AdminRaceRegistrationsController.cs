using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Admin;
using Microsoft.AspNetCore.Authorization;
using Eliteracingleague.API.Constants;
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

            registration.Status = RaceRegistrationStatuses.Approved;
            registration.ReviewedBy = adminId;
            registration.ReviewedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new AdminActionResponse
            {
                Message = "Registration approved successfully",
                Id = registration.RegistrationId,
                Status = registration.Status
            });
        }

        [HttpPut("{id}/reject")]
        public async Task<IActionResult> RejectRegistration(int id)
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

            registration.Status = RaceRegistrationStatuses.Rejected;
            registration.ReviewedAt = DateTime.UtcNow;
            registration.AdminNote = "Rejected by admin";

            await _context.SaveChangesAsync();

            return Ok(new AdminActionResponse
            {
                Message = "Registration rejected successfully",
                Id = registration.RegistrationId,
                Status = registration.Status
            });
        }
    }
}