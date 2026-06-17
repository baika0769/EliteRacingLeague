using System.Security.Claims;
using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Eliteracingleague.API.DTOs.Referee;

namespace Eliteracingleague.API.Controllers.Referee;

[Authorize(Roles = UserRoles.RaceReferee)]
[ApiController]
[Route("api/referee/races")]
public class RefereeRacesController : ControllerBase
{
    private readonly EliteRacingLeagueContext _context;

    public RefereeRacesController(EliteRacingLeagueContext context)
    {
        _context = context;
    }

    private int GetRefereeId()
    {
        return int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    }

    [HttpGet]
    public async Task<IActionResult> GetAssignedRaces()
    {
        var refereeId = GetRefereeId();

        var races = await _context.RefereeAssignments
            .Where(a => a.RefereeId == refereeId
                && a.Status == RefereeAssignmentStatuses.Assigned)
            .Select(a => new
            {
                assignmentId = a.RefereeAssignmentId,
                raceId = a.RaceId,
                raceName = a.Race.RaceName,
                tournamentName = a.Race.Tournament.TournamentName,
                raceDate = a.Race.RaceDate,
                distanceMeters = a.Race.DistanceMeters,
                location = a.Race.Location,
                raceStatus = a.Race.Status,
                assignmentStatus = a.Status,
                assignedAt = a.AssignedAt
            })
            .ToListAsync();

        return Ok(races);
    }

    [HttpGet("{raceId}/registrations")]
    public async Task<IActionResult> GetRaceRegistrations(int raceId)
    {
        var refereeId = GetRefereeId();

        var assigned = await _context.RefereeAssignments.AnyAsync(a =>
            a.RaceId == raceId &&
            a.RefereeId == refereeId &&
            a.Status == RefereeAssignmentStatuses.Assigned);

        if (!assigned)
            return Forbid();

        var registrations = await _context.RaceRegistrations
            .Where(r => r.RaceId == raceId)
            .Select(r => new
            {
                registrationId = r.RegistrationId,
                horseId = r.HorseId,
                horseName = r.Horse.HorseName,
                ownerId = r.OwnerId,
                jockeyId = r.JockeyId,
                status = r.Status
            })
            .ToListAsync();

        return Ok(registrations);
    }

