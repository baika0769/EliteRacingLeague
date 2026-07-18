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
        bool includeInspectionReportFields)
    {
        var registrations = await _context.RaceRegistrations
            .AsNoTracking()
            .Where(registration =>
                registration.RaceId == raceId &&
                ActiveRegistrationStatuses.Contains(registration.Status) &&
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

        var registrations = await GetRefereeRegistrationResponsesAsync(
            raceId,
            usePendingInspectionFallback: false,
            includeInspectionReportFields: false);

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
            .FirstOrDefaultAsync(r =>
                r.RaceId == raceId &&
                r.Status != RaceStatuses.Cancelled &&
                r.Tournament.Status != TournamentStatuses.Cancelled);

        if (race == null)
        {
            return NotFound(new { message = "Race not found or has been cancelled." });
        }

        if (race.Status != RaceStatuses.AssignedReferee)
        {
            return BadRequest(new { message = "Pre-race inspection can only be updated when race status is AssignedReferee." });
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

        var outcomeStatus = RaceResultValidationService.NormalizeOutcome(request.OutcomeStatus);

        if (outcomeStatus == RaceOutcomeStatuses.Finished)
        {
            if (!request.FinishPosition.HasValue || request.FinishPosition.Value <= 0)
                return BadRequest("Finished outcome requires a finish position greater than 0.");
            if (!request.FinishTimeSeconds.HasValue || request.FinishTimeSeconds.Value <= 0)
                return BadRequest("Finished outcome requires a finish time greater than 0.");
        }
        else
        {
            if (request.FinishPosition.HasValue)
                return BadRequest($"Outcome {outcomeStatus} must not have a finish position.");
            if (request.FinishTimeSeconds.HasValue && request.FinishTimeSeconds.Value <= 0)
                return BadRequest("Finish time must be greater than 0 when provided.");
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

        if (failedInspection && outcomeStatus is RaceOutcomeStatuses.Finished or RaceOutcomeStatuses.DidNotFinish)
        {
            return BadRequest("A registration that failed pre-race inspection must be recorded as DNS, DSQ, or Withdrawn.");
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

        if (outcomeStatus == RaceOutcomeStatuses.Finished && request.FinishPosition.HasValue)
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
            result.FinishTimeSeconds = request.FinishTimeSeconds;
            result.FinishPosition = request.FinishPosition;
            result.Score = request.Score;
            result.OutcomeStatus = outcomeStatus;
            result.Note = request.Note;
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
    public async Task<IActionResult> ConfirmAllResults(int raceId)
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

        var disqualifiedIds = (await _context.RaceViolations.AsNoTracking()
            .Where(v => v.RaceId == raceId &&
                        v.Action == RaceViolationActions.Disqualified &&
                        eligibleRegistrationIds.Contains(v.RegistrationId))
            .Select(v => v.RegistrationId)
            .Distinct()
            .ToListAsync()).ToHashSet();

        foreach (var result in results.Where(r => disqualifiedIds.Contains(r.RegistrationId)))
        {
            result.OutcomeStatus = RaceOutcomeStatuses.Disqualified;
            result.FinishPosition = null;
        }

        var validationErrors = _resultValidationService.ValidateForPublication(
            results,
            eligibleRegistrationIds.ToHashSet(),
            disqualifiedIds);
        if (validationErrors.Count > 0)
            return BadRequest(new { message = "Race results are incomplete or invalid.", errors = validationErrors });

        if (results.Any(r => r.Status is RaceResultStatuses.AdminApproved or RaceResultStatuses.Published))
        {
            return BadRequest(new { message = "Admin-approved or published results cannot be confirmed by referee." });
        }

        foreach (var result in results.Where(r => r.Status == RaceResultStatuses.Draft))
        {
            result.Status = RaceResultStatuses.RefereeConfirmed;
            result.UpdatedAt = _dateTimeProvider.UtcNow;
        }

        race.Status = RaceStatuses.ResultPending;
        race.UpdatedAt = _dateTimeProvider.UtcNow;

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
                ActiveRegistrationStatuses.Contains(r.Status) &&
                r.Race.Status != RaceStatuses.Cancelled &&
                r.Race.Tournament.Status != TournamentStatuses.Cancelled);

        if (!registrationExists)
        {
            return NotFound("Registration not found or has been cancelled.");
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
    CreateRefereeReportRequest request)
    {
        if (!TryGetRefereeId(out var refereeId))
        {
            return Unauthorized(new { message = "Token không hợp lệ hoặc thiếu UserId." });
        }

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
            .FirstOrDefaultAsync(r =>
                r.RaceId == raceId &&
                r.Status != RaceStatuses.Cancelled &&
                r.Tournament.Status != TournamentStatuses.Cancelled);

        if (race == null)
        {
            return NotFound("Race not found or has been cancelled.");
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

        var duplicateReportExists = await _context.RefereeReports
            .AsNoTracking()
            .AnyAsync(r =>
                r.RaceId == raceId &&
                r.RefereeId == refereeId &&
                r.ReportType == request.ReportType);

        if (duplicateReportExists)
        {
            return Conflict(new
            {
                message = "This referee already submitted this report type for the race. Update the existing report instead of creating a duplicate.",
                reportType = request.ReportType
            });
        }

        var report = new RefereeReport
        {
            RaceId = raceId,
            RefereeId = refereeId,
            ReportContent = request.ReportContent.Trim(),
            ReportType = request.ReportType,
            SubmittedAt = _dateTimeProvider.UtcNow
        };

        _context.RefereeReports.Add(report);

        if (request.ReportType == RefereeReportTypes.PostRace)
        {
            await _notificationService.CreateForAdminsAsync(
                "Post-race Report Submitted",
                $"Referee submitted a post-race report for {race.RaceName}.",
                "PostRaceReport",
                "/admin/referee-reports",
                "RefereeReport",
                report.ReportId);
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Referee report submitted successfully",
            reportId = report.ReportId,
            reportType = report.ReportType
        });
    }

    [HttpPut("{raceId}/reports/{reportId:int}")]
    public async Task<IActionResult> UpdateReport(
        int raceId,
        int reportId,
        CreateRefereeReportRequest request)
    {
        if (!TryGetRefereeId(out var refereeId))
        {
            return Unauthorized(new { message = "Token không hợp lệ hoặc thiếu UserId." });
        }

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

        var report = await _context.RefereeReports
            .FirstOrDefaultAsync(r =>
                r.ReportId == reportId &&
                r.RaceId == raceId &&
                r.RefereeId == refereeId);

        if (report == null)
        {
            return NotFound(new { message = "Report not found or you do not have permission to update it." });
        }

        if (report.ReportType != request.ReportType)
        {
            return BadRequest(new
            {
                message = "Report type cannot be changed. Create/update the correct report type instead.",
                currentReportType = report.ReportType,
                requestedReportType = request.ReportType
            });
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
        report.SubmittedAt = _dateTimeProvider.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Referee report updated successfully",
            reportId = report.ReportId,
            reportType = report.ReportType
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
