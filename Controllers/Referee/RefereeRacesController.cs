using System.Security.Claims;
using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Referee;
using Eliteracingleague.API.Models;
using Eliteracingleague.API.Services;
using Eliteracingleague.API.Services.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Controllers.Referee;

[Authorize(Roles = UserRoles.RaceReferee)]
[ApiController]
[Route("api/referee/races")]
public class RefereeRacesController : ControllerBase
{
    private readonly EliteRacingLeagueContext _context;
    private readonly RefereeRaceLifecycleService _lifecycleService;
    private readonly INotificationService _notificationService;

    private static readonly string[] ActiveRegistrationStatuses =
    {
        RaceRegistrationStatuses.Approved,
        RaceRegistrationStatuses.JockeyInvited,
        RaceRegistrationStatuses.ReadyToRace
    };

    private static readonly string[] ViolationAllowedRaceStatuses =
    {
        RaceStatuses.AssignedReferee,
        RaceStatuses.RefereeReady,
        RaceStatuses.Ongoing,
        RaceStatuses.Finished
    };

    public RefereeRacesController(
        EliteRacingLeagueContext context,
        RefereeRaceLifecycleService lifecycleService,
        INotificationService notificationService)
    {
        _context = context;
        _lifecycleService = lifecycleService;
        _notificationService = notificationService;
    }

    private int GetRefereeId()
    {
        return int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    }

    private async Task<bool> IsAssignedToActiveRaceAsync(int raceId, int refereeId)
    {
        return await _context.RefereeAssignments.AnyAsync(a =>
            a.RaceId == raceId &&
            a.RefereeId == refereeId &&
            a.Status == RefereeAssignmentStatuses.Assigned &&
            a.Race.Status != RaceStatuses.Cancelled &&
            a.Race.Tournament.Status != TournamentStatuses.Cancelled);
    }

    private async Task<IActionResult> BuildLifecycleAccessErrorAsync(int raceId)
    {
        return await _lifecycleService.RaceExistsAsync(raceId)
            ? Forbid()
            : NotFound(new { message = "Race not found." });
    }

