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
                .Select(r => new AdminReportResponse
                {
                    ViolationId = r.ViolationId,
                    RaceId = r.RaceId,
                    RegistrationId = r.RegistrationId,
                    RefereeId = r.RefereeId,
                    ViolationType = r.ViolationType,
                    Description = r.Description,
                    PenaltyPoints = r.PenaltyPoints,
                    Action = r.Action,
                    CreatedAt = r.CreatedAt
                })
                .ToListAsync();

            return Ok(reports);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetReportById(int id)
        {
            var report = await _context.RaceViolations
                .Where(r => r.ViolationId == id)
                .Select(r => new AdminReportResponse
                {
                    ViolationId = r.ViolationId,
                    RaceId = r.RaceId,
                    RegistrationId = r.RegistrationId,
                    RefereeId = r.RefereeId,
                    ViolationType = r.ViolationType,
                    Description = r.Description,
                    PenaltyPoints = r.PenaltyPoints,
                    Action = r.Action,
                    CreatedAt = r.CreatedAt
                })
                .FirstOrDefaultAsync();

            if (report == null)
            {
                return NotFound(new AdminActionResponse
                {
                    Message = "Report not found",
                    Id = id
                });
            }

            return Ok(report);
        }

        [HttpGet("today")]
        public async Task<IActionResult> GetReportsToday()
        {
            var today = DateTime.UtcNow.Date;

            var reports = await _context.RaceViolations
                .Where(r => r.CreatedAt.Date == today)
                .Select(r => new AdminReportResponse
                {
                    ViolationId = r.ViolationId,
                    RaceId = r.RaceId,
                    RegistrationId = r.RegistrationId,
                    RefereeId = r.RefereeId,
                    ViolationType = r.ViolationType,
                    Description = r.Description,
                    PenaltyPoints = r.PenaltyPoints,
                    Action = r.Action,
                    CreatedAt = r.CreatedAt
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
                .CountAsync(r => r.Action == null);

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
            {
                return NotFound(new AdminActionResponse
                {
                    Message = "Report not found",
                    Id = id
                });
            }

            report.Action = "Warning";

            await _context.SaveChangesAsync();

            return Ok(new AdminActionResponse
            {
                Message = "Report resolved successfully",
                Id = report.ViolationId,
                Status = report.Action
            });
        }

        [HttpPut("{id}/reject")]
        public async Task<IActionResult> RejectReport(int id)
        {
            var report = await _context.RaceViolations
                .FirstOrDefaultAsync(r => r.ViolationId == id);

            if (report == null)
            {
                return NotFound(new AdminActionResponse
                {
                    Message = "Report not found",
                    Id = id
                });
            }

            report.Action = "Disqualified";

            await _context.SaveChangesAsync();

            return Ok(new AdminActionResponse
            {
                Message = "Report rejected successfully",
                Id = report.ViolationId,
                Status = report.Action
            });
        }
    }

}