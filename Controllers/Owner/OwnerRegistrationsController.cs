using System.Data;
using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Owner.Registrations;
using Eliteracingleague.API.Models;
using Eliteracingleague.API.Services.Notifications;
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
        IDateTimeProvider dateTimeProvider) : base(context)
    {
        _notificationService = notificationService;
        _dateTimeProvider = dateTimeProvider;
    }

    // API 1: Open Tournaments
    // API 2: Eligible Horses
    // API 3: Create Registration
    // API 4: Pending Registrations
    // API 5: Approved Registrations
    // API 6: Registration Detail
    // API 7: Registration Journey

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
    public async Task<IActionResult> GetOpenTournaments([FromQuery] int limit = 6)
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

        if (limit <= 0)
        {
            limit = 6;
        }

        if (limit > 20)
        {
            limit = 20;
        }

        var localNow = _dateTimeProvider.GetLocalNow(_dateTimeProvider.TimeZoneId);
        var localToday = DateOnly.FromDateTime(localNow);

        var data = await _context.Tournaments
            .AsNoTracking()
            .Where(t =>
                t.Season.Status == SeasonStatuses.Active &&
                t.Status == TournamentStatuses.OpenRegistration &&
                t.StartDate >= localToday &&
                t.Race != null &&
                RaceStatuses.RegisterableStatuses.Contains(t.Race.Status) &&
                t.Race.RaceDate >= localNow)
            .OrderBy(t => t.Race!.RaceDate)
            .Select(t => new
            {
                t.TournamentId,
                t.TournamentName,
                t.Location,
                t.PrizePool,
                t.ImageUrl,

                RaceId = t.Race!.RaceId,
                RaceDate = t.Race.RaceDate,
                DistanceMeters = t.Race.DistanceMeters,
                MaxHorses = t.Race.MaxHorses,

                RegisteredCount = t.Race.RaceRegistrations.Count(r =>
                    r.Status != RaceRegistrationStatuses.Rejected &&
                    r.Status != RaceRegistrationStatuses.Cancelled),

                OwnerAlreadyRegistered = t.Race.RaceRegistrations.Any(r =>
                    r.OwnerId == ownerId.Value &&
                    r.Status != RaceRegistrationStatuses.Rejected &&
                    r.Status != RaceRegistrationStatuses.Cancelled)
            })
            .ToListAsync();

        var response = data
            .Where(t => t.RegisteredCount < t.MaxHorses && !t.OwnerAlreadyRegistered)
            .Take(limit)
            .Select(t => new OwnerOpenTournamentResponse
            {
                TournamentId = t.TournamentId,
                TournamentName = t.TournamentName,
                RaceId = t.RaceId,
                RaceDate = t.RaceDate.ToString("yyyy-MM-dd"),
                Location = t.Location,
                DistanceMeters = t.DistanceMeters,
                PrizePool = t.PrizePool,
                MaxHorses = t.MaxHorses,
                RegisteredCount = t.RegisteredCount,
                AvailableSlots = Math.Max(0, t.MaxHorses - t.RegisteredCount),
                OwnerAlreadyRegistered = t.OwnerAlreadyRegistered,
                ImageUrl = t.ImageUrl
            })
            .ToList();

        return Ok(response);
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
            r.Status != RaceRegistrationStatuses.Cancelled);

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
                r.Status != RaceRegistrationStatuses.Cancelled);

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
            HorseName = r.HorseName,
            JockeyName = r.JockeyName,
            RaceDate = r.RaceDate.ToString("yyyy-MM-dd"),
            Status = r.Status
        });

        return Ok(response);
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
                Label = "Đã gửi đơn",
                Description = "Owner đã gửi đơn đăng ký race.",
                IsCompleted = currentStep >= 1,
                IsCurrent = currentStep == 1
            },
            new RegistrationJourneyStepResponse
            {
                StepNumber = 2,
                Key = "PendingReview",
                Label = "Chờ Admin duyệt",
                Description = "Admin đang kiểm tra đơn đăng ký.",
                IsCompleted = currentStep >= 2 &&
                    registration.Status != RaceRegistrationStatuses.Rejected &&
                    registration.Status != RaceRegistrationStatuses.Cancelled,
                IsCurrent = currentStep == 2
            },
            new RegistrationJourneyStepResponse
            {
                StepNumber = 3,
                Key = RaceRegistrationStatuses.Approved,
                Label = "Đã được duyệt",
                Description = "Đơn đăng ký đã được Admin duyệt.",
                IsCompleted = currentStep >= 3 &&
                    registration.Status != RaceRegistrationStatuses.Rejected &&
                    registration.Status != RaceRegistrationStatuses.Cancelled,
                IsCurrent = currentStep == 3
            },
            new RegistrationJourneyStepResponse
            {
                StepNumber = 4,
                Key = RaceRegistrationStatuses.JockeyInvited,
                Label = "Mời Jockey",
                Description = "Owner đã mời Jockey tham gia race.",
                IsCompleted = currentStep >= 4 &&
                    registration.Status != RaceRegistrationStatuses.Rejected &&
                    registration.Status != RaceRegistrationStatuses.Cancelled,
                IsCurrent = currentStep == 4
            },
            new RegistrationJourneyStepResponse
            {
                StepNumber = 5,
                Key = RaceRegistrationStatuses.ReadyToRace,
                Label = "Sẵn sàng thi đấu",
                Description = "Ngựa và Jockey đã sẵn sàng tham gia race.",
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
