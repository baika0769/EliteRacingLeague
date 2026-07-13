using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Admin;
using Eliteracingleague.API.Services.SystemTime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Controllers.Admin
{
    [Authorize(Roles = UserRoles.Admin)]
    [ApiController]
    [Route("api/admin/reports")]
    public class AdminReportsController : ControllerBase
    {
        private readonly EliteRacingLeagueContext _context;
        private readonly IDateTimeProvider _dateTimeProvider;

        public AdminReportsController(
            EliteRacingLeagueContext context,
            IDateTimeProvider dateTimeProvider)
        {
            _context = context;
            _dateTimeProvider = dateTimeProvider;
        }

        private IQueryable<AdminReportResponse> BuildRefereeReportQuery()
        {
            return _context.RefereeReports
                .AsNoTracking()
                .Select(r => new AdminReportResponse
                {
                    ReportId = r.ReportId,
                    Type = "RefereeReport",
                    ReportType = r.ReportType,

                    RaceId = r.RaceId,
                    RaceName = r.Race.RaceName,
                    TournamentId = r.Race.TournamentId,
                    TournamentName = r.Race.Tournament.TournamentName,

                    RefereeId = r.RefereeId,
                    RefereeName = r.Referee.Referee.FullName,

                    ReportContent = r.ReportContent,
                    SubmittedAt = r.SubmittedAt,
                    CreatedAt = r.SubmittedAt
                });
        }

        private IQueryable<AdminReportResponse> BuildViolationReportQuery()
        {
            return _context.RaceViolations
                .AsNoTracking()
                .Select(v => new AdminReportResponse
                {
                    ViolationId = v.ViolationId,
                    Type = "Violation",
                    ReportType = "Violation",

                    RaceId = v.RaceId,
                    RaceName = v.Race.RaceName,
                    TournamentId = v.Race.TournamentId,
                    TournamentName = v.Race.Tournament.TournamentName,

                    RegistrationId = v.RegistrationId,
                    RegistrationStatus = v.Registration.Status,
                    HorseId = v.Registration.HorseId,
                    HorseName = v.Registration.Horse.HorseName,
                    JockeyId = v.Registration.JockeyId,
                    JockeyName = v.Registration.Jockey == null
                        ? null
                        : v.Registration.Jockey.JockeyNavigation.FullName,

                    RefereeId = v.RefereeId,
                    RefereeName = v.Referee.Referee.FullName,

                    ReportContent = v.Description,
                    ViolationType = v.ViolationType,
                    Description = v.Description,
                    PenaltyPoints = v.PenaltyPoints,
                    Action = v.Action,

                    SubmittedAt = v.CreatedAt,
                    CreatedAt = v.CreatedAt
                });
        }

        private (DateTime UtcStart, DateTime UtcEnd) GetCurrentLocalDayUtcRange()
        {
            var localNow = _dateTimeProvider.GetLocalNow(_dateTimeProvider.TimeZoneId);
            var timeZone = SystemDateTimeProvider.ResolveTimeZone(
                _dateTimeProvider.TimeZoneId);

            var localStart = DateTime.SpecifyKind(
                localNow.Date,
                DateTimeKind.Unspecified);
            var localEnd = localStart.AddDays(1);

            return (
                TimeZoneInfo.ConvertTimeToUtc(localStart, timeZone),
                TimeZoneInfo.ConvertTimeToUtc(localEnd, timeZone));
        }

        [HttpGet]
        public async Task<IActionResult> GetReports()
        {
            var refereeReports = await BuildRefereeReportQuery().ToListAsync();
            var violationReports = await BuildViolationReportQuery().ToListAsync();

            var reports = refereeReports
                .Concat(violationReports)
                .OrderByDescending(r => r.SubmittedAt)
                .ToList();

            return Ok(reports);
        }

        [HttpGet("{idOrSlug}")]
        public async Task<IActionResult> GetReportById(string idOrSlug)
        {
            var value = (idOrSlug ?? string.Empty).Trim();

            if (value.Equals("today", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("statistics", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(new { message = "Report not found." });
            }

            var isRefereeReport = value.StartsWith(
                "report-",
                StringComparison.OrdinalIgnoreCase);
            var isViolation = value.StartsWith(
                "violation-",
                StringComparison.OrdinalIgnoreCase);

            var numericText = value
                .Replace("report-", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("violation-", string.Empty, StringComparison.OrdinalIgnoreCase);

            if (!int.TryParse(numericText, out var id) || id <= 0)
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "Invalid report id",
                    Id = 0
                });
            }

            if (isRefereeReport)
            {
                var report = await BuildRefereeReportQuery()
                    .FirstOrDefaultAsync(r => r.ReportId == id);

                return report == null
                    ? NotFound(new AdminActionResponse
                    {
                        Message = "Referee report not found",
                        Id = id
                    })
                    : Ok(report);
            }

            if (isViolation)
            {
                var violation = await BuildViolationReportQuery()
                    .FirstOrDefaultAsync(r => r.ViolationId == id);

                return violation == null
                    ? NotFound(new AdminActionResponse
                    {
                        Message = "Violation report not found",
                        Id = id
                    })
                    : Ok(violation);
            }

            var refereeReport = await BuildRefereeReportQuery()
                .FirstOrDefaultAsync(r => r.ReportId == id);

            if (refereeReport != null)
            {
                return Ok(refereeReport);
            }

            var violationReport = await BuildViolationReportQuery()
                .FirstOrDefaultAsync(r => r.ViolationId == id);

            if (violationReport != null)
            {
                return Ok(violationReport);
            }

            return NotFound(new AdminActionResponse
            {
                Message = "Report not found",
                Id = id
            });
        }

        [HttpGet("today")]
        public async Task<IActionResult> GetReportsToday()
        {
            var (utcStart, utcEnd) = GetCurrentLocalDayUtcRange();

            var refereeReports = await BuildRefereeReportQuery()
                .Where(r =>
                    r.SubmittedAt >= utcStart &&
                    r.SubmittedAt < utcEnd)
                .ToListAsync();

            var violationReports = await BuildViolationReportQuery()
                .Where(r =>
                    r.SubmittedAt >= utcStart &&
                    r.SubmittedAt < utcEnd)
                .ToListAsync();

            var reports = refereeReports
                .Concat(violationReports)
                .OrderByDescending(r => r.SubmittedAt)
                .ToList();

            return Ok(reports);
        }

        [HttpGet("statistics")]
        public async Task<IActionResult> GetReportStatistics()
        {
            var (utcStart, utcEnd) = GetCurrentLocalDayUtcRange();

            var refereeReportCount = await _context.RefereeReports.CountAsync();
            var violationReportCount = await _context.RaceViolations.CountAsync();

            var refereeReportsToday = await _context.RefereeReports.CountAsync(r =>
                r.SubmittedAt >= utcStart &&
                r.SubmittedAt < utcEnd);

            var violationReportsToday = await _context.RaceViolations.CountAsync(r =>
                r.CreatedAt >= utcStart &&
                r.CreatedAt < utcEnd);

            var pendingViolationReports = await _context.RaceViolations
                .CountAsync(r => r.Action == null);

            return Ok(new
            {
                totalReports = refereeReportCount + violationReportCount,
                reportsToday = refereeReportsToday + violationReportsToday,
                pendingReports = pendingViolationReports,
                refereeReportCount,
                violationReportCount,
                refereeReportsToday,
                violationReportsToday
            });
        }

        [HttpPut("{id:int}/resolve")]
        public async Task<IActionResult> ResolveReport(int id)
        {
            var report = await _context.RaceViolations
                .FirstOrDefaultAsync(r => r.ViolationId == id);

            if (report == null)
            {
                return NotFound(new AdminActionResponse
                {
                    Message = "Violation report not found",
                    Id = id
                });
            }

            if (report.Action == null)
            {
                report.Action = RaceViolationActions.Warning;
                await _context.SaveChangesAsync();
            }

            return Ok(new AdminActionResponse
            {
                Message = "Report resolved successfully",
                Id = report.ViolationId,
                Status = report.Action
            });
        }

        [HttpPut("{id:int}/reject")]
        public async Task<IActionResult> RejectReport(int id)
        {
            var report = await _context.RaceViolations
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.ViolationId == id);

            if (report == null)
            {
                return NotFound(new AdminActionResponse
                {
                    Message = "Violation report not found",
                    Id = id
                });
            }

            return BadRequest(new
            {
                message = "Rejecting a report is not supported safely because RaceViolation has no separate review status. Add a ReviewStatus field before enabling this action.",
                violationId = report.ViolationId,
                currentAction = report.Action
            });
        }

        [HttpDelete("{idOrSlug}")]
        public async Task<IActionResult> DeleteReport(string idOrSlug)
        {
            var value = (idOrSlug ?? string.Empty).Trim();

            var isRefereeReport = value.StartsWith(
                "report-",
                StringComparison.OrdinalIgnoreCase);
            var isViolation = value.StartsWith(
                "violation-",
                StringComparison.OrdinalIgnoreCase);

            var numericText = value
                .Replace("report-", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("violation-", string.Empty, StringComparison.OrdinalIgnoreCase);

            if (!int.TryParse(numericText, out var id) || id <= 0)
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "Invalid report id",
                    Id = 0
                });
            }

            if (isRefereeReport)
            {
                var refereeReport = await _context.RefereeReports
                    .FirstOrDefaultAsync(r => r.ReportId == id);

                if (refereeReport == null)
                {
                    return NotFound(new AdminActionResponse
                    {
                        Message = "Referee report not found",
                        Id = id
                    });
                }

                _context.RefereeReports.Remove(refereeReport);
                await _context.SaveChangesAsync();

                return Ok(new AdminActionResponse
                {
                    Message = "Referee report deleted successfully",
                    Id = id
                });
            }

            if (isViolation)
            {
                var violation = await _context.RaceViolations
                    .FirstOrDefaultAsync(r => r.ViolationId == id);

                if (violation == null)
                {
                    return NotFound(new AdminActionResponse
                    {
                        Message = "Violation report not found",
                        Id = id
                    });
                }

                _context.RaceViolations.Remove(violation);
                await _context.SaveChangesAsync();

                return Ok(new AdminActionResponse
                {
                    Message = "Violation report deleted successfully",
                    Id = id
                });
            }

            return BadRequest(new AdminActionResponse
            {
                Message = "Report type is required. Use report-{id} or violation-{id}.",
                Id = id
            });
        }
    }
}