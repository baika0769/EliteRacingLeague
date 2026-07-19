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
                        (r.Status == RefereeReportStatuses.Submitted ||
                         (r.Status == RefereeReportStatuses.Approved &&
                          r.Race.Status != RaceStatuses.Published)),

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

            if (report.Race.Status is RaceStatuses.Published or RaceStatuses.Cancelled)
            {
                return Conflict(new AdminActionResponse
                {
                    Message = "Published or cancelled race results cannot be returned through this workflow.",
                    Id = id,
                    Status = report.Race.Status
                });
            }

            if (report.Status == RefereeReportStatuses.Returned)
            {
                return Ok(new
                {
                    message = "This submission has already been returned to the referee.",
                    reportId = report.ReportId,
                    status = report.Status,
                    canEditByReferee = true
                });
            }

            if (report.Status is not RefereeReportStatuses.Submitted and
                not RefereeReportStatuses.Approved)
            {
                return Conflict(new AdminActionResponse
                {
                    Message = "Only a submitted report can be returned for revision.",
                    Id = id,
                    Status = report.Status
                });
            }

            var results = await _context.RaceResults
                .Where(r => r.RaceId == report.RaceId)
                .ToListAsync(cancellationToken);

            var lockedResult = results.FirstOrDefault(r =>
                r.Status is RaceResultStatuses.AdminApproved or RaceResultStatuses.Published);

            if (lockedResult != null)
            {
                return Conflict(new
                {
                    code = "PUBLISHED_RESULT_REQUIRES_CORRECTION_WORKFLOW",
                    message = "This race already has an admin-approved or published result. Use the result-correction workflow instead.",
                    resultId = lockedResult.ResultId,
                    resultStatus = lockedResult.Status
                });
            }

            var now = _dateTimeProvider.UtcNow;
            var oldReportStatus = report.Status;
            var oldRaceStatus = report.Race.Status;
            var oldResultStatuses = results
                .Select(r => new { r.ResultId, r.Status })
                .ToList();

            await using var transaction = await _context.Database
                .BeginTransactionAsync(cancellationToken);

            try
            {
                report.Status = RefereeReportStatuses.Returned;
                report.ReturnReasonCategory = category;
                report.ReturnReason = reason;
                report.ReviewedByAdminId = adminId;
                report.ReviewedAt = now;
                report.UpdatedAt = now;

                foreach (var result in results)
                {
                    result.Status = RaceResultStatuses.Returned;
                    result.AdminConfirmedBy = null;
                    result.PublishedAt = null;
                    result.UpdatedAt = now;
                }

                report.Race.Status = RaceStatuses.Finished;
                report.Race.UpdatedAt = now;

                await _auditService.WriteAsync(
                    adminId,
                    AuditActionTypes.Reject,
                    "RefereeReport",
                    report.ReportId.ToString(),
                    new
                    {
                        ReportStatus = oldReportStatus,
                        RaceStatus = oldRaceStatus,
                        ResultStatuses = oldResultStatuses
                    },
                    new
                    {
                        ReportStatus = report.Status,
                        RaceStatus = report.Race.Status,
                        ReturnedResults = results.Count,
                        report.ReturnReasonCategory,
                        report.ReturnReason
                    },
                    reason,
                    cancellationToken);

                await _notificationService.CreateForUserAsync(
                    report.RefereeId,
                    "Post-race submission returned for revision",
                    $"Your final report and race results for {report.Race.RaceName} were returned. Reason: {reason}",
                    "PostRaceReportReturned",
                    $"/referee/races/post-race?raceId={report.RaceId}&reportId={report.ReportId}",
                    "RefereeReport",
                    report.ReportId,
                    cancellationToken,
                    preventDuplicates: false);

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }

            return Ok(new
            {
                message = "The final report and all race results were returned to the referee for revision.",
                reportId = report.ReportId,
                status = report.Status,
                raceStatus = report.Race.Status,
                returnedResults = results.Count,
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
            if (!User.TryGetUserId(out _))
            {
                return Unauthorized();
            }

            var report = await _context.RefereeReports
                .AsNoTracking()
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

            if (report.Status == RefereeReportStatuses.Returned)
            {
                return Conflict(new AdminActionResponse
                {
                    Message = "The referee must resubmit the returned report before approval.",
                    Id = id,
                    Status = report.Status
                });
            }

            if (report.Race.Status == RaceStatuses.Published &&
                report.Status == RefereeReportStatuses.Approved)
            {
                return Ok(new
                {
                    message = "This final report and its race results were already approved.",
                    reportId = report.ReportId,
                    raceId = report.RaceId,
                    status = report.Status,
                    alreadyCompleted = true
                });
            }

            if (report.Race.Status != RaceStatuses.ResultPending)
            {
                return Conflict(new AdminActionResponse
                {
                    Message = "The race must be ResultPending before its final submission can be approved.",
                    Id = id,
                    Status = report.Race.Status
                });
            }

            if (report.Status is not RefereeReportStatuses.Submitted and
                not RefereeReportStatuses.Approved)
            {
                return Conflict(new AdminActionResponse
                {
                    Message = "The final report is not ready for approval.",
                    Id = id,
                    Status = report.Status
                });
            }

            var resultCount = await _context.RaceResults
                .AsNoTracking()
                .CountAsync(r =>
                    r.RaceId == report.RaceId &&
                    (r.Status == RaceResultStatuses.RefereeConfirmed ||
                     r.Status == RaceResultStatuses.AdminApproved),
                    cancellationToken);

            if (resultCount == 0)
            {
                return Conflict(new
                {
                    code = "NO_REFEREE_CONFIRMED_RESULTS",
                    message = "No referee-confirmed results are ready for approval.",
                    reportId = report.ReportId,
                    raceId = report.RaceId
                });
            }

            // Compatibility endpoint:
            // Do not approve the report separately. The FE may still call this
            // endpoint before approve-all. The actual report approval, result
            // publication, race/tournament update and prize creation are all
            // committed atomically by PUT /api/admin/results/race/{raceId}/approve-all.
            return Ok(new
            {
                message = "Final report validation passed. Complete approval will run atomically with all race results.",
                reportId = report.ReportId,
                raceId = report.RaceId,
                status = report.Status,
                deferredToAtomicApproval = true,
                endpoint = $"PUT /api/admin/results/race/{report.RaceId}/approve-all"
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
