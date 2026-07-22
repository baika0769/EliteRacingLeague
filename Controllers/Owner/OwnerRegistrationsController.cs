using System.Data;
using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Owner.Registrations;
using Eliteracingleague.API.Models;
using Eliteracingleague.API.Services.Notifications;
using Eliteracingleague.API.Services.Racing;
using Eliteracingleague.API.Services.SystemTime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Controllers.Owner;

[Route("api/owner/registrations")]
[ApiController]
[Authorize(Roles = UserRoles.HorseOwner)]
public class OwnerRegistrationsController : OwnerBaseController
{
    private readonly INotificationService _notificationService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly RaceSchedulingValidationService _schedulingValidation;

    private static readonly string[] ActiveRegistrationStatuses =
    {
        RaceRegistrationStatuses.Pending,
        RaceRegistrationStatuses.Approved,
        RaceRegistrationStatuses.JockeyInvited,
        RaceRegistrationStatuses.ReadyToRace
    };

    private static readonly string[] ApprovedRegistrationStatuses =
    {
        RaceRegistrationStatuses.Approved,
        RaceRegistrationStatuses.JockeyInvited,
        RaceRegistrationStatuses.ReadyToRace,
        RaceRegistrationStatuses.Completed
    };

    public OwnerRegistrationsController(
        EliteRacingLeagueContext context,
        INotificationService notificationService,
        IDateTimeProvider dateTimeProvider,
        RaceSchedulingValidationService schedulingValidation) : base(context)
    {
        _notificationService = notificationService;
        _dateTimeProvider = dateTimeProvider;
        _schedulingValidation = schedulingValidation;
    }

    // API 1: Open Tournaments
    // API 2: Eligible Horses
    // API 3: Create Registration
    // API 4: Pending Registrations
    // API 5: Approved Registrations
    // API 6: Registration Detail
    // API 7: Registration Journey
    // API 8: Cancel Pending Registration

    private static string? GetHorseIneligibleReason(
        bool isActive,
        string healthStatus,
        bool alreadyRegistered)
    {
        if (!isActive)
        {
            return "Ngựa đang Inactive.";
        }

        if (alreadyRegistered)
        {
            return "Ngựa đã đăng ký race này.";
        }

        if (healthStatus != HorseHealthStatuses.Healthy)
        {
            return "Ngựa không ở trạng thái Healthy.";
        }

        return null;
    }

    private static int GetRegistrationCurrentStep(string status)
    {
        return status switch
        {
            RaceRegistrationStatuses.Pending => 2,
            RaceRegistrationStatuses.Approved => 3,
            RaceRegistrationStatuses.JockeyInvited => 4,
            RaceRegistrationStatuses.ReadyToRace => 5,
            RaceRegistrationStatuses.Completed => 5,
            RaceRegistrationStatuses.Rejected => 2,
            RaceRegistrationStatuses.Cancelled => 1,
            _ => 1
        };
    }