    [HttpGet]
    public async Task<IActionResult> GetAssignedRaces()
    {
        var refereeId = GetRefereeId();

        var races = await _context.RefereeAssignments
            .AsNoTracking()
            .Where(a =>
                a.RefereeId == refereeId &&
                a.Status == RefereeAssignmentStatuses.Assigned &&
                a.Race.Status != RaceStatuses.Cancelled &&
                a.Race.Tournament.Status != TournamentStatuses.Cancelled)
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
                tournamentStatus = a.Race.Tournament.Status,
                assignmentStatus = a.Status,
                assignedAt = a.AssignedAt
            })
            .ToListAsync();

        var response = new List<object>();

        foreach (var race in races)
        {
            var lifecycle = await _lifecycleService.GetLifecycleAsync(race.raceId, refereeId);

            response.Add(new
            {
                race.assignmentId,
                race.raceId,
                race.raceName,
                race.tournamentName,
                race.raceDate,
                race.distanceMeters,
                race.location,
                race.raceStatus,
                race.tournamentStatus,
                race.assignmentStatus,
                race.assignedAt,
                currentStage = lifecycle?.CurrentStage,
                nextStage = lifecycle?.NextStage,
                allowedActions = lifecycle?.AllowedActions
            });
        }

        return Ok(response);
    }

    [HttpGet("{raceId}/lifecycle")]
    public async Task<IActionResult> GetLifecycle(int raceId)
    {
        var refereeId = GetRefereeId();
        var lifecycle = await _lifecycleService.GetLifecycleAsync(raceId, refereeId);

        if (lifecycle == null)
        {
            return await BuildLifecycleAccessErrorAsync(raceId);
        }

        return Ok(lifecycle);
    }

    [HttpGet("{raceId}/registrations")]
    public async Task<IActionResult> GetRaceRegistrations(int raceId)
    {
        var refereeId = GetRefereeId();

        var assigned = await IsAssignedToActiveRaceAsync(raceId, refereeId);

        if (!assigned)
        {
            return Forbid();
        }

        var registrations = await _context.RaceRegistrations
            .AsNoTracking()
            .Where(r =>
                r.RaceId == raceId &&
                ActiveRegistrationStatuses.Contains(r.Status) &&
                r.Race.Status != RaceStatuses.Cancelled &&
                r.Race.Tournament.Status != TournamentStatuses.Cancelled)
            .Select(r => new
            {
                registrationId = r.RegistrationId,
                horseId = r.HorseId,
                horseName = r.Horse.HorseName,
                horseImageUrl = r.Horse.ImageUrl,
                horseHealthStatus = r.Horse.HealthStatus,
                healthCertificateImageUrl = r.Horse.HealthCertificateImageUrl,
                ownerId = r.OwnerId,
                jockeyId = r.JockeyId,
                jockeyName = r.Jockey == null ? null : r.Jockey.JockeyNavigation.FullName,
                status = r.Status
            })
            .ToListAsync();

        return Ok(registrations);
    }

    [HttpPut("{raceId}/mark-ready")]
    public async Task<IActionResult> MarkReady(int raceId)
    {
        var refereeId = GetRefereeId();
        var lifecycle = await _lifecycleService.GetLifecycleAsync(raceId, refereeId);

        if (lifecycle == null)
        {
            return await BuildLifecycleAccessErrorAsync(raceId);
        }

        if (lifecycle.RaceStatus != RaceStatuses.AssignedReferee)
        {
            return BadRequest(new { message = "Race must be AssignedReferee before it can be marked ready." });
        }

        if (lifecycle.Counts.TotalRegistrations == 0)
        {
            return BadRequest(new { message = "Race cannot be marked ready because no eligible registrations were found." });
        }

        if (lifecycle.Counts.PendingInspections > 0)
        {
            return BadRequest(new { message = "Race cannot be marked ready because there are pending inspections." });
        }

        if (lifecycle.Counts.FailedInspections >= lifecycle.Counts.TotalRegistrations)
        {
            return BadRequest(new { message = "Race cannot be marked ready because all inspections failed." });
        }

        var race = await _context.Races.FirstAsync(r => r.RaceId == raceId);
        race.Status = RaceStatuses.RefereeReady;
        race.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(await _lifecycleService.GetLifecycleAsync(raceId, refereeId));
    }

    [HttpPut("{raceId}/start")]
    public async Task<IActionResult> StartRace(int raceId)
    {
        var refereeId = GetRefereeId();
        var lifecycle = await _lifecycleService.GetLifecycleAsync(raceId, refereeId);

        if (lifecycle == null)
        {
            return await BuildLifecycleAccessErrorAsync(raceId);
        }

        if (lifecycle.RaceStatus != RaceStatuses.RefereeReady)
        {
            return BadRequest(new { message = "Race must be RefereeReady before it can start." });
        }

        if (lifecycle.Counts.PendingInspections > 0)
        {
            return BadRequest(new { message = "Race cannot start because there are pending inspections." });
        }

        if (lifecycle.Counts.TotalRegistrations == 0 ||
            lifecycle.Counts.FailedInspections >= lifecycle.Counts.TotalRegistrations)
        {
            return BadRequest(new { message = "Race cannot start because no eligible registrations were found." });
        }

        var race = await _context.Races
            .Include(r => r.Tournament)
            .FirstAsync(r => r.RaceId == raceId);

        race.Status = RaceStatuses.Ongoing;
        race.UpdatedAt = DateTime.UtcNow;

        if (race.Tournament.Status != TournamentStatuses.Ongoing &&
            race.Tournament.Status != TournamentStatuses.Completed &&
            race.Tournament.Status != TournamentStatuses.Cancelled)
        {
            race.Tournament.Status = TournamentStatuses.Ongoing;
            race.Tournament.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        return Ok(await _lifecycleService.GetLifecycleAsync(raceId, refereeId));
    }

    [HttpPut("{raceId}/finish")]
    public async Task<IActionResult> FinishRace(int raceId)
    {
        var refereeId = GetRefereeId();
        var lifecycle = await _lifecycleService.GetLifecycleAsync(raceId, refereeId);

        if (lifecycle == null)
        {
            return await BuildLifecycleAccessErrorAsync(raceId);
        }

        if (lifecycle.RaceStatus != RaceStatuses.Ongoing)
        {
            return BadRequest(new { message = "Race must be Ongoing before it can finish." });
        }

        var race = await _context.Races.FirstAsync(r => r.RaceId == raceId);
        race.Status = RaceStatuses.Finished;
        race.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(await _lifecycleService.GetLifecycleAsync(raceId, refereeId));
    }

    [HttpPost("{raceId}/inspections")]
    public async Task<IActionResult> CreateOrUpdateInspection(
        int raceId,
        CreateInspectionRequest request)
    {
        var refereeId = GetRefereeId();

        if (!PreRaceInspectionStatuses.IsValid(request.Status))
        {
            return BadRequest("Invalid inspection status.");
        }

        var assigned = await IsAssignedToActiveRaceAsync(raceId, refereeId);

        if (!assigned)
        {
            return Forbid();
        }

        var race = await _context.Races
            .AsNoTracking()
            .FirstOrDefaultAsync(r =>
                r.RaceId == raceId &&
                r.Status != RaceStatuses.Cancelled &&
                r.Tournament.Status != TournamentStatuses.Cancelled);

        if (race == null)
        {
            return NotFound("Race not found or has been cancelled.");
        }

        if (race.Status != RaceStatuses.AssignedReferee)
        {
            return BadRequest("Pre-race inspection can only be updated when race status is AssignedReferee.");
        }

        var registration = await _context.RaceRegistrations
            .FirstOrDefaultAsync(r =>
                r.RaceId == raceId &&
                r.RegistrationId == request.RegistrationId &&
                ActiveRegistrationStatuses.Contains(r.Status) &&
                r.Race.Status != RaceStatuses.Cancelled &&
                r.Race.Tournament.Status != TournamentStatuses.Cancelled);

        if (registration == null)
        {
            return NotFound("Registration not found or has been cancelled.");
        }

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

        var assigned = await IsAssignedToActiveRaceAsync(raceId, refereeId);

        if (!assigned)
        {
            return Forbid();
        }

        var race = await _context.Races
            .AsNoTracking()
            .FirstOrDefaultAsync(r =>
                r.RaceId == raceId &&
                r.Status != RaceStatuses.Cancelled &&
                r.Tournament.Status != TournamentStatuses.Cancelled);

        if (race == null)
        {
            return NotFound("Race not found or has been cancelled.");
        }

        if (race.Status != RaceStatuses.Finished)
        {
            return BadRequest("Race results can only be entered when race status is Finished.");
        }

        if (request.FinishPosition.HasValue && request.FinishPosition.Value <= 0)
        {
            return BadRequest("Finish position must be greater than 0.");
        }

        if (request.FinishTimeSeconds.HasValue && request.FinishTimeSeconds.Value <= 0)
        {
            return BadRequest("Finish time must be greater than 0.");
        }

        if (request.Score.HasValue && request.Score.Value < 0)
        {
            return BadRequest("Score cannot be negative.");
        }

        var registration = await _context.RaceRegistrations
            .FirstOrDefaultAsync(r =>
                r.RaceId == raceId &&
                r.RegistrationId == request.RegistrationId &&
                ActiveRegistrationStatuses.Contains(r.Status) &&
                r.Race.Status != RaceStatuses.Cancelled &&
                r.Race.Tournament.Status != TournamentStatuses.Cancelled);

        if (registration == null)
        {
            return NotFound("Registration not found or has been cancelled.");
        }

        var failedInspection = await _context.PreRaceInspections
            .AnyAsync(i =>
                i.RaceId == raceId &&
                i.RegistrationId == request.RegistrationId &&
                i.Status == PreRaceInspectionStatuses.Failed);

        if (failedInspection)
        {
            return BadRequest("Cannot enter result for a registration that failed pre-race inspection.");
        }

        var result = request.ResultId.HasValue
            ? await _context.RaceResults
                .FirstOrDefaultAsync(r =>
                    r.ResultId == request.ResultId.Value &&
                    r.RaceId == raceId)
            : await _context.RaceResults
                .FirstOrDefaultAsync(r =>
                    r.RaceId == raceId &&
                    r.RegistrationId == request.RegistrationId);

        if (request.ResultId.HasValue && result == null)
        {
            return NotFound("Race result not found.");
        }

        if (result != null)
        {
            if (result.EnteredByRefereeId != refereeId)
            {
                return Forbid();
            }

            if (result.RegistrationId != request.RegistrationId)
            {
                return BadRequest("Race result does not match the registration.");
            }

            if (result.Status is RaceResultStatuses.AdminApproved or RaceResultStatuses.Published)
            {
                return BadRequest("Admin-approved or published results cannot be edited.");
            }
        }

        if (request.FinishPosition.HasValue)
        {
            var duplicatePosition = await _context.RaceResults
                .AnyAsync(r =>
                    r.RaceId == raceId &&
                    r.FinishPosition == request.FinishPosition.Value &&
                    (result == null || r.ResultId != result.ResultId));

            if (duplicatePosition)
            {
                return BadRequest(request.FinishPosition.Value == 1
                    ? "Race can only have one winner."
                    : "Finish position already exists for this race.");
            }
        }

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

    [HttpPut("{raceId}/results/{resultId:int}/confirm")]
    public async Task<IActionResult> ConfirmResult(int raceId, int resultId)
    {
        var refereeId = GetRefereeId();

        var assigned = await IsAssignedToActiveRaceAsync(raceId, refereeId);

        if (!assigned)
        {
            return Forbid();
        }

        var race = await _context.Races
            .FirstOrDefaultAsync(r =>
                r.RaceId == raceId &&
                r.Status != RaceStatuses.Cancelled &&
                r.Tournament.Status != TournamentStatuses.Cancelled);

        if (race == null)
        {
            return NotFound("Race not found or has been cancelled.");
        }

        if (race.Status != RaceStatuses.Finished)
        {
            return BadRequest("Results can only be confirmed while race status is Finished.");
        }

        var result = await _context.RaceResults
            .FirstOrDefaultAsync(r =>
                r.ResultId == resultId &&
                r.RaceId == raceId &&
                r.EnteredByRefereeId == refereeId);

        if (result == null)
        {
            return NotFound("Race result not found.");
        }

        if (result.Status is RaceResultStatuses.AdminApproved or RaceResultStatuses.Published)
        {
            return BadRequest("Admin-approved or published results cannot be confirmed by referee.");
        }

        result.Status = RaceResultStatuses.RefereeConfirmed;
        result.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Result confirmed by referee",
            resultId = result.ResultId,
            status = result.Status
        });
    }

    [HttpPut("{raceId}/results/confirm-all")]
    public async Task<IActionResult> ConfirmAllResults(int raceId)
    {
        var refereeId = GetRefereeId();

        var assigned = await IsAssignedToActiveRaceAsync(raceId, refereeId);

        if (!assigned)
        {
            return Forbid();
        }

        var race = await _context.Races
            .FirstOrDefaultAsync(r =>
                r.RaceId == raceId &&
                r.Status != RaceStatuses.Cancelled &&
                r.Tournament.Status != TournamentStatuses.Cancelled);

        if (race == null)
        {
            return NotFound("Race not found or has been cancelled.");
        }

        if (race.Status != RaceStatuses.Finished)
        {
            return BadRequest(new { message = "All results can only be confirmed when race status is Finished." });
        }

        var activeRegistrationIds = await _context.RaceRegistrations
            .AsNoTracking()
            .Where(r =>
                r.RaceId == raceId &&
                ActiveRegistrationStatuses.Contains(r.Status))
            .Select(r => r.RegistrationId)
            .ToListAsync();

        var failedRegistrationIds = await _context.PreRaceInspections
            .AsNoTracking()
            .Where(i =>
                i.RaceId == raceId &&
                i.Status == PreRaceInspectionStatuses.Failed &&
                activeRegistrationIds.Contains(i.RegistrationId))
            .Select(i => i.RegistrationId)
            .ToListAsync();

        var eligibleRegistrationIds = activeRegistrationIds
            .Except(failedRegistrationIds)
            .ToList();

        if (eligibleRegistrationIds.Count == 0)
        {
            return BadRequest(new { message = "No eligible registrations found." });
        }

        var results = await _context.RaceResults
            .Where(r =>
                r.RaceId == raceId &&
                r.EnteredByRefereeId == refereeId &&
                eligibleRegistrationIds.Contains(r.RegistrationId))
            .ToListAsync();

        if (results.Count(r => r.Status == RaceResultStatuses.Draft) == 0)
        {
            return BadRequest(new { message = "There are no draft results to confirm." });
        }

        if (results.Count < eligibleRegistrationIds.Count)
        {
            return BadRequest(new { message = "All eligible registrations must have a result before confirmation." });
        }

        if (results.Any(r => r.FinishPosition == null))
        {
            return BadRequest(new { message = "All results must have a finish position." });
        }

        var duplicatePosition = results
            .GroupBy(r => r.FinishPosition)
            .Any(g => g.Count() > 1);

        if (duplicatePosition)
        {
            return BadRequest(new { message = "Finish positions cannot be duplicated." });
        }

        if (results.Count(r => r.FinishPosition == 1) != 1)
        {
            return BadRequest(new { message = "Race must have exactly one winner." });
        }

        if (results.Any(r => r.Status is RaceResultStatuses.AdminApproved or RaceResultStatuses.Published))
        {
            return BadRequest(new { message = "Admin-approved or published results cannot be confirmed by referee." });
        }

        foreach (var result in results.Where(r => r.Status == RaceResultStatuses.Draft))
        {
            result.Status = RaceResultStatuses.RefereeConfirmed;
            result.UpdatedAt = DateTime.UtcNow;
        }

        race.Status = RaceStatuses.ResultPending;
        race.UpdatedAt = DateTime.UtcNow;

        await _notificationService.CreateForAdminsAsync(
            "Race Result Submitted",
            $"Referee submitted results for {race.RaceName}. Please validate.",
            "RaceResultValidation",
            "/admin/validate-results",
            "Race",
            raceId);

        await _context.SaveChangesAsync();

        return Ok(await _lifecycleService.GetLifecycleAsync(raceId, refereeId));
    }

    [HttpPost("{raceId}/violations")]
    public async Task<IActionResult> CreateViolation(
        int raceId,
        CreateViolationRequest request)
    {
        var refereeId = GetRefereeId();

        if (!RaceViolationActions.IsValid(request.Action))
        {
            return BadRequest("Invalid violation action.");
        }

        var assigned = await IsAssignedToActiveRaceAsync(raceId, refereeId);

        if (!assigned)
        {
            return Forbid();
        }

        var race = await _context.Races
            .AsNoTracking()
            .FirstOrDefaultAsync(r =>
                r.RaceId == raceId &&
                r.Status != RaceStatuses.Cancelled &&
                r.Tournament.Status != TournamentStatuses.Cancelled);

        if (race == null)
        {
            return NotFound("Race not found or has been cancelled.");
        }

        if (!ViolationAllowedRaceStatuses.Contains(race.Status))
        {
            return BadRequest("Violations can only be reported before publishing or cancellation.");
        }

        var registrationExists = await _context.RaceRegistrations
            .AnyAsync(r =>
                r.RaceId == raceId &&
                r.RegistrationId == request.RegistrationId &&
                ActiveRegistrationStatuses.Contains(r.Status) &&
                r.Race.Status != RaceStatuses.Cancelled &&
                r.Race.Tournament.Status != TournamentStatuses.Cancelled);

        if (!registrationExists)
        {
            return NotFound("Registration not found or has been cancelled.");
        }

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
            message = "Violation recorded successfully",
            violationId = violation.ViolationId,
            action = violation.Action
        });
    }

    [HttpGet("{raceId}/inspection-report")]
    public async Task<IActionResult> GetInspectionReport(int raceId, string? filter = "all")
    {
        var refereeId = GetRefereeId();

        var assigned = await IsAssignedToActiveRaceAsync(raceId, refereeId);

        if (!assigned)
        {
            return Forbid();
        }

        var race = await _context.Races
            .AsNoTracking()
            .Where(r =>
                r.RaceId == raceId &&
                r.Status != RaceStatuses.Cancelled &&
                r.Tournament.Status != TournamentStatuses.Cancelled)
            .Select(r => new
            {
                r.RaceId,
                r.RaceName,
                r.Location
            })
            .FirstOrDefaultAsync();

        if (race == null)
        {
            return NotFound("Race not found or has been cancelled.");
        }

        var query = _context.RaceRegistrations
            .AsNoTracking()
            .Where(r =>
                r.RaceId == raceId &&
                ActiveRegistrationStatuses.Contains(r.Status) &&
                r.Race.Status != RaceStatuses.Cancelled &&
                r.Race.Tournament.Status != TournamentStatuses.Cancelled)
            .Select(r => new
            {
                registrationId = r.RegistrationId,
                horseId = r.HorseId,
                horseName = r.Horse.HorseName,
                horseImageUrl = r.Horse.ImageUrl,
                horseHealthStatus = r.Horse.HealthStatus,
                healthCertificateImageUrl = r.Horse.HealthCertificateImageUrl,
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
        {
            query = query.Where(x => x.inspectionStatus == PreRaceInspectionStatuses.Failed);
        }

        if (filter == "pending")
        {
            query = query.Where(x => x.inspectionStatus == PreRaceInspectionStatuses.PendingConfirmation);
        }

        var rows = await query.ToListAsync();

        var allCount = await _context.RaceRegistrations
            .CountAsync(r =>
                r.RaceId == raceId &&
                ActiveRegistrationStatuses.Contains(r.Status) &&
                r.Race.Status != RaceStatuses.Cancelled &&
                r.Race.Tournament.Status != TournamentStatuses.Cancelled);

        var failedCount = await _context.PreRaceInspections
            .CountAsync(i =>
                i.RaceId == raceId &&
                i.Status == PreRaceInspectionStatuses.Failed &&
                i.Registration.Race.Status != RaceStatuses.Cancelled &&
                i.Registration.Race.Tournament.Status != TournamentStatuses.Cancelled);

        var inspectedCount = await _context.PreRaceInspections
            .CountAsync(i =>
                i.RaceId == raceId &&
                i.Status != PreRaceInspectionStatuses.PendingConfirmation &&
                i.Registration.Race.Status != RaceStatuses.Cancelled &&
                i.Registration.Race.Tournament.Status != TournamentStatuses.Cancelled);

        var pendingCount = Math.Max(0, allCount - inspectedCount);

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
                x.horseId,
                x.horseName,
                x.horseImageUrl,
                x.horseHealthStatus,
                x.healthCertificateImageUrl,
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

    [HttpGet("{raceId}/violations")]
    public async Task<IActionResult> GetViolations(int raceId)
    {
        var refereeId = GetRefereeId();

        var assigned = await IsAssignedToActiveRaceAsync(raceId, refereeId);

        if (!assigned)
        {
            return Forbid();
        }

        var violations = await _context.RaceViolations
            .AsNoTracking()
            .Where(v =>
                v.RaceId == raceId &&
                v.RefereeId == refereeId &&
                v.Race.Status != RaceStatuses.Cancelled &&
                v.Race.Tournament.Status != TournamentStatuses.Cancelled)
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

        var assigned = await IsAssignedToActiveRaceAsync(raceId, refereeId);

        if (!assigned)
        {
            return Forbid();
        }

        var results = await _context.RaceResults
            .AsNoTracking()
            .Where(r =>
                r.RaceId == raceId &&
                r.EnteredByRefereeId == refereeId &&
                r.Race.Status != RaceStatuses.Cancelled &&
                r.Race.Tournament.Status != TournamentStatuses.Cancelled)
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

    [HttpPost("{raceId}/reports")]
    public async Task<IActionResult> CreateReport(
        int raceId,
        CreateRefereeReportRequest request)
    {
        var refereeId = GetRefereeId();

        if (!RefereeReportTypes.IsValid(request.ReportType))
        {
            return BadRequest("Invalid report type.");
        }

        if (string.IsNullOrWhiteSpace(request.ReportContent))
        {
            return BadRequest("Report content is required.");
        }

        var assigned = await IsAssignedToActiveRaceAsync(raceId, refereeId);

        if (!assigned)
        {
            return Forbid();
        }

        var race = await _context.Races
            .Include(r => r.Tournament)
            .FirstOrDefaultAsync(r =>
                r.RaceId == raceId &&
                r.Status != RaceStatuses.Cancelled &&
                r.Tournament.Status != TournamentStatuses.Cancelled);

        if (race == null)
        {
            return NotFound("Race not found or has been cancelled.");
        }

        if (request.ReportType == RefereeReportTypes.PreRace)
        {
            if (race.Status != RaceStatuses.AssignedReferee &&
                race.Status != RaceStatuses.RefereeReady)
            {
                return BadRequest("Pre-race report can only be submitted when race status is AssignedReferee or RefereeReady.");
            }
        }

        if (request.ReportType == RefereeReportTypes.PostRace)
        {
            if (race.Status != RaceStatuses.Finished &&
                race.Status != RaceStatuses.ResultPending)
            {
                return BadRequest("Post-race report can only be submitted when race status is Finished or ResultPending.");
            }
        }

        var report = new RefereeReport
        {
            RaceId = raceId,
            RefereeId = refereeId,
            ReportContent = request.ReportContent.Trim(),
            ReportType = request.ReportType,
            SubmittedAt = DateTime.UtcNow
        };

        _context.RefereeReports.Add(report);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Referee report submitted successfully",
            reportId = report.ReportId,
            reportType = report.ReportType
        });
    }

    [HttpGet("{raceId}/reports")]
    public async Task<IActionResult> GetReports(int raceId)
    {
        var refereeId = GetRefereeId();

        var assigned = await IsAssignedToActiveRaceAsync(raceId, refereeId);

        if (!assigned)
        {
            return Forbid();
        }

        var reports = await _context.RefereeReports
            .AsNoTracking()
            .Where(r =>
                r.RaceId == raceId &&
                r.RefereeId == refereeId &&
                r.Race.Status != RaceStatuses.Cancelled &&
                r.Race.Tournament.Status != TournamentStatuses.Cancelled)
            .OrderByDescending(r => r.SubmittedAt)
            .Select(r => new
            {
                reportId = r.ReportId,
                raceId = r.RaceId,
                reportContent = r.ReportContent,
                reportType = r.ReportType,
                submittedAt = r.SubmittedAt
            })
            .ToListAsync();

        return Ok(reports);
    }
}