    [HttpPost("{raceId}/inspections")]
    public async Task<IActionResult> CreateOrUpdateInspection(
        int raceId,
        CreateInspectionRequest request)
    {
        var refereeId = GetRefereeId();

        if (!PreRaceInspectionStatuses.IsValid(request.Status))
            return BadRequest("Invalid inspection status.");

        var assigned = await _context.RefereeAssignments.AnyAsync(a =>
            a.RaceId == raceId &&
            a.RefereeId == refereeId &&
            a.Status == RefereeAssignmentStatuses.Assigned);

        if (!assigned)
            return Forbid();

        var registration = await _context.RaceRegistrations
            .FirstOrDefaultAsync(r =>
                r.RaceId == raceId &&
                r.RegistrationId == request.RegistrationId);

        if (registration == null)
            return NotFound("Registration not found.");

        var inspection = await _context.PreRaceInspections
            .FirstOrDefaultAsync(i =>
                i.RaceId == raceId &&
                i.RegistrationId == request.RegistrationId);

        if (inspection == null)
        {
            inspection = new PreRaceInspection
            {
                RaceId = raceId,
                RegistrationId = request.RegistrationId,
                RefereeId = refereeId,
                Status = request.Status,
                Note = request.Note,
                InspectedAt = DateTime.UtcNow
            };

            _context.PreRaceInspections.Add(inspection);
        }
        else
        {
            inspection.Status = request.Status;
            inspection.Note = request.Note;
            inspection.InspectedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Inspection saved successfully",
            inspectionId = inspection.InspectionId,
            status = inspection.Status
        });
    }

    [HttpPost("{raceId}/results")]
    public async Task<IActionResult> CreateOrUpdateResult(
        int raceId,
        CreateRaceResultRequest request)
    {
        var refereeId = GetRefereeId();

        var assigned = await _context.RefereeAssignments.AnyAsync(a =>
            a.RaceId == raceId &&
            a.RefereeId == refereeId &&
            a.Status == RefereeAssignmentStatuses.Assigned);

        if (!assigned)
            return Forbid();

        var registration = await _context.RaceRegistrations
            .FirstOrDefaultAsync(r =>
                r.RaceId == raceId &&
                r.RegistrationId == request.RegistrationId);

        if (registration == null)
            return NotFound("Registration not found.");

        var result = await _context.RaceResults
            .FirstOrDefaultAsync(r => r.RegistrationId == request.RegistrationId);

        if (result == null)
        {
            result = new RaceResult
            {
                RaceId = raceId,
                RegistrationId = request.RegistrationId,
                FinishTimeSeconds = request.FinishTimeSeconds,
                FinishPosition = request.FinishPosition,
                Score = request.Score,
                Status = RaceResultStatuses.Draft,
                EnteredByRefereeId = refereeId,
                Note = request.Note,
                CreatedAt = DateTime.UtcNow
            };

            _context.RaceResults.Add(result);
        }
        else
        {
            result.FinishTimeSeconds = request.FinishTimeSeconds;
            result.FinishPosition = request.FinishPosition;
            result.Score = request.Score;
            result.Note = request.Note;
            result.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Race result saved successfully",
            resultId = result.ResultId,
            status = result.Status
        });
    }

    [HttpPut("{raceId}/results/{resultId}/confirm")]
    public async Task<IActionResult> ConfirmResult(int raceId, int resultId)
    {
        var refereeId = GetRefereeId();

        var result = await _context.RaceResults
            .FirstOrDefaultAsync(r =>
                r.ResultId == resultId &&
                r.RaceId == raceId &&
                r.EnteredByRefereeId == refereeId);

        if (result == null)
            return NotFound("Race result not found.");

        result.Status = RaceResultStatuses.RefereeConfirmed;
        result.UpdatedAt = DateTime.UtcNow;

        var race = await _context.Races.FirstOrDefaultAsync(r => r.RaceId == raceId);
        if (race != null)
        {
            race.Status = RaceStatuses.ResultPending;
            race.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Result confirmed by referee",
            resultId = result.ResultId,
            status = result.Status
        });
    }

    [HttpPost("{raceId}/violations")]
    public async Task<IActionResult> CreateViolation(
        int raceId,
        CreateViolationRequest request)
    {
        var refereeId = GetRefereeId();

        if (!RaceViolationActions.IsValid(request.Action))
            return BadRequest("Invalid violation action.");

        var assigned = await _context.RefereeAssignments.AnyAsync(a =>
            a.RaceId == raceId &&
            a.RefereeId == refereeId &&
            a.Status == RefereeAssignmentStatuses.Assigned);

        if (!assigned)
            return Forbid();

        var violation = new RaceViolation
        {
            RaceId = raceId,
            RegistrationId = request.RegistrationId,
            RefereeId = refereeId,
            ViolationType = request.ViolationType,
            Description = request.Description,
            Action = request.Action,
            PenaltyPoints = request.PenaltyPoints,
            CreatedAt = DateTime.UtcNow
        };

        _context.RaceViolations.Add(violation);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Violation created successfully",
            violationId = violation.ViolationId,
            action = violation.Action
        });
    }

    [HttpGet("{raceId}/inspection-report")]
    public async Task<IActionResult> GetInspectionReport(int raceId, string? filter = "all")
    {
        var refereeId = GetRefereeId();

        var assigned = await _context.RefereeAssignments.AnyAsync(a =>
            a.RaceId == raceId &&
            a.RefereeId == refereeId &&
            a.Status == RefereeAssignmentStatuses.Assigned);

        if (!assigned)
            return Forbid();

        var race = await _context.Races
            .Where(r => r.RaceId == raceId)
            .Select(r => new
            {
                r.RaceId,
                r.RaceName,
                r.Location
            })
            .FirstOrDefaultAsync();

        if (race == null)
            return NotFound("Race not found.");

        var query = _context.RaceRegistrations
            .Where(r => r.RaceId == raceId)
            .Select(r => new
            {
                registrationId = r.RegistrationId,
                horseName = r.Horse.HorseName,
                registrationCode = "#REG-" + r.RegistrationId.ToString(),
                inspectionStatus = _context.PreRaceInspections
                    .Where(i => i.RaceId == raceId && i.RegistrationId == r.RegistrationId)
                    .Select(i => i.Status)
                    .FirstOrDefault() ?? PreRaceInspectionStatuses.PendingConfirmation,
                note = _context.PreRaceInspections
                    .Where(i => i.RaceId == raceId && i.RegistrationId == r.RegistrationId)
                    .Select(i => i.Note)
                    .FirstOrDefault()
            });

        if (filter == "flagged")
            query = query.Where(x => x.inspectionStatus == PreRaceInspectionStatuses.Failed);

        if (filter == "pending")
            query = query.Where(x => x.inspectionStatus == PreRaceInspectionStatuses.PendingConfirmation);

        var rows = await query.ToListAsync();

        var allCount = await _context.RaceRegistrations.CountAsync(r => r.RaceId == raceId);

        var failedCount = await _context.PreRaceInspections
            .CountAsync(i => i.RaceId == raceId && i.Status == PreRaceInspectionStatuses.Failed);

        var pendingCount = allCount - await _context.PreRaceInspections
            .CountAsync(i => i.RaceId == raceId && i.Status != PreRaceInspectionStatuses.PendingConfirmation);

        return Ok(new
        {
            race,
            counts = new
            {
                all = allCount,
                flagged = failedCount,
                pending = pendingCount
            },
            items = rows.Select(x => new
            {
                x.registrationId,
                x.horseName,
                x.registrationCode,
                checklist = x.inspectionStatus == PreRaceInspectionStatuses.Failed
                    ? new[] { true, true, false, true }
                    : new[] { true, true, true, true },
                ruleRef = x.inspectionStatus == PreRaceInspectionStatuses.Failed ? "Rule 2.2" : "N/A",
                severity = x.inspectionStatus == PreRaceInspectionStatuses.Failed ? "HIGH" :
                           x.inspectionStatus == PreRaceInspectionStatuses.PendingConfirmation ? "MEDIUM" : "-",
                violation = x.inspectionStatus == PreRaceInspectionStatuses.Failed
                    ? x.note ?? "Violation detected."
                    : x.inspectionStatus == PreRaceInspectionStatuses.PendingConfirmation
                        ? "Awaiting referee confirmation."
                        : "Full compliance with technical and health standards.",
                outcome = x.inspectionStatus == PreRaceInspectionStatuses.Failed ? "PROHIBITED" :
                          x.inspectionStatus == PreRaceInspectionStatuses.PendingConfirmation ? "PENDING" : "ALLOWED"
            })
        });
    }

    [HttpPost("{raceId}/reports")]
    public async Task<IActionResult> CreateReport(
        int raceId,
        CreateRefereeReportRequest request)
    {
        var refereeId = GetRefereeId();

        var assigned = await _context.RefereeAssignments.AnyAsync(a =>
            a.RaceId == raceId &&
            a.RefereeId == refereeId &&
            a.Status == RefereeAssignmentStatuses.Assigned);

        if (!assigned)
            return Forbid();

        var report = new RefereeReport
        {
            RaceId = raceId,
            RefereeId = refereeId,
            ReportContent = request.ReportContent,
            SubmittedAt = DateTime.UtcNow
        };

        _context.RefereeReports.Add(report);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Referee report submitted successfully",
            reportId = report.ReportId
        });
    }

    [HttpGet("{raceId}/violations")]
    public async Task<IActionResult> GetViolations(int raceId)
    {
        var refereeId = GetRefereeId();

        var assigned = await _context.RefereeAssignments.AnyAsync(a =>
            a.RaceId == raceId &&
            a.RefereeId == refereeId &&
            a.Status == RefereeAssignmentStatuses.Assigned);

        if (!assigned)
            return Forbid();

        var violations = await _context.RaceViolations
            .Where(v => v.RaceId == raceId && v.RefereeId == refereeId)
            .OrderByDescending(v => v.CreatedAt)
            .Select(v => new
            {
                violationId = v.ViolationId,
                registrationId = v.RegistrationId,
                horseName = v.Registration.Horse.HorseName,
                violationType = v.ViolationType,
                description = v.Description,
                action = v.Action,
                penaltyPoints = v.PenaltyPoints,
                createdAt = v.CreatedAt
            })
            .ToListAsync();

        return Ok(violations);
    }

    [HttpGet("{raceId}/results")]
    public async Task<IActionResult> GetResults(int raceId)
    {
        var refereeId = GetRefereeId();

        var assigned = await _context.RefereeAssignments.AnyAsync(a =>
            a.RaceId == raceId &&
            a.RefereeId == refereeId &&
            a.Status == RefereeAssignmentStatuses.Assigned);

        if (!assigned)
            return Forbid();

        var results = await _context.RaceResults
            .Where(r => r.RaceId == raceId && r.EnteredByRefereeId == refereeId)
            .OrderBy(r => r.FinishPosition)
            .Select(r => new
            {
                resultId = r.ResultId,
                registrationId = r.RegistrationId,
                horseName = r.Registration.Horse.HorseName,
                finishTimeSeconds = r.FinishTimeSeconds,
                finishPosition = r.FinishPosition,
                score = r.Score,
                status = r.Status,
                note = r.Note,
                createdAt = r.CreatedAt,
                updatedAt = r.UpdatedAt
            })
            .ToListAsync();

        return Ok(results);
    }
    [HttpGet("{raceId}/reports")]
    public async Task<IActionResult> GetReports(int raceId)
    {
        var refereeId = GetRefereeId();

        var assigned = await _context.RefereeAssignments.AnyAsync(a =>
            a.RaceId == raceId &&
            a.RefereeId == refereeId &&
            a.Status == RefereeAssignmentStatuses.Assigned);

        if (!assigned)
            return Forbid();

        var reports = await _context.RefereeReports
            .Where(r => r.RaceId == raceId && r.RefereeId == refereeId)
            .OrderByDescending(r => r.SubmittedAt)
            .Select(r => new
            {
                reportId = r.ReportId,
                raceId = r.RaceId,
                reportContent = r.ReportContent,
                submittedAt = r.SubmittedAt
            })
            .ToListAsync();

        return Ok(reports);
    }
}