    [HttpGet("open-tournaments")]
    public async Task<IActionResult> GetOpenTournaments(
        [FromQuery] int limit = 6,
        CancellationToken cancellationToken = default)
    {
        var ownerId = GetCurrentUserId();
        if (ownerId == null) return InvalidToken();

        var ownerProfileError = await ValidateOwnerProfileAsync(ownerId.Value);
        if (ownerProfileError != null) return ownerProfileError;

        limit = Math.Clamp(limit, 1, 20);
        var localNow = _dateTimeProvider.GetLocalNow(_dateTimeProvider.TimeZoneId);
        var localToday = DateOnly.FromDateTime(localNow);

        var data = await _context.Races
            .AsNoTracking()
            .Where(r =>
                r.Tournament.Season.Status == SeasonStatuses.Active &&
                r.Tournament.Status == TournamentStatuses.OpenRegistration &&
                r.Tournament.StartDate >= localToday &&
                RaceStatuses.RegisterableStatuses.Contains(r.Status) &&
                r.RaceDate >= localNow)
            .OrderBy(r => r.RaceDate)
            .Select(r => new
            {
                r.TournamentId,
                r.Tournament.TournamentName,
                SeasonId = r.Tournament.SeasonId,
                SeasonName = r.Tournament.Season.SeasonName,
                SeasonStatus = r.Tournament.Season.Status,
                RegistrationDeadline = r.Tournament.StartDate,
                r.Tournament.Location,
                r.Tournament.PrizePool,
                r.Tournament.ImageUrl,
                r.RaceId,
                r.RaceDate,
                r.DistanceMeters,
                r.MaxHorses,
                RegisteredCount = r.RaceRegistrations.Count(registration =>
                    registration.Status != RaceRegistrationStatuses.Rejected &&
                    registration.Status != RaceRegistrationStatuses.Cancelled &&
                    registration.Status != RaceRegistrationStatuses.Withdrawn),
                OwnerAlreadyRegistered = r.RaceRegistrations.Any(registration =>
                    registration.OwnerId == ownerId.Value &&
                    registration.Status != RaceRegistrationStatuses.Rejected &&
                    registration.Status != RaceRegistrationStatuses.Cancelled &&
                    registration.Status != RaceRegistrationStatuses.Withdrawn)
            })
            .Where(r => r.RegisteredCount < r.MaxHorses && !r.OwnerAlreadyRegistered)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return Ok(data.Select(r => new OwnerOpenTournamentResponse
        {
            TournamentId = r.TournamentId,
            TournamentName = r.TournamentName,
            SeasonId = r.SeasonId,
            SeasonName = r.SeasonName,
            SeasonStatus = r.SeasonStatus,
            RegistrationDeadline = r.RegistrationDeadline.ToString("yyyy-MM-dd"),
            RaceId = r.RaceId,
            RaceDate = r.RaceDate.ToString("yyyy-MM-dd"),
            Location = r.Location,
            DistanceMeters = r.DistanceMeters,
            PrizePool = r.PrizePool,
            MaxHorses = r.MaxHorses,
            RegisteredCount = r.RegisteredCount,
            AvailableSlots = Math.Max(0, r.MaxHorses - r.RegisteredCount),
            OwnerAlreadyRegistered = r.OwnerAlreadyRegistered,
            ImageUrl = r.ImageUrl
        }));
    }

    [HttpGet("eligible-horses")]
    public async Task<IActionResult> GetEligibleHorses([FromQuery] int raceId)
    {
        var ownerId = GetCurrentUserId();

        if (ownerId == null)
        {
            return InvalidToken();
        }



        var ownerProfileError = await ValidateOwnerProfileAsync(ownerId.Value);

        if (ownerProfileError != null)
        {
            return ownerProfileError;
        }

        var race = await _context.Races
            .AsNoTracking()
            .Where(r => r.RaceId == raceId)
            .Select(r => new
            {
                r.RaceId,
                r.Status,
                r.RaceDate,
                TournamentStatus = r.Tournament.Status,
                RegistrationDeadline = r.Tournament.StartDate,
                SeasonStatus = r.Tournament.Season.Status
            })
            .FirstOrDefaultAsync();

        if (race == null)
        {
            return NotFound(new { message = "Không tìm thấy race." });
        }

        if (race.SeasonStatus != SeasonStatuses.Active)
        {
            return BadRequest(new
            {
                code = "SEASON_NOT_ACTIVE",
                message = "Season của tournament hiện không hoạt động.",
                seasonStatus = race.SeasonStatus
            });
        }

        if (race.TournamentStatus != TournamentStatuses.OpenRegistration)
        {
            return BadRequest(new
            {
                message = "Tournament hiện không mở đăng ký."
            });
        }

        var localNow = _dateTimeProvider.GetLocalNow(_dateTimeProvider.TimeZoneId);
        var localToday = DateOnly.FromDateTime(localNow);

        if (race.RegistrationDeadline < localToday)
        {
            return BadRequest(new
            {
                message = "Đã hết hạn đăng ký tournament."
            });
        }

        if (!RaceStatuses.CanRegister(race.Status))
        {
            return BadRequest(new
            {
                message = "Race hiện không mở đăng ký."
            });
        }

        if (race.RaceDate <= localNow)
        {
            return BadRequest(new
            {
                message = "Race đã bắt đầu hoặc đã diễn ra, không thể chọn ngựa."
            });
        }

        var horses = await _context.Horses
            .AsNoTracking()
            .Where(h => h.OwnerId == ownerId.Value)
            .Select(h => new
            {
                h.HorseId,
                h.HorseName,
                BreedName = h.Breed.BreedName,
                h.Age,
                h.HeightCm,
                h.WeightKg,
                h.HealthStatus,
                h.IsActive,
                AlreadyRegistered = h.RaceRegistrations.Any(r =>
                    r.RaceId == raceId &&
                    r.Status != RaceRegistrationStatuses.Rejected &&
                    r.Status != RaceRegistrationStatuses.Cancelled)
            })
            .ToListAsync();

        var response = horses.Select(h =>
        {
            var reason = GetHorseIneligibleReason(
    h.IsActive,
    h.HealthStatus,
    h.AlreadyRegistered);


            return new OwnerEligibleHorseResponse
            {
                HorseId = h.HorseId,
                HorseName = h.HorseName,
                BreedName = h.BreedName,
                Age = h.Age,
                HeightCm = h.HeightCm,
                WeightKg = h.WeightKg,
                HealthStatus = h.HealthStatus,
                IsEligible = reason == null,
                IneligibleReason = reason
            };
        });

        return Ok(response);
    }


