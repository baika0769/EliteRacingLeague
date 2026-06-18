using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Owner.Registrations;
using Eliteracingleague.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Controllers.Owner;

[Route("api/owner/registrations")]
[ApiController]
[Authorize(Roles = UserRoles.HorseOwner)]
public class OwnerRegistrationsController : OwnerBaseController
{
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
        RaceRegistrationStatuses.ReadyToRace
    };

    public OwnerRegistrationsController(EliteRacingLeagueContext context) : base(context)
    {
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
        bool alreadyRegistered,
        int age,
        decimal weightKg,
        int? minAge,
        int? maxAge,
        decimal? minWeight,
        decimal? maxWeight)
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

        if (minAge.HasValue && age < minAge.Value)
        {
            return $"Ngựa chưa đủ tuổi tối thiểu {minAge.Value}.";
        }

        if (maxAge.HasValue && age > maxAge.Value)
        {
            return $"Ngựa vượt quá tuổi tối đa {maxAge.Value}.";
        }

        if (minWeight.HasValue && weightKg < minWeight.Value)
        {
            return $"Ngựa chưa đạt cân nặng tối thiểu {minWeight.Value}kg.";
        }

        if (maxWeight.HasValue && weightKg > maxWeight.Value)
        {
            return $"Ngựa vượt quá cân nặng tối đa {maxWeight.Value}kg.";
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
    public async Task<IActionResult> GetOpenTournaments([FromQuery] int limit = 3)
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

        if (limit <= 0) limit = 3;
        if (limit > 20) limit = 20;

        var today = DateTime.UtcNow.Date;

        var data = await _context.Tournaments
            .AsNoTracking()
            .Where(t =>
                t.Status == TournamentStatuses.OpenRegistration &&
                t.Race != null &&
                t.Race.Status == RaceStatuses.Scheduled &&
                t.Race.RaceDate >= today)
            .OrderBy(t => t.Race!.RaceDate)
            .Select(t => new
            {
                t.TournamentId,
                t.TournamentName,
                t.Location,
                t.PrizePool,
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
                ImageUrl = null
            });

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
                TournamentMinAge = r.Tournament.MinHorseAge,
                TournamentMaxAge = r.Tournament.MaxHorseAge,
                TournamentMinWeight = r.Tournament.MinHorseWeightKg,
                TournamentMaxWeight = r.Tournament.MaxHorseWeightKg
            })
            .FirstOrDefaultAsync();

        if (race == null)
        {
            return NotFound(new { message = "Không tìm thấy race." });
        }

        if (race.TournamentStatus != TournamentStatuses.OpenRegistration)
        {
            return BadRequest(new
            {
                message = "Tournament hiện không mở đăng ký."
            });
        }

        if (race.Status != RaceStatuses.Scheduled)
        {
            return BadRequest(new
            {
                message = "Race hiện không mở đăng ký."
            });
        }

        if (race.RaceDate.Date < DateTime.UtcNow.Date)
        {
            return BadRequest(new
            {
                message = "Race đã diễn ra, không thể chọn ngựa."
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
                h.AlreadyRegistered,
                h.Age,
                h.WeightKg,
                race.TournamentMinAge,
                race.TournamentMaxAge,
                race.TournamentMinWeight,
                race.TournamentMaxWeight);

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

        var race = await _context.Races
            .Include(r => r.Tournament)
            .Include(r => r.RaceRegistrations)
            .FirstOrDefaultAsync(r => r.RaceId == request.RaceId);

        if (race == null)
        {
            return NotFound(new { message = "Không tìm thấy race." });
        }

        if (race.Tournament.Status != TournamentStatuses.OpenRegistration)
        {
            return BadRequest(new { message = "Tournament hiện không mở đăng ký." });
        }

        if (race.Status != RaceStatuses.Open)
        {
            return BadRequest(new { message = "Race hiện không mở đăng ký." });
        }

        if (race.RaceDate.Date < DateTime.UtcNow.Date)
        {
            return BadRequest(new { message = "Race đã diễn ra, không thể đăng ký." });
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
                r.Status != RaceRegistrationStatuses.Cancelled),
            horse.Age,
            horse.WeightKg,
            race.Tournament.MinHorseAge,
            race.Tournament.MaxHorseAge,
            race.Tournament.MinHorseWeightKg,
            race.Tournament.MaxHorseWeightKg);

        if (ineligibleReason != null)
        {
            return BadRequest(new { message = ineligibleReason });
        }

        var registration = new RaceRegistration
        {
            RaceId = request.RaceId,
            HorseId = request.HorseId,
            OwnerId = ownerId.Value,
            JockeyId = null,
            Status = RaceRegistrationStatuses.Pending,
            SubmittedAt = DateTime.UtcNow
        };

        _context.RaceRegistrations.Add(registration);

        await _context.SaveChangesAsync();

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
                r.Status == RaceRegistrationStatuses.Pending)
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
                ApprovedRegistrationStatuses.Contains(r.Status))
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
                r.OwnerId == ownerId.Value)
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
                r.OwnerId == ownerId.Value)
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
                Key = "Approved",
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
                Key = "JockeyInvited",
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
                Key = "ReadyToRace",
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







