using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Referee;
using Eliteracingleague.API.Extensions;
using Eliteracingleague.API.Models;
using Eliteracingleague.API.Services;
using Eliteracingleague.API.Services.Notifications;
using Eliteracingleague.API.Services.Racing;
using Eliteracingleague.API.Services.SystemTime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Eliteracingleague.API.Controllers.Referee;

[Authorize(Roles = UserRoles.RaceReferee)]
[ApiController]
[Route("api/referee/races")]
public class RefereeRacesController : ControllerBase
{
    private readonly EliteRacingLeagueContext _context;
    private readonly RefereeRaceLifecycleService _lifecycleService;
    private readonly INotificationService _notificationService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly RaceResultValidationService _resultValidationService;

    private static readonly string[] ActiveRegistrationStatuses =
    {
        RaceRegistrationStatuses.ReadyToRace
    };

    // Registrations are marked Completed after a race is published. When an
    // admin reopens published results for correction, the same participants
    // must remain available to the referee without changing their historical
    // registration status back to ReadyToRace.
    private static readonly string[] PostRaceRegistrationStatuses =
    {
        RaceRegistrationStatuses.ReadyToRace,
        RaceRegistrationStatuses.Completed
    };

    private static readonly string[] ViolationAllowedRaceStatuses =
{
    RaceStatuses.AssignedReferee,
    RaceStatuses.RefereeReady,
    RaceStatuses.Ongoing,
    RaceStatuses.Finished,
    RaceStatuses.ResultPending
};

    public RefereeRacesController(
        EliteRacingLeagueContext context,
        RefereeRaceLifecycleService lifecycleService,
        INotificationService notificationService,
        IDateTimeProvider dateTimeProvider,
        RaceResultValidationService resultValidationService)
    {
        _context = context;
        _lifecycleService = lifecycleService;
        _notificationService = notificationService;
        _dateTimeProvider = dateTimeProvider;
        _resultValidationService = resultValidationService;
    }