    [HttpPost]
    public async Task<IActionResult> CreateRegistration(CreateRaceRegistrationRequest request)
    {
        var ownerId = GetCurrentUserId();

        if (ownerId == null)
        {
            return InvalidToken();
        }

        var ownerProfileError = await ValidateOwnerProfileAsync(ownerId.Value);

        if (ownerProfileError != null)
        {
            return ownerProfileError;
        }

        var utcNow = _dateTimeProvider.UtcNow;
        var localNow = _dateTimeProvider.GetLocalNow(_dateTimeProvider.TimeZoneId);
        var localToday = DateOnly.FromDateTime(localNow);

        await using var transaction = await _context.Database
            .BeginTransactionAsync(IsolationLevel.Serializable);

        var race = await _context.Races
            .Include(r => r.Tournament)
                .ThenInclude(t => t.Season)
            .Include(r => r.RaceRegistrations)
            .FirstOrDefaultAsync(r => r.RaceId == request.RaceId);

        if (race == null)
        {
            return NotFound(new { message = "Không tìm thấy race." });
        }

        if (race.Tournament.Season.Status != SeasonStatuses.Active)
        {
            return BadRequest(new
            {
                code = "SEASON_NOT_ACTIVE",
                message = "Season của tournament hiện không hoạt động.",
                seasonId = race.Tournament.SeasonId,
                seasonStatus = race.Tournament.Season.Status
            });
        }

        if (race.Tournament.Status != TournamentStatuses.OpenRegistration)
        {
            return BadRequest(new { message = "Tournament hiện không mở đăng ký." });
        }

        if (race.Tournament.StartDate < localToday)
        {
            return BadRequest(new { message = "Đã hết hạn đăng ký tournament." });
        }

        if (!RaceStatuses.CanRegister(race.Status))
        {
            return BadRequest(new { message = "Race hiện không mở đăng ký." });
        }

        if (race.RaceDate <= localNow)
        {
            return BadRequest(new { message = "Race đã bắt đầu hoặc đã diễn ra, không thể đăng ký." });
        }

        var currentRegisteredCount = race.RaceRegistrations.Count(r =>
            r.Status != RaceRegistrationStatuses.Rejected &&
            r.Status != RaceRegistrationStatuses.Cancelled &&
            r.Status != RaceRegistrationStatuses.Withdrawn);

        if (currentRegisteredCount >= race.MaxHorses)
        {
            return BadRequest(new { message = "Race đã đủ số lượng ngựa đăng ký." });
        }

        var horse = await _context.Horses
            .Include(h => h.RaceRegistrations)
            .FirstOrDefaultAsync(h =>
                h.HorseId == request.HorseId &&
                h.OwnerId == ownerId.Value);

        if (horse == null)
        {
            return NotFound(new { message = "Không tìm thấy ngựa hoặc bạn không sở hữu ngựa này." });
        }

        var alreadyRegisteredByOwner = await _context.RaceRegistrations
            .AnyAsync(r =>
                r.RaceId == request.RaceId &&
                r.OwnerId == ownerId.Value &&
                r.Status != RaceRegistrationStatuses.Rejected &&
                r.Status != RaceRegistrationStatuses.Cancelled &&
                r.Status != RaceRegistrationStatuses.Withdrawn);

        if (alreadyRegisteredByOwner)
        {
            return BadRequest(new { message = "Bạn đã có đăng ký trong race này." });
        }

        var ineligibleReason = GetHorseIneligibleReason(
    horse.IsActive,
    horse.HealthStatus,
    horse.RaceRegistrations.Any(r =>
        r.RaceId == request.RaceId &&
        r.Status != RaceRegistrationStatuses.Rejected &&
        r.Status != RaceRegistrationStatuses.Cancelled));

        if (ineligibleReason != null)
        {
            return BadRequest(new { message = ineligibleReason });
        }

        await _schedulingValidation.EnsureHorseAndJockeyNoConflictAsync(
            request.RaceId, request.HorseId, null);

        var ownerName = await _context.Users
            .AsNoTracking()
            .Where(u => u.UserId == ownerId.Value)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync();

        var registration = new RaceRegistration
        {
            RaceId = request.RaceId,
            HorseId = request.HorseId,
            OwnerId = ownerId.Value,
            JockeyId = null,
            Status = RaceRegistrationStatuses.Pending,
            SubmittedAt = utcNow
        };

        _context.RaceRegistrations.Add(registration);

        await _context.SaveChangesAsync();

        var displayOwnerName = string.IsNullOrWhiteSpace(ownerName) ? "An owner" : ownerName;

        await _notificationService.CreateForAdminsAsync(
            "New Race Registration",
            $"Owner {displayOwnerName} registered horse {horse.HorseName} for tournament {race.Tournament.TournamentName}.",
            "RaceRegistration",
            "/admin/registrations",
            "RaceRegistration",
            registration.RegistrationId);

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return Ok(new
        {
            message = "Đăng ký race thành công. Đơn đang chờ Admin duyệt.",
            registrationId = registration.RegistrationId,
            status = registration.Status,
            nextStep = "PendingReview"
        });
    }

    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingRegistrations()
    {
        var ownerId = GetCurrentUserId();

        if (ownerId == null)
        {
            return InvalidToken();
        }

        var ownerProfileError = await ValidateOwnerProfileAsync(ownerId.Value);

        if (ownerProfileError != null)
        {
            return ownerProfileError;
        }

        var data = await _context.RaceRegistrations
            .AsNoTracking()
            .Where(r =>
    r.OwnerId == ownerId.Value &&
    r.Status == RaceRegistrationStatuses.Pending &&
    r.Race.Status != RaceStatuses.Cancelled &&
    r.Race.Tournament.Status != TournamentStatuses.Cancelled)
            .OrderByDescending(r => r.SubmittedAt)
            .Select(r => new
            {
                r.RegistrationId,
                r.RaceId,
                TournamentName = r.Race.Tournament.TournamentName,

                SeasonId = r.Race.Tournament.SeasonId,
                SeasonName = r.Race.Tournament.Season.SeasonName,
                SeasonStatus = r.Race.Tournament.Season.Status,
                RegistrationDeadline = r.Race.Tournament.StartDate,

                HorseName = r.Horse.HorseName,
                r.SubmittedAt,
                r.Status,
                r.AdminNote
            })
            .ToListAsync();

        var response = data.Select(r => new OwnerPendingRegistrationResponse
        {
            RegistrationId = r.RegistrationId,
            RaceId = r.RaceId,
            TournamentName = r.TournamentName,

            SeasonId = r.SeasonId,
            SeasonName = r.SeasonName,
            SeasonStatus = r.SeasonStatus,
            RegistrationDeadline = r.RegistrationDeadline.ToString("yyyy-MM-dd"),

            HorseName = r.HorseName,
            RegDate = r.SubmittedAt.ToString("yyyy-MM-dd"),
            Status = r.Status,
            AdminNote = r.AdminNote
        });

        return Ok(response);
    }



