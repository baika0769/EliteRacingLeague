using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Admin;
using Microsoft.AspNetCore.Authorization;
using Eliteracingleague.API.Constants;
namespace Eliteracingleague.API.Controllers.Admin
{
    [Authorize(Roles = UserRoles.Admin)]
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
            var refereeReports = await _context.RefereeReports
                .AsNoTracking()
                .Select(r => new AdminReportResponse
                {
                    ReportId = r.ReportId,
                    Type = "RefereeReport",

                    RaceId = r.RaceId,
                    RaceName = r.Race.RaceName,

                    RefereeId = r.RefereeId,
                    RefereeName = r.Referee.Referee.FullName,

                    ReportContent = r.ReportContent,

                    SubmittedAt = r.SubmittedAt,
                    CreatedAt = r.SubmittedAt
                })
                .ToListAsync();

            var violationReports = await _context.RaceViolations
                .AsNoTracking()
                .Select(v => new AdminReportResponse
                {
                    ViolationId = v.ViolationId,
                    Type = "Violation",

                    RaceId = v.RaceId,
                    RaceName = v.Race.RaceName,

                    RegistrationId = v.RegistrationId,
                    HorseId = v.Registration.HorseId,
                    HorseName = v.Registration.Horse.HorseName,

                    RefereeId = v.RefereeId,
                    RefereeName = v.Referee.Referee.FullName,

                    ReportContent = v.Description,
                    ViolationType = v.ViolationType,
                    Description = v.Description,
                    PenaltyPoints = v.PenaltyPoints,
                    Action = v.Action,

                    SubmittedAt = v.CreatedAt,
                    CreatedAt = v.CreatedAt
                })
                .ToListAsync();

            var reports = refereeReports
                .Concat(violationReports)
                .OrderByDescending(r => r.SubmittedAt)
                .ToList();

            return Ok(reports);
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

            report.Action = RaceViolationActions.Warning;

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

            report.Action = RaceViolationActions.Disqualified;

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