    private bool TryGetRefereeId(out int refereeId)
    {
        return User.TryGetUserId(out refereeId);
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

    private Task<RefereeRaceSummaryResponse?> GetRefereeRaceSummaryAsync(int raceId)
    {
        return _context.Races
            .AsNoTracking()
            .Where(r =>
                r.RaceId == raceId &&
                r.Status != RaceStatuses.Cancelled &&
                r.Tournament.Status != TournamentStatuses.Cancelled)
            .Select(r => new RefereeRaceSummaryResponse
            {
                RaceId = r.RaceId,
                RaceName = r.RaceName,
                TournamentId = r.TournamentId,
                TournamentName = r.Tournament.TournamentName,
                TournamentImageUrl = r.Tournament.ImageUrl,
                ImageUrl = r.Tournament.ImageUrl,
                RaceDate = r.RaceDate,
                DistanceMeters = r.DistanceMeters,
                Location = r.Location,
                RaceStatus = r.Status,
                MaxHorses = r.MaxHorses
            })
            .FirstOrDefaultAsync();
    }

    private async Task<List<RefereeRaceRegistrationResponse>> GetRefereeRegistrationResponsesAsync(
        int raceId,
        bool usePendingInspectionFallback,
        bool includeInspectionReportFields,
        bool includeCompletedRegistrations = false,
        bool passedOnly = false)
    {
        var registrations = await _context.RaceRegistrations
            .AsNoTracking()
            .Where(registration =>
                registration.RaceId == raceId &&
                (includeCompletedRegistrations
                    ? PostRaceRegistrationStatuses.Contains(registration.Status)
                    : ActiveRegistrationStatuses.Contains(registration.Status)) &&
                (!passedOnly || _context.PreRaceInspections.Any(inspection =>
                    inspection.RaceId == raceId &&
                    inspection.RegistrationId == registration.RegistrationId &&
                    inspection.Status == PreRaceInspectionStatuses.Passed)) &&
                registration.Race.Status != RaceStatuses.Cancelled &&
                registration.Race.Tournament.Status != TournamentStatuses.Cancelled)
            .Select(registration => new RefereeRaceRegistrationResponse
            {
                RegistrationId = registration.RegistrationId,
                RegistrationCode = "#REG-" + registration.RegistrationId.ToString(),
                RegistrationStatus = registration.Status,
                Status = registration.Status,
                Horse = new RefereeHorseResponse
                {
                    HorseId = registration.Horse.HorseId,
                    HorseName = registration.Horse.HorseName,
                    ImageUrl = registration.Horse.ImageUrl,
                    BreedId = registration.Horse.BreedId,
                    BreedName = registration.Horse.Breed.BreedName,
                    Breed = registration.Horse.Breed.BreedName,
                    Age = registration.Horse.Age,
                    HeightCm = registration.Horse.HeightCm,
                    WeightKg = registration.Horse.WeightKg,
                    HealthStatus = registration.Horse.HealthStatus,
                    HealthCertificateImageUrl = registration.Horse.HealthCertificateImageUrl,
                    HealthCertificate = registration.Horse.HealthCertificateImageUrl,
                    IsActive = registration.Horse.IsActive,
                    AchievementSummary = registration.Horse.AchievementSummary
                },
                Owner = new RefereeOwnerResponse
                {
                    OwnerId = registration.OwnerId,
                    OwnerName = registration.Owner.Owner.FullName,
                    Email = registration.Owner.Owner.Email,
                    Phone = registration.Owner.Owner.Phone
                },
                Jockey = registration.Jockey == null
                    ? null
                    : new RefereeJockeyResponse
                    {
                        JockeyId = registration.Jockey.JockeyId,
                        JockeyName = registration.Jockey.JockeyNavigation.FullName,
                        ProfileImageUrl = registration.Jockey.ProfileImageUrl,
                        Avatar = registration.Jockey.ProfileImageUrl,
                        WeightKg = registration.Jockey.WeightKg,
                        YearsOfExperience = registration.Jockey.YearsOfExperience,
                        Experience = registration.Jockey.YearsOfExperience,
                        HealthStatus = registration.Jockey.HealthStatus,
                        CertificateNo = registration.Jockey.CertificateNo,
                        CertificateFileUrl = registration.Jockey.CertificateFileUrl,
                        HealthCertificateUrl = registration.Jockey.HealthCertificateUrl,
                        HealthCertificate = registration.Jockey.HealthCertificateUrl,
                        IsActive = registration.Jockey.IsActive,
                        Email = registration.Jockey.JockeyNavigation.Email,
                        Phone = registration.Jockey.JockeyNavigation.Phone
                    },
                HorseId = registration.HorseId,
                HorseName = registration.Horse.HorseName,
                HorseImageUrl = registration.Horse.ImageUrl,
                HorseHealthStatus = registration.Horse.HealthStatus,
                HealthCertificateImageUrl = registration.Horse.HealthCertificateImageUrl,
                OwnerId = registration.OwnerId,
                JockeyId = registration.JockeyId,
                JockeyName = registration.Jockey == null
                    ? null
                    : registration.Jockey.JockeyNavigation.FullName
            })
            .ToListAsync();

        if (registrations.Count == 0)
        {
            return registrations;
        }

        var registrationIds = registrations
            .Select(registration => registration.RegistrationId)
            .ToList();

        var inspections = await _context.PreRaceInspections
            .AsNoTracking()
            .Where(inspection =>
                inspection.RaceId == raceId &&
                registrationIds.Contains(inspection.RegistrationId))
            .Select(inspection => new
            {
                inspection.RegistrationId,
                Response = new RefereeInspectionResponse
                {
                    InspectionId = inspection.InspectionId,
                    Status = inspection.Status,
                    Note = inspection.Note,
                    InspectedAt = inspection.InspectedAt,
                    InspectedByRefereeId = inspection.RefereeId
                }
            })
            .ToListAsync();

        var inspectionByRegistrationId = inspections.ToDictionary(
            inspection => inspection.RegistrationId,
            inspection => inspection.Response);

        foreach (var registration in registrations)
        {
            if (inspectionByRegistrationId.TryGetValue(
                registration.RegistrationId,
                out var inspection))
            {
                registration.Inspection = inspection;
            }
            else if (usePendingInspectionFallback)
            {
                registration.Inspection = new RefereeInspectionResponse
                {
                    Status = PreRaceInspectionStatuses.PendingConfirmation
                };
            }

            if (includeInspectionReportFields)
            {
                ApplyInspectionReportFields(registration);
            }
        }

        return registrations;
    }

    private static void ApplyInspectionReportFields(
        RefereeRaceRegistrationResponse registration)
    {
        var inspectionStatus = registration.Inspection?.Status ??
            PreRaceInspectionStatuses.PendingConfirmation;

        registration.Checklist = inspectionStatus == PreRaceInspectionStatuses.Failed
            ? new[] { true, true, false, true }
            : new[] { true, true, true, true };
        registration.RuleRef = inspectionStatus == PreRaceInspectionStatuses.Failed
            ? "Rule 2.2"
            : "N/A";
        registration.Severity = inspectionStatus == PreRaceInspectionStatuses.Failed
            ? "HIGH"
            : inspectionStatus == PreRaceInspectionStatuses.PendingConfirmation
                ? "MEDIUM"
                : "-";
        registration.Violation = inspectionStatus == PreRaceInspectionStatuses.Failed
            ? registration.Inspection?.Note ?? "Violation detected."
            : inspectionStatus == PreRaceInspectionStatuses.PendingConfirmation
                ? "Awaiting referee confirmation."
                : "Full compliance with technical and health standards.";
        registration.Outcome = inspectionStatus == PreRaceInspectionStatuses.Failed
            ? "PROHIBITED"
            : inspectionStatus == PreRaceInspectionStatuses.PendingConfirmation
                ? "PENDING"
                : "ALLOWED";
    }

    [HttpGet]
    public async Task<IActionResult> GetAssignedRaces()
    {
        if (!TryGetRefereeId(out var refereeId))
        {
            return Unauthorized(new { message = "Token không hợp lệ hoặc thiếu UserId." });
        }

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
        if (!TryGetRefereeId(out var refereeId))
        {
            return Unauthorized(new { message = "Token không hợp lệ hoặc thiếu UserId." });
        }
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
        if (!TryGetRefereeId(out var refereeId))
        {
            return Unauthorized(new { message = "Token không hợp lệ hoặc thiếu UserId." });
        }

        var assigned = await IsAssignedToActiveRaceAsync(raceId, refereeId);

        if (!assigned)
        {
            return await BuildLifecycleAccessErrorAsync(raceId);
        }

        var raceStatus = await _context.Races
            .AsNoTracking()
            .Where(r =>
                r.RaceId == raceId &&
                r.Status != RaceStatuses.Cancelled &&
                r.Tournament.Status != TournamentStatuses.Cancelled)
            .Select(r => r.Status)
            .FirstOrDefaultAsync();

        if (raceStatus == null)
        {
            return NotFound(new { message = "Race not found or has been cancelled." });
        }

        var includeCompletedRegistrations = raceStatus is
            RaceStatuses.Finished or
            RaceStatuses.ResultPending or
            RaceStatuses.Published;

        var registrations = await GetRefereeRegistrationResponsesAsync(
            raceId,
            usePendingInspectionFallback: false,
            includeInspectionReportFields: false,
            includeCompletedRegistrations: includeCompletedRegistrations,
            passedOnly: includeCompletedRegistrations);

        return Ok(registrations);
    }

    [HttpPut("{raceId}/mark-ready")]
    public async Task<IActionResult> MarkReady(int raceId)
    {
        if (!TryGetRefereeId(out var refereeId))
        {
            return Unauthorized(new { message = "Token không hợp lệ hoặc thiếu UserId." });
        }

        var lifecycle = await _lifecycleService.GetLifecycleAsync(raceId, refereeId);

        if (lifecycle == null)
        {
            return await BuildLifecycleAccessErrorAsync(raceId);
        }

        if (lifecycle.RaceStatus != RaceStatuses.AssignedReferee)
        {
            return BadRequest(new
            {
                message = "Race must be AssignedReferee before it can be marked ready."
            });
        }

        var race = await _context.Races
            .Include(r => r.Tournament)
                .ThenInclude(t => t.Season)
            .FirstOrDefaultAsync(r => r.RaceId == raceId);

        if (race == null)
        {
            return NotFound(new { message = "Race not found." });
        }

        if (race.Tournament.Season.Status != SeasonStatuses.Active)
        {
            return BadRequest(new
            {
                message = "Race cannot be marked ready because the season is not active.",
                seasonId = race.Tournament.SeasonId,
                seasonStatus = race.Tournament.Season.Status
            });
        }

        if (race.Tournament.Status != TournamentStatuses.ClosedRegistration)
        {
            return BadRequest(new
            {
                message = "Registration must be closed before the race can be marked ready.",
                tournamentStatus = race.Tournament.Status
            });
        }

        if (lifecycle.Counts.TotalRegistrations == 0)
        {
            return BadRequest(new
            {
                message = "Race cannot be marked ready because no eligible registrations were found."
            });
        }

        if (lifecycle.Counts.PendingInspections > 0)
        {
            return BadRequest(new
            {
                message = "Race cannot be marked ready because there are pending inspections."
            });
        }

        if (lifecycle.Counts.FailedInspections >= lifecycle.Counts.TotalRegistrations)
        {
            return BadRequest(new
            {
                message = "Race cannot be marked ready because all inspections failed."
            });
        }

        race.Status = RaceStatuses.RefereeReady;
        race.UpdatedAt = _dateTimeProvider.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(await _lifecycleService.GetLifecycleAsync(raceId, refereeId));
    }

    [HttpPut("{raceId}/start")]
    public async Task<IActionResult> StartRace(int raceId)
    {
        if (!TryGetRefereeId(out var refereeId))
        {
            return Unauthorized(new { message = "Token không hợp lệ hoặc thiếu UserId." });
        }

        var lifecycle = await _lifecycleService.GetLifecycleAsync(raceId, refereeId);

        if (lifecycle == null)
        {
            return await BuildLifecycleAccessErrorAsync(raceId);
        }

        if (lifecycle.RaceStatus != RaceStatuses.RefereeReady)
        {
            return BadRequest(new
            {
                message = "Race must be RefereeReady before it can start."
            });
        }

        if (lifecycle.Counts.PendingInspections > 0)
        {
            return BadRequest(new
            {
                message = "Race cannot start because there are pending inspections."
            });
        }

        if (lifecycle.Counts.TotalRegistrations == 0 ||
            lifecycle.Counts.FailedInspections >= lifecycle.Counts.TotalRegistrations)
        {
            return BadRequest(new
            {
                message = "Race cannot start because no eligible registrations were found."
            });
        }

        var race = await _context.Races
            .Include(r => r.Tournament)
                .ThenInclude(t => t.Season)
            .FirstOrDefaultAsync(r => r.RaceId == raceId);

        if (race == null)
        {
            return NotFound(new { message = "Race not found." });
        }

        if (race.Tournament.Season.Status != SeasonStatuses.Active)
        {
            return BadRequest(new
            {
                message = "Race cannot start because the season is not active.",
                seasonId = race.Tournament.SeasonId,
                seasonStatus = race.Tournament.Season.Status
            });
        }

        if (race.Tournament.Status != TournamentStatuses.ClosedRegistration)
        {
            return BadRequest(new
            {
                message = "Registration must be closed before the race can start.",
                tournamentStatus = race.Tournament.Status
            });
        }

        var localNow = _dateTimeProvider.GetLocalNow(_dateTimeProvider.TimeZoneId);

        if (localNow < race.RaceDate)
        {
            return BadRequest(new
            {
                message = "Race cannot start before its scheduled time.",
                raceDate = race.RaceDate,
                serverLocalNow = localNow
            });
        }

        var utcNow = _dateTimeProvider.UtcNow;

        race.Status = RaceStatuses.Ongoing;
        race.UpdatedAt = utcNow;
        race.Tournament.Status = TournamentStatuses.Ongoing;
        race.Tournament.UpdatedAt = utcNow;

        await _context.SaveChangesAsync();

        return Ok(await _lifecycleService.GetLifecycleAsync(raceId, refereeId));
    }

    [HttpPut("{raceId}/finish")]
    public async Task<IActionResult> FinishRace(int raceId)
    {
        if (!TryGetRefereeId(out var refereeId))
        {
            return Unauthorized(new { message = "Token không hợp lệ hoặc thiếu UserId." });
        }

        var lifecycle = await _lifecycleService.GetLifecycleAsync(raceId, refereeId);

        if (lifecycle == null)
        {
            return await BuildLifecycleAccessErrorAsync(raceId);
        }

        if (lifecycle.RaceStatus != RaceStatuses.Ongoing)
        {
            return BadRequest(new
            {
                message = "Race must be Ongoing before it can finish."
            });
        }

        var race = await _context.Races
            .FirstOrDefaultAsync(r => r.RaceId == raceId);

        if (race == null)
        {
            return NotFound(new { message = "Race not found." });
        }

        race.Status = RaceStatuses.Finished;
        race.UpdatedAt = _dateTimeProvider.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(await _lifecycleService.GetLifecycleAsync(raceId, refereeId));
    }

    [HttpPost("{raceId}/inspections")]
    public async Task<IActionResult> CreateOrUpdateInspection(
        int raceId,
        CreateInspectionRequest request)
    {
        if (!TryGetRefereeId(out var refereeId))
        {
            return Unauthorized(new { message = "Token không hợp lệ hoặc thiếu UserId." });
        }

        if (!PreRaceInspectionStatuses.IsValid(request.Status))
        {
            return BadRequest(new { message = "Invalid inspection status." });
        }

        var assigned = await IsAssignedToActiveRaceAsync(raceId, refereeId);

        if (!assigned)
        {
            return await BuildLifecycleAccessErrorAsync(raceId);
        }

        var race = await _context.Races
            .AsNoTracking()
            .Include(r => r.Tournament)
            .FirstOrDefaultAsync(r =>
                r.RaceId == raceId &&
                r.Status != RaceStatuses.Cancelled &&
                r.Tournament.Status != TournamentStatuses.Cancelled);

        if (race == null)
        {
            return NotFound(new { message = "Race not found or has been cancelled." });
        }

        if (race.Tournament.Status != TournamentStatuses.ClosedRegistration &&
            race.Tournament.Status != TournamentStatuses.Ongoing)
        {
            return BadRequest(new
            {
                message = "Pre-race inspection is only available after registration is closed.",
                tournamentStatus = race.Tournament.Status
            });
        }

        if (race.Status != RaceStatuses.AssignedReferee)
        {
            return BadRequest(new
            {
                message = "Pre-race inspection can only be updated when race status is AssignedReferee.",
                raceStatus = race.Status
            });
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
            return BadRequest(new { message = "Registration does not belong to this race or is not ready to race." });
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
                InspectedAt = _dateTimeProvider.UtcNow
            };

            _context.PreRaceInspections.Add(inspection);
        }
        else
        {
            inspection.Status = request.Status;
            inspection.Note = request.Note;
            inspection.InspectedAt = _dateTimeProvider.UtcNow;
        }

        // A horse that fails pre-race inspection never participates in the race,
        // so it must not keep a post-race result row from an earlier test/correction.
        if (request.Status == PreRaceInspectionStatuses.Failed)
        {
            var staleResults = await _context.RaceResults
                .Where(result =>
                    result.RaceId == raceId &&
                    result.RegistrationId == request.RegistrationId)
                .ToListAsync();

            if (staleResults.Any(result =>
                    result.Status is RaceResultStatuses.AdminApproved or
                        RaceResultStatuses.Published))
            {
                return Conflict(new
                {
                    code = "PUBLISHED_RESULT_EXISTS",
                    message = "This horse already has an approved/published result. Reopen the published race before changing the pre-race inspection."
                });
            }

            if (staleResults.Count > 0)
            {
                _context.RaceResults.RemoveRange(staleResults);
            }
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
        if (!TryGetRefereeId(out var refereeId))
        {
            return Unauthorized(new { message = "Token không hợp lệ hoặc thiếu UserId." });
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

        if (race.Status != RaceStatuses.Finished)
        {
            return BadRequest("Race results can only be entered when race status is Finished.");
        }

        var lockedFinalReportStatus = await _context.RefereeReports
            .AsNoTracking()
            .Where(r =>
                r.RaceId == raceId &&
                r.RefereeId == refereeId &&
                r.ReportType == RefereeReportTypes.PostRace)
            .Select(r => r.Status)
            .FirstOrDefaultAsync();

        if (lockedFinalReportStatus is RefereeReportStatuses.Submitted or RefereeReportStatuses.Approved)
        {
            return Conflict(new
            {
                code = "POST_RACE_SUBMISSION_LOCKED",
                message = "Race results are locked while the final report is waiting for admin review or has already been approved.",
                reportStatus = lockedFinalReportStatus
            });
        }

        var outcomeStatus = RaceResultValidationService.NormalizeOutcome(request.OutcomeStatus);

        if (!RaceOutcomeStatuses.IsValid(outcomeStatus))
        {
            return BadRequest(new
            {
                message = "Invalid race outcome.",
                receivedOutcome = request.OutcomeStatus,
                allowedOutcomes = RaceOutcomeStatuses.All
            });
        }

        var normalizedFinishPosition = outcomeStatus == RaceOutcomeStatuses.Finished
            ? request.FinishPosition
            : null;
        var normalizedFinishTimeSeconds = outcomeStatus == RaceOutcomeStatuses.Finished
            ? request.FinishTimeSeconds
            : null;

        if (outcomeStatus == RaceOutcomeStatuses.Finished)
        {
            if (!normalizedFinishPosition.HasValue || normalizedFinishPosition.Value <= 0)
                return BadRequest("Finished outcome requires a finish position greater than 0.");
            if (!normalizedFinishTimeSeconds.HasValue || normalizedFinishTimeSeconds.Value <= 0)
                return BadRequest("Finished outcome requires a finish time greater than 0.");
        }

        if (request.Score.HasValue && request.Score.Value < 0)
        {
            return BadRequest("Score cannot be negative.");
        }

        var registration = await _context.RaceRegistrations
            .FirstOrDefaultAsync(r =>
                r.RaceId == raceId &&
                r.RegistrationId == request.RegistrationId &&
                PostRaceRegistrationStatuses.Contains(r.Status) &&
                _context.PreRaceInspections.Any(inspection =>
                    inspection.RaceId == raceId &&
                    inspection.RegistrationId == r.RegistrationId &&
                    inspection.Status == PreRaceInspectionStatuses.Passed) &&
                r.Race.Status != RaceStatuses.Cancelled &&
                r.Race.Tournament.Status != TournamentStatuses.Cancelled);

        if (registration == null)
        {
            var failedInspection = await _context.PreRaceInspections
                .AsNoTracking()
                .AnyAsync(inspection =>
                    inspection.RaceId == raceId &&
                    inspection.RegistrationId == request.RegistrationId &&
                    inspection.Status == PreRaceInspectionStatuses.Failed);

            if (failedInspection)
            {
                return Conflict(new
                {
                    code = "PRE_RACE_FAILED_NOT_ELIGIBLE",
                    message = "This horse failed pre-race inspection and is shown only in Violations. It cannot receive a post-race result."
                });
            }

            return NotFound("Registration not found or did not pass pre-race inspection.");
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

        if (outcomeStatus == RaceOutcomeStatuses.Finished && normalizedFinishPosition.HasValue)
        {
            var duplicatePosition = await _context.RaceResults
                .AnyAsync(r =>
                    r.RaceId == raceId &&
                    r.FinishPosition == normalizedFinishPosition.Value &&
                    (result == null || r.ResultId != result.ResultId));

            if (duplicatePosition)
            {
                return BadRequest(normalizedFinishPosition.Value == 1
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
                FinishTimeSeconds = normalizedFinishTimeSeconds,
                FinishPosition = normalizedFinishPosition,
                Score = request.Score,
                OutcomeStatus = outcomeStatus,
                Status = RaceResultStatuses.Draft,
                EnteredByRefereeId = refereeId,
                Note = request.Note,
                CreatedAt = _dateTimeProvider.UtcNow
            };

            _context.RaceResults.Add(result);
        }
        else
        {
            result.FinishTimeSeconds = normalizedFinishTimeSeconds;
            result.FinishPosition = normalizedFinishPosition;
            result.Score = request.Score;
            result.OutcomeStatus = outcomeStatus;
            result.Note = request.Note;
            result.Status = RaceResultStatuses.Draft;
            result.AdminConfirmedBy = null;
            result.PublishedAt = null;
            result.UpdatedAt = _dateTimeProvider.UtcNow;
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
        if (!TryGetRefereeId(out var refereeId))
        {
            return Unauthorized(new { message = "Token không hợp lệ hoặc thiếu UserId." });
        }

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

        var singleValidation = _resultValidationService.ValidateForPublication(
            new[] { result },
            new HashSet<int> { result.RegistrationId },
            new HashSet<int>());
        if (singleValidation.Any(error => error.Contains("requires", StringComparison.OrdinalIgnoreCase) ||
                                          error.Contains("invalid outcome", StringComparison.OrdinalIgnoreCase)))
            return BadRequest(new { message = "Result is incomplete.", errors = singleValidation });

        result.Status = RaceResultStatuses.RefereeConfirmed;
        result.UpdatedAt = _dateTimeProvider.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Result confirmed by referee",
            resultId = result.ResultId,
            status = result.Status
        });
    }

    [HttpPut("{raceId}/results/confirm-all")]
    public async Task<IActionResult> ConfirmAllResults(
        int raceId,
        CancellationToken cancellationToken)
    {
        if (!TryGetRefereeId(out var refereeId))
        {
            return Unauthorized(new { message = "Token không hợp lệ hoặc thiếu UserId." });
        }

        if (!await IsAssignedToActiveRaceAsync(raceId, refereeId))
        {
            return Forbid();
        }

        var race = await _context.Races
            .FirstOrDefaultAsync(r =>
                r.RaceId == raceId &&
                r.Status != RaceStatuses.Cancelled &&
                r.Tournament.Status != TournamentStatuses.Cancelled,
                cancellationToken);

        if (race == null)
        {
            return NotFound("Race not found or has been cancelled.");
        }

        if (race.Status is not RaceStatuses.Finished and not RaceStatuses.ResultPending)
        {
            return BadRequest(new
            {
                message = "All results can only be confirmed when race status is Finished.",
                raceStatus = race.Status
            });
        }

        var lockedFinalReport = await _context.RefereeReports
            .AsNoTracking()
            .Where(r =>
                r.RaceId == raceId &&
                r.RefereeId == refereeId &&
                r.ReportType == RefereeReportTypes.PostRace &&
                (r.Status == RefereeReportStatuses.Submitted ||
                 r.Status == RefereeReportStatuses.Approved))
            .Select(r => new { r.ReportId, r.Status })
            .FirstOrDefaultAsync(cancellationToken);

        if (lockedFinalReport != null)
        {
            return Conflict(new
            {
                code = "POST_RACE_SUBMISSION_LOCKED",
                message = "The final report has already been submitted. Admin must return the submission before results can be edited or confirmed again.",
                reportId = lockedFinalReport.ReportId,
                reportStatus = lockedFinalReport.Status
            });
        }

        var activeRegistrationIds = await _context.RaceRegistrations
            .AsNoTracking()
            .Where(r =>
                r.RaceId == raceId &&
                PostRaceRegistrationStatuses.Contains(r.Status) &&
                _context.PreRaceInspections.Any(inspection =>
                    inspection.RaceId == raceId &&
                    inspection.RegistrationId == r.RegistrationId &&
                    inspection.Status == PreRaceInspectionStatuses.Passed))
            .Select(r => r.RegistrationId)
            .ToListAsync(cancellationToken);

        if (activeRegistrationIds.Count == 0)
        {
            return BadRequest(new { message = "No eligible registrations found." });
        }

        var registrationIdSet = activeRegistrationIds.ToHashSet();
        var results = await _context.RaceResults
            .Where(r =>
                r.RaceId == raceId &&
                registrationIdSet.Contains(r.RegistrationId))
            .ToListAsync(cancellationToken);

        var missingRegistrationIds = registrationIdSet
            .Except(results.Select(r => r.RegistrationId))
            .OrderBy(id => id)
            .ToList();

        if (missingRegistrationIds.Count > 0)
        {
            return BadRequest(new
            {
                code = "MISSING_RACE_RESULTS",
                message = "Every horse that passed pre-race inspection must have a post-race result.",
                missingRegistrationIds
            });
        }

        if (results.Any(r => r.EnteredByRefereeId != refereeId))
        {
            return Forbid();
        }

        if (results.Any(r =>
                r.Status is RaceResultStatuses.AdminApproved or RaceResultStatuses.Published))
        {
            return Conflict(new
            {
                message = "Admin-approved or published results cannot be confirmed by the referee."
            });
        }

        var disqualifiedIds = (await _context.RaceViolations
            .AsNoTracking()
            .Where(v =>
                v.RaceId == raceId &&
                v.Action == RaceViolationActions.Disqualified &&
                registrationIdSet.Contains(v.RegistrationId))
            .Select(v => v.RegistrationId)
            .Distinct()
            .ToListAsync(cancellationToken))
            .ToHashSet();

        foreach (var result in results)
        {
            if (disqualifiedIds.Contains(result.RegistrationId))
            {
                result.OutcomeStatus = RaceOutcomeStatuses.Disqualified;
                result.FinishPosition = null;
                result.FinishTimeSeconds = null;
            }

            if (result.Status is RaceResultStatuses.Draft or RaceResultStatuses.Returned)
            {
                result.Status = RaceResultStatuses.RefereeConfirmed;
                result.AdminConfirmedBy = null;
                result.PublishedAt = null;
                result.UpdatedAt = _dateTimeProvider.UtcNow;
            }
        }

        var validationErrors = _resultValidationService
            .ValidateForPublication(results, registrationIdSet, disqualifiedIds)
            .Distinct()
            .ToList();

        if (validationErrors.Count > 0)
        {
            return BadRequest(new
            {
                code = "INVALID_RACE_RESULTS",
                message = "Race results are incomplete or invalid.",
                errors = validationErrors
            });
        }

        var wasAlreadyPending = race.Status == RaceStatuses.ResultPending;
        race.Status = RaceStatuses.ResultPending;
        race.UpdatedAt = _dateTimeProvider.UtcNow;

        await using var transaction = await _context.Database
            .BeginTransactionAsync(cancellationToken);

        try
        {
            if (!wasAlreadyPending)
            {
                await _notificationService.CreateForAdminsAsync(
                    "Race Result Submitted",
                    $"Referee submitted all results for {race.RaceName}. Please validate.",
                    "RaceResultValidation",
                    "/admin/validate-results",
                    "Race",
                    raceId,
                    cancellationToken);
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Ok(await _lifecycleService.GetLifecycleAsync(raceId, refereeId));
    }

    [HttpPost("{raceId}/violations")]
    public async Task<IActionResult> CreateViolation(
        int raceId,
        CreateViolationRequest request)
    {
        if (!TryGetRefereeId(out var refereeId))
        {
            return Unauthorized(new { message = "Token không hợp lệ hoặc thiếu UserId." });
        }

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
                PostRaceRegistrationStatuses.Contains(r.Status) &&
                _context.PreRaceInspections.Any(inspection =>
                    inspection.RaceId == raceId &&
                    inspection.RegistrationId == r.RegistrationId &&
                    inspection.Status == PreRaceInspectionStatuses.Passed) &&
                r.Race.Status != RaceStatuses.Cancelled &&
                r.Race.Tournament.Status != TournamentStatuses.Cancelled);

        if (!registrationExists)
        {
            return NotFound("Registration not found, cancelled, or did not pass pre-race inspection.");
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
            CreatedAt = _dateTimeProvider.UtcNow
        };

        _context.RaceViolations.Add(violation);

        if (violation.Action == RaceViolationActions.Disqualified)
        {
            var result = await _context.RaceResults.FirstOrDefaultAsync(r =>
                r.RaceId == raceId && r.RegistrationId == request.RegistrationId);
            if (result != null && result.Status != RaceResultStatuses.Published)
            {
                result.OutcomeStatus = RaceOutcomeStatuses.Disqualified;
                result.FinishPosition = null;
                result.FinishTimeSeconds = null;
                result.UpdatedAt = _dateTimeProvider.UtcNow;
            }
        }

        await _context.SaveChangesAsync();

        await _notificationService.CreateForAdminsAsync(
            "Race Violation Reported",
            $"Referee reported a violation in race {race.RaceName}.",
            "RaceViolation",
            "/admin/validate-results",
            "RaceViolation",
            violation.ViolationId);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Violation recorded successfully",
            violationId = violation.ViolationId,
            action = violation.Action
        });
    }

    [HttpPut("{raceId}/violations/{violationId:int}")]
    public async Task<IActionResult> UpdateViolation(
    int raceId,
    int violationId,
    UpdateViolationRequest request)
    {
        if (!TryGetRefereeId(out var refereeId))
        {
            return Unauthorized(new { message = "Token không hợp lệ hoặc thiếu UserId." });
        }

        if (!RaceViolationActions.IsValid(request.Action))
        {
            return BadRequest("Invalid violation action.");
        }

        if (string.IsNullOrWhiteSpace(request.ViolationType))
        {
            return BadRequest("Violation type is required.");
        }

        if (request.PenaltyPoints < 0)
        {
            return BadRequest("Penalty points must be greater than or equal to 0.");
        }

        if (request.Action == RaceViolationActions.PointDeduction &&
            (!request.PenaltyPoints.HasValue || request.PenaltyPoints.Value <= 0))
        {
            return BadRequest("Point deduction requires penalty points greater than 0.");
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
            return BadRequest("Violations can only be updated before publishing or cancellation.");
        }

        var registrationExists = await _context.RaceRegistrations
            .AnyAsync(r =>
                r.RaceId == raceId &&
                r.RegistrationId == request.RegistrationId &&
                PostRaceRegistrationStatuses.Contains(r.Status) &&
                _context.PreRaceInspections.Any(inspection =>
                    inspection.RaceId == raceId &&
                    inspection.RegistrationId == r.RegistrationId &&
                    inspection.Status == PreRaceInspectionStatuses.Passed) &&
                r.Race.Status != RaceStatuses.Cancelled &&
                r.Race.Tournament.Status != TournamentStatuses.Cancelled);

        if (!registrationExists)
        {
            return NotFound("Registration not found, cancelled, or did not pass pre-race inspection.");
        }

        var violation = await _context.RaceViolations
            .FirstOrDefaultAsync(v =>
                v.ViolationId == violationId &&
                v.RaceId == raceId &&
                v.RefereeId == refereeId);

        if (violation == null)
        {
            return NotFound("Violation not found.");
        }

        violation.RegistrationId = request.RegistrationId;
        violation.ViolationType = request.ViolationType.Trim();
        violation.Description = string.IsNullOrWhiteSpace(request.Description)
            ? null
            : request.Description.Trim();
        violation.Action = request.Action;
        violation.PenaltyPoints = request.PenaltyPoints;

        if (violation.Action == RaceViolationActions.Disqualified)
        {
            var result = await _context.RaceResults.FirstOrDefaultAsync(r =>
                r.RaceId == raceId && r.RegistrationId == request.RegistrationId);
            if (result != null && result.Status != RaceResultStatuses.Published)
            {
                result.OutcomeStatus = RaceOutcomeStatuses.Disqualified;
                result.FinishPosition = null;
                result.FinishTimeSeconds = null;
                result.UpdatedAt = _dateTimeProvider.UtcNow;
            }
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Violation updated successfully",
            violationId = violation.ViolationId,
            action = violation.Action
        });
    }

    [HttpDelete("{raceId}/violations/{violationId:int}")]
    public async Task<IActionResult> DeleteViolation(int raceId, int violationId)
    {
        if (!TryGetRefereeId(out var refereeId))
        {
            return Unauthorized(new { message = "Token không hợp lệ hoặc thiếu UserId." });
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
            return BadRequest("Violations can only be deleted before publishing or cancellation.");
        }

        var violation = await _context.RaceViolations
            .FirstOrDefaultAsync(v =>
                v.ViolationId == violationId &&
                v.RaceId == raceId &&
                v.RefereeId == refereeId);

        if (violation == null)
        {
            return NotFound("Violation not found.");
        }

        _context.RaceViolations.Remove(violation);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Violation deleted successfully",
            violationId = violationId,
            raceId = raceId
        });
    }

    [HttpGet("{raceId}/inspection-report")]
    public async Task<IActionResult> GetInspectionReport(int raceId, string? filter = "all")
    {
        if (!TryGetRefereeId(out var refereeId))
        {
            return Unauthorized(new { message = "Token không hợp lệ hoặc thiếu UserId." });
        }

        var assigned = await IsAssignedToActiveRaceAsync(raceId, refereeId);

        if (!assigned)
        {
            return await BuildLifecycleAccessErrorAsync(raceId);
        }

        var race = await GetRefereeRaceSummaryAsync(raceId);

        if (race == null)
        {
            return NotFound(new { message = "Race not found or has been cancelled." });
        }

        var registrations = await GetRefereeRegistrationResponsesAsync(
            raceId,
            usePendingInspectionFallback: true,
            includeInspectionReportFields: true);

        var allCount = registrations.Count;
        var failedCount = registrations.Count(registration =>
            registration.Inspection?.Status == PreRaceInspectionStatuses.Failed);
        var inspectedCount = registrations.Count(registration =>
            registration.Inspection?.Status != PreRaceInspectionStatuses.PendingConfirmation);
        var pendingCount = Math.Max(0, allCount - inspectedCount);
        var filteredRegistrations = registrations.AsEnumerable();

        if (filter == "flagged")
        {
            filteredRegistrations = filteredRegistrations.Where(registration =>
                registration.Inspection?.Status == PreRaceInspectionStatuses.Failed);
        }

        if (filter == "pending")
        {
            filteredRegistrations = filteredRegistrations.Where(registration =>
                registration.Inspection?.Status == PreRaceInspectionStatuses.PendingConfirmation);
        }

        return Ok(new PreRaceInspectionReportResponse
        {
            Race = race,
            Counts = new PreRaceInspectionReportCountsResponse
            {
                All = allCount,
                Flagged = failedCount,
                Pending = pendingCount
            },
            Items = filteredRegistrations.ToList()
        });
    }

    [HttpPost("{raceId}/reports")]
    public async Task<IActionResult> CreateReport(
        int raceId,
        CreateRefereeReportRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetRefereeId(out var refereeId))
        {
            return Unauthorized(new { message = "Token không hợp lệ hoặc thiếu UserId." });
        }

        if (!RefereeReportTypes.IsValid(request.ReportType))
        {
            return BadRequest(new { message = "Invalid report type." });
        }

        if (string.IsNullOrWhiteSpace(request.ReportContent))
        {
            return BadRequest(new { message = "Report content is required." });
        }

        var assigned = await IsAssignedToActiveRaceAsync(raceId, refereeId);
        if (!assigned)
        {
            return Forbid();
        }

        var race = await _context.Races
            .FirstOrDefaultAsync(r =>
                r.RaceId == raceId &&
                r.Status != RaceStatuses.Cancelled &&
                r.Tournament.Status != TournamentStatuses.Cancelled,
                cancellationToken);

        if (race == null)
        {
            return NotFound(new { message = "Race not found or has been cancelled." });
        }

        var reportStageError = ValidateReportStage(request.ReportType, race.Status);
        if (reportStageError != null)
        {
            return BadRequest(new
            {
                message = reportStageError,
                reportType = request.ReportType,
                raceStatus = race.Status
            });
        }

        if (request.ReportType == RefereeReportTypes.PostRace)
        {
            if (race.Status != RaceStatuses.ResultPending)
            {
                return Conflict(new
                {
                    code = "RESULTS_NOT_CONFIRMED",
                    message = "Confirm all race results before submitting the final post-race report.",
                    raceStatus = race.Status
                });
            }

            var activeRegistrationIds = await _context.RaceRegistrations
                .AsNoTracking()
                .Where(r =>
                    r.RaceId == raceId &&
                    PostRaceRegistrationStatuses.Contains(r.Status) &&
                    _context.PreRaceInspections.Any(inspection =>
                        inspection.RaceId == raceId &&
                        inspection.RegistrationId == r.RegistrationId &&
                        inspection.Status == PreRaceInspectionStatuses.Passed))
                .Select(r => r.RegistrationId)
                .ToListAsync(cancellationToken);

            var confirmedResults = await _context.RaceResults
                .AsNoTracking()
                .Where(r =>
                    r.RaceId == raceId &&
                    activeRegistrationIds.Contains(r.RegistrationId))
                .Select(r => new
                {
                    r.RegistrationId,
                    r.Status
                })
                .ToListAsync(cancellationToken);

            var missingRegistrationIds = activeRegistrationIds
                .Except(confirmedResults.Select(r => r.RegistrationId))
                .OrderBy(id => id)
                .ToList();
            var unconfirmedResultIds = confirmedResults
                .Where(r => r.Status != RaceResultStatuses.RefereeConfirmed)
                .Select(r => r.RegistrationId)
                .OrderBy(id => id)
                .ToList();

            if (activeRegistrationIds.Count == 0 ||
                missingRegistrationIds.Count > 0 ||
                unconfirmedResultIds.Count > 0)
            {
                return Conflict(new
                {
                    code = "RESULTS_NOT_CONFIRMED",
                    message = "The final report can only be submitted after every horse that passed pre-race inspection has a referee-confirmed result.",
                    expectedResults = activeRegistrationIds.Count,
                    actualResults = confirmedResults.Count,
                    missingRegistrationIds,
                    unconfirmedRegistrationIds = unconfirmedResultIds
                });
            }
        }

        var existingReport = await _context.RefereeReports
            .AsNoTracking()
            .Where(r =>
                r.RaceId == raceId &&
                r.RefereeId == refereeId &&
                r.ReportType == request.ReportType)
            .Select(r => new { r.ReportId, r.Status })
            .FirstOrDefaultAsync(cancellationToken);

        if (existingReport != null)
        {
            return Conflict(new
            {
                message = request.ReportType == RefereeReportTypes.PostRace &&
                    existingReport.Status == RefereeReportStatuses.Returned
                        ? "The final report was returned. Use the resubmit endpoint to revise and send it again."
                        : "This referee already submitted this report type for the race.",
                reportId = existingReport.ReportId,
                status = existingReport.Status,
                reportType = request.ReportType
            });
        }

        var now = _dateTimeProvider.UtcNow;
        var report = new RefereeReport
        {
            RaceId = raceId,
            RefereeId = refereeId,
            ReportContent = request.ReportContent.Trim(),
            ReportType = request.ReportType,
            Status = RefereeReportStatuses.Submitted,
            RevisionNumber = 1,
            SubmittedAt = now,
            UpdatedAt = now
        };

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            _context.RefereeReports.Add(report);
            await _context.SaveChangesAsync(cancellationToken);

            if (request.ReportType == RefereeReportTypes.PostRace)
            {
                await _notificationService.CreateForAdminsAsync(
                    "Final referee report submitted",
                    $"A referee submitted the final report for race {race.RaceName}.",
                    "PostRaceReportSubmitted",
                    $"/admin/results?raceId={report.RaceId}&reportId={report.ReportId}",
                    "RefereeReport",
                    report.ReportId,
                    cancellationToken);

                await _context.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Ok(new
        {
            message = request.ReportType == RefereeReportTypes.PostRace
                ? "Final report submitted successfully. The report is now locked while waiting for admin review."
                : "Referee report submitted successfully.",
            reportId = report.ReportId,
            reportType = report.ReportType,
            status = report.Status,
            revisionNumber = report.RevisionNumber,
            canEdit = false,
            isLocked = true
        });
    }

    [HttpPut("{raceId}/reports/{reportId:int}")]
    public async Task<IActionResult> UpdateReport(
        int raceId,
        int reportId,
        CreateRefereeReportRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetRefereeId(out var refereeId))
        {
            return Unauthorized(new { message = "Token không hợp lệ hoặc thiếu UserId." });
        }

        if (!RefereeReportTypes.IsValid(request.ReportType))
        {
            return BadRequest(new { message = "Invalid report type." });
        }

        if (string.IsNullOrWhiteSpace(request.ReportContent))
        {
            return BadRequest(new { message = "Report content is required." });
        }

        var assigned = await IsAssignedToActiveRaceAsync(raceId, refereeId);
        if (!assigned)
        {
            return Forbid();
        }

        var report = await _context.RefereeReports
            .FirstOrDefaultAsync(r =>
                r.ReportId == reportId &&
                r.RaceId == raceId &&
                r.RefereeId == refereeId,
                cancellationToken);

        if (report == null)
        {
            return NotFound(new { message = "Report not found or you do not have permission to update it." });
        }

        if (report.ReportType != request.ReportType)
        {
            return BadRequest(new
            {
                message = "Report type cannot be changed.",
                currentReportType = report.ReportType,
                requestedReportType = request.ReportType
            });
        }

        if (report.ReportType == RefereeReportTypes.PostRace)
        {
            return Conflict(new
            {
                code = "FINAL_REPORT_LOCKED",
                message = report.Status == RefereeReportStatuses.Returned
                    ? "The final report can only be edited through the resubmit endpoint after it is returned by the admin."
                    : "The final report is locked and cannot be edited while it is waiting for review or after approval.",
                reportId = report.ReportId,
                status = report.Status,
                canEdit = report.Status == RefereeReportStatuses.Returned,
                resubmitUrl = $"/api/referee/races/{raceId}/reports/{reportId}/resubmit"
            });
        }

        var race = await _context.Races
            .FirstOrDefaultAsync(r =>
                r.RaceId == raceId &&
                r.Status != RaceStatuses.Cancelled &&
                r.Tournament.Status != TournamentStatuses.Cancelled,
                cancellationToken);

        if (race == null)
        {
            return NotFound(new { message = "Race not found or has been cancelled." });
        }

        var reportStageError = ValidateReportStage(report.ReportType, race.Status);
        if (reportStageError != null)
        {
            return BadRequest(new
            {
                message = reportStageError,
                reportType = report.ReportType,
                raceStatus = race.Status
            });
        }

        report.ReportContent = request.ReportContent.Trim();
        report.UpdatedAt = _dateTimeProvider.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "Pre-race report updated successfully.",
            reportId = report.ReportId,
            reportType = report.ReportType,
            status = report.Status
        });
    }

    [HttpPut("{raceId}/reports/{reportId:int}/resubmit")]
    public async Task<IActionResult> ResubmitFinalReport(
        int raceId,
        int reportId,
        CreateRefereeReportRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetRefereeId(out var refereeId))
        {
            return Unauthorized(new { message = "Token không hợp lệ hoặc thiếu UserId." });
        }

        if (string.IsNullOrWhiteSpace(request.ReportContent))
        {
            return BadRequest(new { message = "Report content is required." });
        }

        if (!string.Equals(
                request.ReportType,
                RefereeReportTypes.PostRace,
                StringComparison.Ordinal))
        {
            return BadRequest(new
            {
                message = "The resubmit endpoint only supports the final post-race report.",
                requiredReportType = RefereeReportTypes.PostRace
            });
        }

        var assigned = await IsAssignedToActiveRaceAsync(raceId, refereeId);
        if (!assigned)
        {
            return Forbid();
        }

        var report = await _context.RefereeReports
            .Include(r => r.Race)
            .FirstOrDefaultAsync(r =>
                r.ReportId == reportId &&
                r.RaceId == raceId &&
                r.RefereeId == refereeId,
                cancellationToken);

        if (report == null)
        {
            return NotFound(new { message = "Report not found or you do not have permission to resubmit it." });
        }

        if (report.ReportType != RefereeReportTypes.PostRace)
        {
            return BadRequest(new { message = "Only a final post-race report can be resubmitted." });
        }

        if (report.Status != RefereeReportStatuses.Returned)
        {
            return Conflict(new
            {
                code = "REPORT_NOT_RETURNED",
                message = "Only a report returned by the admin can be revised and resubmitted.",
                reportId = report.ReportId,
                status = report.Status,
                canEdit = false
            });
        }

        var reportStageError = ValidateReportStage(report.ReportType, report.Race.Status);
        if (reportStageError != null)
        {
            return BadRequest(new
            {
                message = reportStageError,
                reportType = report.ReportType,
                raceStatus = report.Race.Status
            });
        }

        if (report.Race.Status != RaceStatuses.ResultPending)
        {
            return Conflict(new
            {
                code = "RESULTS_NOT_RECONFIRMED",
                message = "Correct and confirm all returned race results before resubmitting the final report.",
                raceStatus = report.Race.Status
            });
        }

        var activeRegistrationIds = await _context.RaceRegistrations
            .AsNoTracking()
            .Where(r =>
                r.RaceId == raceId &&
                PostRaceRegistrationStatuses.Contains(r.Status) &&
                _context.PreRaceInspections.Any(inspection =>
                    inspection.RaceId == raceId &&
                    inspection.RegistrationId == r.RegistrationId &&
                    inspection.Status == PreRaceInspectionStatuses.Passed))
            .Select(r => r.RegistrationId)
            .ToListAsync(cancellationToken);

        var confirmedResultCount = await _context.RaceResults
            .AsNoTracking()
            .CountAsync(r =>
                r.RaceId == raceId &&
                activeRegistrationIds.Contains(r.RegistrationId) &&
                r.Status == RaceResultStatuses.RefereeConfirmed,
                cancellationToken);

        if (activeRegistrationIds.Count == 0 ||
            confirmedResultCount != activeRegistrationIds.Count)
        {
            return Conflict(new
            {
                code = "RESULTS_NOT_RECONFIRMED",
                message = "Every horse that passed pre-race inspection must be referee-confirmed before the final report is resubmitted.",
                expectedResults = activeRegistrationIds.Count,
                confirmedResults = confirmedResultCount
            });
        }

        var now = _dateTimeProvider.UtcNow;
        report.ReportContent = request.ReportContent.Trim();
        report.Status = RefereeReportStatuses.Submitted;
        report.RevisionNumber += 1;
        report.SubmittedAt = now;
        report.ResubmittedAt = now;
        report.ReviewedByAdminId = null;
        report.ReviewedAt = null;
        report.UpdatedAt = now;

        await _notificationService.CreateForAdminsAsync(
            "Final referee report resubmitted",
            $"The referee revised and resubmitted the final report for race {report.Race.RaceName} (revision {report.RevisionNumber}).",
            "PostRaceReportResubmitted",
            $"/admin/results?raceId={report.RaceId}&reportId={report.ReportId}",
            "RefereeReport",
            report.ReportId,
            cancellationToken,
            preventDuplicates: false);

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "Final report resubmitted successfully. The report is locked again while waiting for admin review.",
            reportId = report.ReportId,
            reportType = report.ReportType,
            status = report.Status,
            revisionNumber = report.RevisionNumber,
            submittedAt = report.SubmittedAt,
            canEdit = false,
            isLocked = true
        });
    }

    [HttpGet("{raceId}/violations")]
    public async Task<IActionResult> GetViolations(int raceId)
    {
        if (!TryGetRefereeId(out var refereeId))
        {
            return Unauthorized(new { message = "Token không hợp lệ hoặc thiếu UserId." });
        }

        var assigned = await IsAssignedToActiveRaceAsync(raceId, refereeId);

        if (!assigned)
        {
            return Forbid();
        }

        var postRaceViolations = await _context.RaceViolations
            .AsNoTracking()
            .Where(v =>
                v.RaceId == raceId &&
                v.RefereeId == refereeId &&
                !_context.PreRaceInspections.Any(inspection =>
                    inspection.RaceId == raceId &&
                    inspection.RegistrationId == v.RegistrationId &&
                    inspection.Status == PreRaceInspectionStatuses.Failed) &&
                v.Race.Status != RaceStatuses.Cancelled &&
                v.Race.Tournament.Status != TournamentStatuses.Cancelled)
            .Select(v => new RefereeViolationDisplayResponse
            {
                ViolationId = v.ViolationId,
                RegistrationId = v.RegistrationId,
                HorseName = v.Registration.Horse.HorseName,
                ViolationType = v.ViolationType,
                Description = v.Description,
                Action = v.Action,
                PenaltyPoints = v.PenaltyPoints,
                CreatedAt = v.CreatedAt,
                SourceType = "PostRaceViolation",
                Phase = "PostRace",
                IsReadOnly = false
            })
            .ToListAsync();

        // A failed pre-race inspection is shown in the Violations tab only.
        // It is not copied to race_violations and does not create a RaceResult.
        var preRaceFailures = await _context.PreRaceInspections
            .AsNoTracking()
            .Where(inspection =>
                inspection.RaceId == raceId &&
                inspection.RefereeId == refereeId &&
                inspection.Status == PreRaceInspectionStatuses.Failed &&
                inspection.Race.Status != RaceStatuses.Cancelled &&
                inspection.Race.Tournament.Status != TournamentStatuses.Cancelled)
            .Select(inspection => new RefereeViolationDisplayResponse
            {
                // Negative IDs avoid collisions with real RaceViolation IDs.
                ViolationId = -inspection.InspectionId,
                RegistrationId = inspection.RegistrationId,
                HorseName = inspection.Registration.Horse.HorseName,
                ViolationType = "Pre-Race Inspection Failed",
                Description = inspection.Note,
                Action = "NotEligible",
                PenaltyPoints = null,
                CreatedAt = inspection.InspectedAt,
                SourceType = "PreRaceInspection",
                Phase = "PreRace",
                IsReadOnly = true
            })
            .ToListAsync();

        var violations = postRaceViolations
            .Concat(preRaceFailures)
            .OrderByDescending(item => item.CreatedAt)
            .ToList();

        return Ok(violations);
    }

    [HttpGet("{raceId}/results")]
    public async Task<IActionResult> GetResults(int raceId)
    {
        if (!TryGetRefereeId(out var refereeId))
        {
            return Unauthorized(new { message = "Token không hợp lệ hoặc thiếu UserId." });
        }

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
                _context.PreRaceInspections.Any(inspection =>
                    inspection.RaceId == raceId &&
                    inspection.RegistrationId == r.RegistrationId &&
                    inspection.Status == PreRaceInspectionStatuses.Passed) &&
                r.Race.Status != RaceStatuses.Cancelled &&
                r.Race.Tournament.Status != TournamentStatuses.Cancelled)
            .OrderBy(r => r.FinishPosition)
            .ThenBy(r => r.ResultId)
            .Select(r => new
            {
                resultId = r.ResultId,
                registrationId = r.RegistrationId,
                horseName = r.Registration.Horse.HorseName,
                outcomeStatus = r.OutcomeStatus,
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
        if (!TryGetRefereeId(out var refereeId))
        {
            return Unauthorized(new { message = "Token không hợp lệ hoặc thiếu UserId." });
        }

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
            .Select(r => new RefereeReportResponse
            {
                ReportId = r.ReportId,
                RaceId = r.RaceId,
                ReportContent = r.ReportContent,
                ReportType = r.ReportType,
                Status = r.Status,
                RevisionNumber = r.RevisionNumber,
                ReturnReasonCategory = r.ReturnReasonCategory,
                ReturnReason = r.ReturnReason,
                SubmittedAt = r.SubmittedAt,
                ResubmittedAt = r.ResubmittedAt,
                ReviewedAt = r.ReviewedAt,
                UpdatedAt = r.UpdatedAt,
                CanEdit = r.ReportType == RefereeReportTypes.PostRace
                    ? r.Status == RefereeReportStatuses.Returned
                    : true,
                IsLocked = r.ReportType == RefereeReportTypes.PostRace &&
                    r.Status != RefereeReportStatuses.Returned
            })
            .ToListAsync();

        return Ok(reports);
    }

    [HttpGet("{raceId}/reports/{reportId:int}")]
    public async Task<IActionResult> GetReportById(int raceId, int reportId)
    {
        if (!TryGetRefereeId(out var refereeId))
        {
            return Unauthorized(new { message = "Token không hợp lệ hoặc thiếu UserId." });
        }

        var assigned = await IsAssignedToActiveRaceAsync(raceId, refereeId);
        if (!assigned)
        {
            return Forbid();
        }

        var report = await _context.RefereeReports
            .AsNoTracking()
            .Where(r =>
                r.ReportId == reportId &&
                r.RaceId == raceId &&
                r.RefereeId == refereeId &&
                r.Race.Status != RaceStatuses.Cancelled &&
                r.Race.Tournament.Status != TournamentStatuses.Cancelled)
            .Select(r => new RefereeReportResponse
            {
                ReportId = r.ReportId,
                RaceId = r.RaceId,
                ReportContent = r.ReportContent,
                ReportType = r.ReportType,
                Status = r.Status,
                RevisionNumber = r.RevisionNumber,
                ReturnReasonCategory = r.ReturnReasonCategory,
                ReturnReason = r.ReturnReason,
                SubmittedAt = r.SubmittedAt,
                ResubmittedAt = r.ResubmittedAt,
                ReviewedAt = r.ReviewedAt,
                UpdatedAt = r.UpdatedAt,
                CanEdit = r.ReportType == RefereeReportTypes.PostRace
                    ? r.Status == RefereeReportStatuses.Returned
                    : true,
                IsLocked = r.ReportType == RefereeReportTypes.PostRace &&
                    r.Status != RefereeReportStatuses.Returned
            })
            .FirstOrDefaultAsync();

        return report == null
            ? NotFound(new { message = "Report not found." })
            : Ok(report);
    }

    private static string? ValidateReportStage(string reportType, string raceStatus)
    {
        if (reportType == RefereeReportTypes.PreRace)
        {
            return raceStatus is RaceStatuses.Ongoing
                or RaceStatuses.Finished
                or RaceStatuses.ResultPending
                or RaceStatuses.Published
                    ? "Pre-race report can only be submitted before the race starts."
                    : null;
        }

        if (reportType == RefereeReportTypes.PostRace)
        {
            return raceStatus is RaceStatuses.Finished
                or RaceStatuses.ResultPending
                    ? null
                    : "Post-race report can only be submitted after the race is finished and before it is published.";
        }

        return null;
    }

}

public sealed class RefereeViolationDisplayResponse
{
    public int ViolationId { get; set; }
    public int RegistrationId { get; set; }
    public string HorseName { get; set; } = string.Empty;
    public string ViolationType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Action { get; set; } = string.Empty;
    public decimal? PenaltyPoints { get; set; }
    public DateTime CreatedAt { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public string Phase { get; set; } = string.Empty;
    public bool IsReadOnly { get; set; }
}