    [HttpGet("approved")]
    public async Task<IActionResult> GetApprovedRegistrations()
    {
        var ownerId = GetCurrentUserId();

        if (ownerId == null)
        {
            return InvalidToken();
        }

        var ownerProfileError = await ValidateOwnerProfileAsync(ownerId.Value);

        if (ownerProfileError != null)
        {
            return ownerProfileError;
        }

        var data = await _context.RaceRegistrations
            .AsNoTracking()
            .Where(r =>
    r.OwnerId == ownerId.Value &&
    ApprovedRegistrationStatuses.Contains(r.Status) &&
    r.Race.Status != RaceStatuses.Cancelled &&
    r.Race.Tournament.Status != TournamentStatuses.Cancelled)
            .OrderByDescending(r => r.Race.RaceDate)
            .Select(r => new
            {
                r.RegistrationId,
                r.RaceId,
                TournamentName = r.Race.Tournament.TournamentName,

                SeasonId = r.Race.Tournament.SeasonId,
                SeasonName = r.Race.Tournament.Season.SeasonName,
                SeasonStatus = r.Race.Tournament.Season.Status,
                RegistrationDeadline = r.Race.Tournament.StartDate,

                HorseName = r.Horse.HorseName,
                JockeyName = r.Jockey == null ? null : r.Jockey.JockeyNavigation.FullName,
                RaceDate = r.Race.RaceDate,
                r.Status
            })
            .ToListAsync();

        var response = data.Select(r => new OwnerApprovedRegistrationResponse
        {
            RegistrationId = r.RegistrationId,
            RaceId = r.RaceId,
            TournamentName = r.TournamentName,

            SeasonId = r.SeasonId,
            SeasonName = r.SeasonName,
            SeasonStatus = r.SeasonStatus,
            RegistrationDeadline = r.RegistrationDeadline.ToString("yyyy-MM-dd"),

            HorseName = r.HorseName,
            JockeyName = r.JockeyName,
            RaceDate = r.RaceDate.ToString("yyyy-MM-dd"),
            Status = r.Status
        });

        return Ok(response);
    }



