using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Admin;
namespace Eliteracingleague.API.Controllers.Admin
{
    [ApiController]
    [Route("api/admin/reports")]
    public class AdminReportsController : ControllerBase
    {
        private readonly EliteRacingLeagueContext _context;

        public AdminReportsController(EliteRacingLeagueContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetReports()
        {
            var reports = await _context.RaceViolations
                .Select(r => new
                {
                    r.ViolationId,
                    r.RaceId,
                    r.RegistrationId,
                    r.RefereeId,
                    r.ViolationType,
                    r.Description,
                    r.PenaltyPoints,
                    r.Action,
                    r.CreatedAt
                })
                .ToListAsync();

            return Ok(reports);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetReportById(int id)
        {
            var report = await _context.RaceViolations
                .Where(r => r.ViolationId == id)
                .Select(r => new
                {
                    r.ViolationId,
                    r.RaceId,
                    r.RegistrationId,
                    r.RefereeId,
                    r.ViolationType,
                    r.Description,
                    r.PenaltyPoints,
                    r.Action,
                    r.CreatedAt
                })
                .FirstOrDefaultAsync();

            if (report == null)
                return NotFound(new { message = "Report not found" });

            return Ok(report);
        }

        [HttpGet("today")]
        public async Task<IActionResult> GetReportsToday()
        {
            var today = DateTime.UtcNow.Date;

            var reports = await _context.RaceViolations
                .Where(r => r.CreatedAt.Date == today)
                .Select(r => new
                {
                    r.ViolationId,
                    r.RaceId,
                    r.RegistrationId,
                    r.RefereeId,
                    r.ViolationType,
                    r.Description,
                    r.PenaltyPoints,
                    r.Action,
                    r.CreatedAt
                })
                .ToListAsync();

            return Ok(reports);
        }

        [HttpGet("statistics")]
        public async Task<IActionResult> GetReportStatistics()
        {
            var today = DateTime.UtcNow.Date;

            var totalReports = await _context.RaceViolations.CountAsync();

            var reportsToday = await _context.RaceViolations
                .CountAsync(r => r.CreatedAt.Date == today);

            var pendingReports = await _context.RaceViolations
                .CountAsync(r => r.Action == null || r.Action == "Pending");

            return Ok(new
            {
                totalReports,
                reportsToday,
                pendingReports
            });
        }

        [HttpPut("{id}/resolve")]
        public async Task<IActionResult> ResolveReport(int id)
        {
            var report = await _context.RaceViolations
                .FirstOrDefaultAsync(r => r.ViolationId == id);

            if (report == null)
                return NotFound(new { message = "Report not found" });

            report.Action = "Resolved";

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Report resolved successfully",
                report.ViolationId,
                report.Action
            });
        }

        [HttpPut("{id}/reject")]
        public async Task<IActionResult> RejectReport(int id)
        {
            var report = await _context.RaceViolations
                .FirstOrDefaultAsync(r => r.ViolationId == id);

            if (report == null)
                return NotFound(new { message = "Report not found" });

            report.Action = "Rejected";

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Report rejected successfully",
                report.ViolationId,
                report.Action
            });
        }
    }
}