using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Eliteracingleague.API.Data;

namespace Eliteracingleague.API.Controllers
{
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
                .Select(r => new
                {
                    r.RegistrationId,
                    r.RaceId,
                    r.HorseId,
                    r.OwnerId,
                    r.JockeyId,
                    r.Status,
                    r.SubmittedAt,
                    r.ReviewedBy,
                    r.ReviewedAt,
                    r.AdminNote
                })
                .ToListAsync();

            return Ok(registrations);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetRegistrationById(int id)
        {
            var registration = await _context.RaceRegistrations
                .Where(r => r.RegistrationId == id)
                .Select(r => new
                {
                    r.RegistrationId,
                    r.RaceId,
                    r.HorseId,
                    r.OwnerId,
                    r.JockeyId,
                    r.Status,
                    r.SubmittedAt,
                    r.ReviewedBy,
                    r.ReviewedAt,
                    r.JockeyConfirmedAt,
                    r.AdminNote
                })
                .FirstOrDefaultAsync();

            if (registration == null)
                return NotFound(new { message = "Registration not found" });

            return Ok(registration);
        }

        [HttpPut("{id}/approve")]
        public async Task<IActionResult> ApproveRegistration(int id)
        {
            var registration = await _context.RaceRegistrations
                .FirstOrDefaultAsync(r => r.RegistrationId == id);

            if (registration == null)
                return NotFound(new { message = "Registration not found" });

            registration.Status = "Approved";
            registration.ReviewedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Registration approved successfully",
                registration.RegistrationId,
                registration.Status
            });
        }

        [HttpPut("{id}/reject")]
        public async Task<IActionResult> RejectRegistration(int id)
        {
            var registration = await _context.RaceRegistrations
                .FirstOrDefaultAsync(r => r.RegistrationId == id);

            if (registration == null)
                return NotFound(new { message = "Registration not found" });

            registration.Status = "Rejected";
            registration.ReviewedAt = DateTime.UtcNow;
            registration.AdminNote = "Rejected by admin";

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Registration rejected successfully",
                registration.RegistrationId,
                registration.Status,
                registration.AdminNote
            });
        }
    }
}