    [HttpPut("{registrationId:int}/cancel")]
    public async Task<IActionResult> CancelPendingRegistration(int registrationId)
    {
        var ownerId = GetCurrentUserId();

        if (ownerId == null)
        {
            return InvalidToken();
        }

        var ownerProfileError = await ValidateOwnerProfileAsync(ownerId.Value);

        if (ownerProfileError != null)
        {
            return ownerProfileError;
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable);

        try
        {
            var registration = await _context.RaceRegistrations
                .Include(r => r.Horse)
                .Include(r => r.Race)
                    .ThenInclude(r => r.Tournament)
                .FirstOrDefaultAsync(r =>
                    r.RegistrationId == registrationId &&
                    r.OwnerId == ownerId.Value);

            if (registration == null)
            {
                return NotFound(new
                {
                    message = "Không tìm thấy đơn đăng ký hoặc bạn không có quyền hủy đơn này.",
                    registrationId
                });
            }

            if (registration.Status == RaceRegistrationStatuses.Cancelled)
            {
                return Ok(new
                {
                    message = "Đơn đăng ký đã được hủy trước đó.",
                    registrationId = registration.RegistrationId,
                    status = registration.Status
                });
            }

            if (registration.Status != RaceRegistrationStatuses.Pending)
            {
                return BadRequest(new
                {
                    message = "Chỉ có thể hủy đơn khi đơn vẫn đang chờ Admin duyệt.",
                    registrationId = registration.RegistrationId,
                    currentStatus = registration.Status,
                    allowedStatus = RaceRegistrationStatuses.Pending
                });
            }

            var localNow = _dateTimeProvider.GetLocalNow(_dateTimeProvider.TimeZoneId);
            var localToday = DateOnly.FromDateTime(localNow);

            if (registration.Race.Tournament.Status != TournamentStatuses.OpenRegistration)
            {
                return BadRequest(new
                {
                    message = "Tournament đã đóng đăng ký nên không thể tự hủy đơn.",
                    registrationId = registration.RegistrationId,
                    tournamentStatus = registration.Race.Tournament.Status
                });
            }

            if (registration.Race.Tournament.StartDate < localToday)
            {
                return BadRequest(new
                {
                    message = "Đã qua hạn đăng ký nên không thể tự hủy đơn.",
                    registrationId = registration.RegistrationId,
                    registrationDeadline = registration.Race.Tournament.StartDate
                });
            }

            if (registration.Race.RaceDate <= localNow)
            {
                return BadRequest(new
                {
                    message = "Race đã bắt đầu hoặc đã diễn ra nên không thể hủy đơn.",
                    registrationId = registration.RegistrationId,
                    raceDate = registration.Race.RaceDate
                });
            }

            registration.Status = RaceRegistrationStatuses.Cancelled;

            await _context.SaveChangesAsync();

            await _notificationService.CreateForAdminsAsync(
                "Race Registration Cancelled",
                $"Owner cancelled registration #{registration.RegistrationId} for horse {registration.Horse.HorseName} in tournament {registration.Race.Tournament.TournamentName}.",
                "RaceRegistration",
                "/admin/registrations",
                "RaceRegistration",
                registration.RegistrationId);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new
            {
                message = "Hủy đơn đăng ký thành công.",
                registrationId = registration.RegistrationId,
                raceId = registration.RaceId,
                horseId = registration.HorseId,
                status = registration.Status
            });
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }


