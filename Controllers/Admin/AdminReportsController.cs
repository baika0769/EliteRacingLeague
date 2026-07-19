using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Admin;
using Eliteracingleague.API.Extensions;
using Eliteracingleague.API.Services.Auditing;
using Eliteracingleague.API.Services.Notifications;
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
        private readonly INotificationService _notificationService;
        private readonly IAuditService _auditService;

        public AdminReportsController(
            EliteRacingLeagueContext context,
            IDateTimeProvider dateTimeProvider,
            INotificationService notificationService,
            IAuditService auditService)
        {
            _context = context;
            _dateTimeProvider = dateTimeProvider;
            _notificationService = notificationService;
            _auditService = auditService;
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
                    Status = r.Status,
                    RevisionNumber = r.RevisionNumber,
                    ReturnReasonCategory = r.ReturnReasonCategory,
                    ReturnReason = r.ReturnReason,
                    ReviewedByAdminId = r.ReviewedByAdminId,
                    ReviewedByAdminName = r.ReviewedByAdmin == null
                        ? null
                        : r.ReviewedByAdmin.FullName,
                    ReviewedAt = r.ReviewedAt,
                    ResubmittedAt = r.ResubmittedAt,
                    UpdatedAt = r.UpdatedAt,
                    CanApprove = r.ReportType == RefereeReportTypes.PostRace &&
                        r.Status == RefereeReportStatuses.Submitted,
                    CanReturn = r.ReportType == RefereeReportTypes.PostRace &&
                        r.Status == RefereeReportStatuses.Submitted,

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

            var submittedRefereeReports = await _context.RefereeReports
                .CountAsync(r => r.Status == RefereeReportStatuses.Submitted);
            var returnedRefereeReports = await _context.RefereeReports
                .CountAsync(r => r.Status == RefereeReportStatuses.Returned);
            var approvedRefereeReports = await _context.RefereeReports
                .CountAsync(r => r.Status == RefereeReportStatuses.Approved);
            var pendingViolationReports = await _context.RaceViolations
                .CountAsync(r => r.Action == null);

            return Ok(new
            {
                totalReports = refereeReportCount + violationReportCount,
                reportsToday = refereeReportsToday + violationReportsToday,
                pendingReports = submittedRefereeReports + pendingViolationReports,
                refereeReportCount,
                violationReportCount,
                refereeReportsToday,
                violationReportsToday,
                submittedRefereeReports,
                returnedRefereeReports,
                approvedRefereeReports,
                pendingViolationReports
            });
        }

        [HttpPut("{id:int}/return")]
        public async Task<IActionResult> ReturnRefereeReport(
            int id,
            ReturnRefereeReportRequest request,
            CancellationToken cancellationToken)
        {
            if (!User.TryGetUserId(out var adminId))
            {
                return Unauthorized();
            }

            var category = request.ReasonCategory?.Trim();
            var reason = request.Reason?.Trim();

            if (!RefereeReportReturnReasonCategories.IsValid(category))
            {
                return BadRequest(new
                {
                    code = "INVALID_RETURN_REASON_CATEGORY",
                    message = "Invalid return reason category.",
                    allowedCategories = RefereeReportReturnReasonCategories.All.OrderBy(x => x)
                });
            }

            if (string.IsNullOrWhiteSpace(reason) || reason.Length < 10 || reason.Length > 1000)
            {
                return BadRequest(new
                {
                    code = "INVALID_RETURN_REASON",
                    message = "Return reason is required and must contain from 10 to 1000 characters."
                });
            }

            var report = await _context.RefereeReports
                .Include(r => r.Race)
                .FirstOrDefaultAsync(r => r.ReportId == id, cancellationToken);

            if (report == null)
            {
                return NotFound(new AdminActionResponse
                {
                    Message = "Referee report not found",
                    Id = id
                });
            }

            if (report.ReportType != RefereeReportTypes.PostRace)
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "Only the final post-race report supports the return-for-revision workflow.",
                    Id = id,
                    Status = report.Status
                });
            }

            if (report.Race.Status is not RaceStatuses.Finished and not RaceStatuses.ResultPending)
            {
                return Conflict(new AdminActionResponse
                {
                    Message = "The final report can only be returned before race results are published or the race is cancelled.",
                    Id = id,
                    Status = report.Race.Status
                });
            }

            if (report.Status != RefereeReportStatuses.Submitted)
            {
                return Conflict(new AdminActionResponse
                {
                    Message = report.Status == RefereeReportStatuses.Returned
                        ? "This report has already been returned and is waiting for the referee to resubmit it."
                        : "Only a submitted report can be returned for revision.",
                    Id = id,
                    Status = report.Status
                });
            }

            var now = _dateTimeProvider.UtcNow;
            var oldStatus = report.Status;

            report.Status = RefereeReportStatuses.Returned;
            report.ReturnReasonCategory = category;
            report.ReturnReason = reason;
            report.ReviewedByAdminId = adminId;
            report.ReviewedAt = now;
            report.UpdatedAt = now;

            await _auditService.WriteAsync(
                adminId,
                AuditActionTypes.Reject,
                "RefereeReport",
                report.ReportId.ToString(),
                new { Status = oldStatus, report.RevisionNumber },
                new
                {
                    Status = report.Status,
                    report.ReturnReasonCategory,
                    report.ReturnReason,
                    report.RevisionNumber
                },
                reason,
                cancellationToken);

            await _notificationService.CreateForUserAsync(
                report.RefereeId,
                "Final report returned for revision",
                $"Your final report for race {report.Race.RaceName} was returned. Reason: {reason}",
                "PostRaceReportReturned",
                $"/referee/races/post-race?raceId={report.RaceId}&reportId={report.ReportId}",
                "RefereeReport",
                report.ReportId,
                cancellationToken,
                preventDuplicates: false);

            await _context.SaveChangesAsync(cancellationToken);

            return Ok(new
            {
                message = "The final report was returned to the referee for revision.",
                reportId = report.ReportId,
                status = report.Status,
                returnReasonCategory = report.ReturnReasonCategory,
                returnReason = report.ReturnReason,
                reviewedAt = report.ReviewedAt,
                canEditByReferee = true
            });
        }

        [HttpPut("{id:int}/approve")]
        public async Task<IActionResult> ApproveRefereeReport(
            int id,
            CancellationToken cancellationToken)
        {
            if (!User.TryGetUserId(out var adminId))
            {
                return Unauthorized();
            }

            var report = await _context.RefereeReports
                .Include(r => r.Race)
                .FirstOrDefaultAsync(r => r.ReportId == id, cancellationToken);

            if (report == null)
            {
                return NotFound(new AdminActionResponse
                {
                    Message = "Referee report not found",
                    Id = id
                });
            }

            if (report.ReportType != RefereeReportTypes.PostRace)
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "Only the final post-race report supports this approval workflow.",
                    Id = id,
                    Status = report.Status
                });
            }

            if (report.Race.Status is not RaceStatuses.Finished and not RaceStatuses.ResultPending)
            {
                return Conflict(new AdminActionResponse
                {
                    Message = "The final report can only be approved before race results are published or the race is cancelled.",
                    Id = id,
                    Status = report.Race.Status
                });
            }

            if (report.Status != RefereeReportStatuses.Submitted)
            {
                return Conflict(new AdminActionResponse
                {
                    Message = report.Status == RefereeReportStatuses.Returned
                        ? "The referee must resubmit the returned report before it can be approved."
                        : "Only a submitted report can be approved.",
                    Id = id,
                    Status = report.Status
                });
            }

            var now = _dateTimeProvider.UtcNow;
            var oldStatus = report.Status;

            report.Status = RefereeReportStatuses.Approved;
            report.ReviewedByAdminId = adminId;
            report.ReviewedAt = now;
            report.UpdatedAt = now;

            await _auditService.WriteAsync(
                adminId,
                AuditActionTypes.Approve,
                "RefereeReport",
                report.ReportId.ToString(),
                new { Status = oldStatus, report.RevisionNumber },
                new { Status = report.Status, report.RevisionNumber },
                "Final referee report approved",
                cancellationToken);

            await _notificationService.CreateForUserAsync(
                report.RefereeId,
                "Final report approved",
                $"Your final report for race {report.Race.RaceName} was approved by the admin.",
                "PostRaceReportApproved",
                $"/referee/races/post-race?raceId={report.RaceId}&reportId={report.ReportId}",
                "RefereeReport",
                report.ReportId,
                cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);

            return Ok(new
            {
                message = "The final report was approved successfully.",
                reportId = report.ReportId,
                status = report.Status,
                reviewedAt = report.ReviewedAt,
                canEditByReferee = false
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
                message = "Rejecting a violation report is not supported safely because RaceViolation has no separate review status.",
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
                return BadRequest(new AdminActionResponse
                {
                    Message = "Submitted referee reports cannot be deleted. Return the report for revision or approve it to preserve audit history.",
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