    [HttpGet("{registrationId:int}")]
    public async Task<IActionResult> GetRegistrationDetail(int registrationId)
    {
        var ownerId = GetCurrentUserId();

        if (ownerId == null)
        {
            return InvalidToken();
        }

        var ownerProfileError = await ValidateOwnerProfileAsync(ownerId.Value);

        if (ownerProfileError != null)
        {
            return ownerProfileError;
        }

        var data = await _context.RaceRegistrations
            .AsNoTracking()
            .Where(r =>
    r.RegistrationId == registrationId &&
    r.OwnerId == ownerId.Value &&
    r.Race.Status != RaceStatuses.Cancelled &&
    r.Race.Tournament.Status != TournamentStatuses.Cancelled)
            .Select(r => new
            {
                r.RegistrationId,
                r.RaceId,
                r.HorseId,
                TournamentName = r.Race.Tournament.TournamentName,

                SeasonId = r.Race.Tournament.SeasonId,
                SeasonName = r.Race.Tournament.Season.SeasonName,
                SeasonStatus = r.Race.Tournament.Season.Status,
                RegistrationDeadline = r.Race.Tournament.StartDate,

                HorseName = r.Horse.HorseName,
                JockeyName = r.Jockey == null ? null : r.Jockey.JockeyNavigation.FullName,
                RaceDate = r.Race.RaceDate,
                r.SubmittedAt,
                r.Status,
                r.AdminNote
            })
            .FirstOrDefaultAsync();

        if (data == null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy đơn đăng ký hoặc bạn không có quyền xem đơn này."
            });
        }

        var response = new OwnerRegistrationDetailResponse
        {
            RegistrationId = data.RegistrationId,
            RaceId = data.RaceId,
            HorseId = data.HorseId,
            TournamentName = data.TournamentName,

            SeasonId = data.SeasonId,
            SeasonName = data.SeasonName,
            SeasonStatus = data.SeasonStatus,
            RegistrationDeadline = data.RegistrationDeadline.ToString("yyyy-MM-dd"),

            HorseName = data.HorseName,
            JockeyName = data.JockeyName,
            RaceDate = data.RaceDate.ToString("yyyy-MM-dd"),
            SubmittedAt = data.SubmittedAt.ToString("yyyy-MM-dd HH:mm"),
            Status = data.Status,
            AdminNote = data.AdminNote
        };

        return Ok(response);
    }


    [HttpGet("{registrationId:int}/journey")]
    public async Task<IActionResult> GetRegistrationJourney(int registrationId)
    {
        var ownerId = GetCurrentUserId();

        if (ownerId == null)
        {
            return InvalidToken();
        }

        var ownerProfileError = await ValidateOwnerProfileAsync(ownerId.Value);

        if (ownerProfileError != null)
        {
            return ownerProfileError;
        }

        var registration = await _context.RaceRegistrations
            .AsNoTracking()
            .Where(r =>
    r.RegistrationId == registrationId &&
    r.OwnerId == ownerId.Value &&
    r.Race.Status != RaceStatuses.Cancelled &&
    r.Race.Tournament.Status != TournamentStatuses.Cancelled)
            .Select(r => new
            {
                r.RegistrationId,
                r.Status
            })
            .FirstOrDefaultAsync();

        if (registration == null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy đơn đăng ký hoặc bạn không có quyền xem đơn này."
            });
        }

        var currentStep = GetRegistrationCurrentStep(registration.Status);

        var steps = new List<RegistrationJourneyStepResponse>
        {
            new RegistrationJourneyStepResponse
            {
                StepNumber = 1,
                Key = "Submitted",
                Label = "Submitted",
                Description = "Owner submitted the race registration.",
                IsCompleted = currentStep >= 1,
                IsCurrent = currentStep == 1
            },
            new RegistrationJourneyStepResponse
            {
                StepNumber = 2,
                Key = "PendingReview",
                Label = "Pending Review",
                Description = "Admin is reviewing the registration.",
                IsCompleted = currentStep >= 2 &&
                    registration.Status != RaceRegistrationStatuses.Rejected &&
                    registration.Status != RaceRegistrationStatuses.Cancelled,
                IsCurrent = currentStep == 2
            },
            new RegistrationJourneyStepResponse
            {
                StepNumber = 3,
                Key = RaceRegistrationStatuses.Approved,
                Label = "Approved",
                Description = "The registration has been approved by Admin.",
                IsCompleted = currentStep >= 3 &&
                    registration.Status != RaceRegistrationStatuses.Rejected &&
                    registration.Status != RaceRegistrationStatuses.Cancelled,
                IsCurrent = currentStep == 3
            },
            new RegistrationJourneyStepResponse
            {
                StepNumber = 4,
                Key = RaceRegistrationStatuses.JockeyInvited,
                Label = "Jockey Invited",
                Description = "Owner invited a jockey to join the race.",
                IsCompleted = currentStep >= 4 &&
                    registration.Status != RaceRegistrationStatuses.Rejected &&
                    registration.Status != RaceRegistrationStatuses.Cancelled,
                IsCurrent = currentStep == 4
            },
            new RegistrationJourneyStepResponse
            {
                StepNumber = 5,
                Key = RaceRegistrationStatuses.ReadyToRace,
                Label = "Ready to Race",
                Description = "Horse and jockey are ready for the race.",
                IsCompleted = currentStep >= 5 &&
                    registration.Status != RaceRegistrationStatuses.Rejected &&
                    registration.Status != RaceRegistrationStatuses.Cancelled,
                IsCurrent = currentStep == 5
            }
        };

        var response = new OwnerRegistrationJourneyResponse
        {
            RegistrationId = registration.RegistrationId,
            CurrentStatus = registration.Status,
            CurrentStep = currentStep,
            Steps = steps
        };

        return Ok(response);
    }


}