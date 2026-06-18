using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Owner;
using Eliteracingleague.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Controllers.Owner;

[Route("api/owner/jockey-assignment")]
[ApiController]
[Authorize(Roles = UserRoles.HorseOwner)]
public class OwnerJockeyAssignmentController : OwnerBaseController
{
    private static readonly string[] AssignableRegistrationStatuses =
    {
        RaceRegistrationStatuses.Approved,
        RaceRegistrationStatuses.JockeyInvited
    };

    private static readonly string[] BusyRegistrationStatuses =
    {
        RaceRegistrationStatuses.ReadyToRace,
        RaceRegistrationStatuses.Completed
    };

    public OwnerJockeyAssignmentController(EliteRacingLeagueContext context) : base(context)
    {
    }

    [HttpGet("{registrationId:int}/context")]
    public async Task<IActionResult> GetContext(int registrationId)
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

        var data = await LoadRegistrationContextAsync(registrationId, ownerId.Value);

        if (data == null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy đăng ký race hoặc bạn không có quyền xem."
            });
        }

        if (!CanOpenAssignmentPage(data))
        {
            return BadRequest(new
            {
                message = "Đăng ký race hiện không thể chọn Jockey.",
                registrationStatus = data.RegistrationStatus,
                raceStatus = data.RaceStatus
            });
        }

        return Ok(new OwnerJockeyAssignmentContextResponse
        {
            RegistrationId = data.RegistrationId,
            RegistrationStatus = data.RegistrationStatus,
            TournamentId = data.TournamentId,
            TournamentName = data.TournamentName,
            RaceId = data.RaceId,
            RaceName = data.RaceName,
            RaceDate = data.RaceDate,
            Location = data.Location,
            DistanceMeters = data.DistanceMeters,
            HorseId = data.HorseId,
            HorseName = data.HorseName,
            BreedName = data.BreedName,
            Age = data.Age,
            HeightCm = data.HeightCm,
            WeightKg = data.WeightKg,
            HealthStatus = data.HealthStatus,
            HorseIsActive = data.HorseIsActive,
            AssignedJockeyId = data.AssignedJockeyId,
            AssignedJockeyName = data.AssignedJockeyName
        });
    }

    [HttpGet("{registrationId:int}/candidates")]
    public async Task<IActionResult> GetCandidates(
        int registrationId,
        [FromQuery] string? search,
        [FromQuery] string? healthStatus,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
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

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var registration = await LoadRegistrationContextAsync(registrationId, ownerId.Value);

        if (registration == null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy đăng ký race hoặc bạn không có quyền xem."
            });
        }

        var assignmentError = ValidateCanAssignJockey(registration);

        if (assignmentError != null)
        {
            return BadRequest(new { message = assignmentError });
        }

        if (IsInactiveFilter(status))
        {
            return Ok(new OwnerJockeyCandidateListResponse
            {
                Page = page,
                PageSize = pageSize,
                TotalItems = 0,
                TotalPages = 0
            });
        }

        var raceDate = DateOnly.FromDateTime(registration.RaceDate.Date);
        var raceDateStart = registration.RaceDate.Date;
        var raceDateEnd = raceDateStart.AddDays(1);
        var query = _context.Jockeys
            .AsNoTracking()
            .Where(j =>
                j.JockeyNavigation.Role == UserRoles.Jockey &&
                j.JockeyNavigation.Status == UserStatuses.Active &&
                j.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim();
            query = query.Where(j => j.JockeyNavigation.FullName.Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(healthStatus))
        {
            var normalizedHealthStatus = healthStatus.Trim();
            query = query.Where(j => j.HealthStatus == normalizedHealthStatus);
        }

        var candidates = await query
            .Select(j => new CandidateSource
            {
                JockeyId = j.JockeyId,
                FullName = j.JockeyNavigation.FullName,
                ProfileImageUrl = j.ProfileImageUrl,
                WeightKg = j.WeightKg,
                YearsOfExperience = j.YearsOfExperience,
                HealthStatus = j.HealthStatus,
                IsActive = j.IsActive,
                UserStatus = j.JockeyNavigation.Status,
                AvailabilityStatus = j.RaceRegistrations.Any(r =>
                        BusyRegistrationStatuses.Contains(r.Status) &&
                        r.Race.RaceDate >= raceDateStart &&
                        r.Race.RaceDate < raceDateEnd &&
                        r.Race.Status != RaceStatuses.Cancelled)
                    ? JockeyAvailabilityStatuses.RacingDay
                    : j.JockeyAvailabilities
                        .Where(a => a.AvailableDate == raceDate)
                        .Select(a => a.Status)
                        .FirstOrDefault(),
                DistanceSkillLevel = j.JockeyDistanceExperiences
                    .Where(e => e.DistanceMeters == registration.DistanceMeters)
                    .Select(e => e.SkillLevel)
                    .FirstOrDefault(),
                BreedSkillLevel = j.JockeyBreedExperiences
                    .Where(e => e.BreedId == registration.BreedId)
                    .Select(e => e.ExperienceLevel)
                    .FirstOrDefault(),
                InvitationStatus = j.JockeyInvitations
                    .Where(i => i.RegistrationId == registrationId)
                    .Select(i => i.Status)
                    .FirstOrDefault()
            })
            .ToListAsync();

        var rankedCandidates = candidates
            .Select(c => BuildCandidateResponse(c, registration))
            .OrderByDescending(c => c.TotalScore)
            .ThenBy(c => c.FullName)
            .ToList();

        for (var i = 0; i < rankedCandidates.Count; i++)
        {
            rankedCandidates[i].RankNo = i + 1;
        }

        var totalItems = rankedCandidates.Count;
        var items = rankedCandidates
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Ok(new OwnerJockeyCandidateListResponse
        {
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize),
            Items = items
        });
    }

    [HttpPost("{registrationId:int}/invitations")]
    public async Task<IActionResult> SendInvitation(int registrationId, SendJockeyInvitationRequest request)
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

        if (request.JockeyId <= 0)
        {
            return BadRequest(new { message = "JockeyId không hợp lệ." });
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();

        var registration = await _context.RaceRegistrations
            .Include(r => r.Race)
            .ThenInclude(r => r.Tournament)
            .Include(r => r.Horse)
            .FirstOrDefaultAsync(r =>
                r.RegistrationId == registrationId &&
                r.OwnerId == ownerId.Value);

        if (registration == null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy đăng ký race hoặc bạn không có quyền mời jockey."
            });
        }

        var assignmentError = ValidateCanAssignJockey(registration);

        if (assignmentError != null)
        {
            return BadRequest(new { message = assignmentError });
        }

        var jockey = await _context.Jockeys
            .Include(j => j.JockeyNavigation)
            .FirstOrDefaultAsync(j => j.JockeyId == request.JockeyId);

        if (jockey == null)
        {
            return NotFound(new { message = "Không tìm thấy hồ sơ jockey." });
        }

        if (jockey.JockeyNavigation.Role != UserRoles.Jockey ||
            jockey.JockeyNavigation.Status != UserStatuses.Active ||
            !jockey.IsActive)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Chỉ có thể mời Jockey Active.",
                status = jockey.JockeyNavigation.Status,
                isActive = jockey.IsActive
            });
        }

        if (!JockeyHealthStatuses.CanRace(jockey.HealthStatus))
        {
            return BadRequest(new
            {
                message = "Jockey không đủ điều kiện sức khỏe để tham gia race.",
                healthStatus = jockey.HealthStatus
            });
        }

        var raceDate = DateOnly.FromDateTime(registration.Race.RaceDate.Date);
        var isUnavailable = await _context.JockeyAvailabilities
            .AsNoTracking()
            .AnyAsync(a =>
                a.JockeyId == request.JockeyId &&
                a.AvailableDate == raceDate &&
                a.Status == JockeyAvailabilityStatuses.Unavailable);

        if (isUnavailable)
        {
            return BadRequest(new { message = "Jockey is unavailable on race day" });
        }

        var raceDateStart = registration.Race.RaceDate.Date;
        var raceDateEnd = raceDateStart.AddDays(1);
        var hasRaceOnSameDay = await _context.RaceRegistrations
            .AsNoTracking()
            .AnyAsync(r =>
                r.RegistrationId != registrationId &&
                r.JockeyId == request.JockeyId &&
                BusyRegistrationStatuses.Contains(r.Status) &&
                r.Race.RaceDate >= raceDateStart &&
                r.Race.RaceDate < raceDateEnd &&
                r.Race.Status != RaceStatuses.Cancelled);

        if (hasRaceOnSameDay)
        {
            return BadRequest(new { message = "Jockey already has a race on this day" });
        }

        var existingInvitation = await _context.JockeyInvitations
            .AsNoTracking()
            .Where(i =>
                i.RegistrationId == registrationId &&
                i.JockeyId == request.JockeyId)
            .Select(i => i.Status)
            .FirstOrDefaultAsync();

        if (existingInvitation == InvitationStatuses.Pending ||
            existingInvitation == InvitationStatuses.Accepted)
        {
            return BadRequest(new
            {
                message = "Jockey này đã có lời mời cho đăng ký race này.",
                invitationStatus = existingInvitation
            });
        }

        if (existingInvitation != null)
        {
            return BadRequest(new
            {
                message = "Jockey này đã từng được mời cho đăng ký race này, không thể tạo trùng theo ràng buộc hiện có.",
                invitationStatus = existingInvitation
            });
        }

        var ownerName = await _context.Users
            .AsNoTracking()
            .Where(u => u.UserId == ownerId.Value)
            .Select(u => u.FullName)
            .FirstAsync();

        var now = DateTime.UtcNow;
        var invitation = new JockeyInvitation
        {
            RegistrationId = registrationId,
            JockeyId = request.JockeyId,
            InvitedByOwnerId = ownerId.Value,
            Status = InvitationStatuses.Pending,
            FeeAmount = request.FeeAmount,
            Message = string.IsNullOrWhiteSpace(request.Message) ? null : request.Message.Trim(),
            SentAt = now
        };

        _context.JockeyInvitations.Add(invitation);

        if (registration.Status == RaceRegistrationStatuses.Approved)
        {
            registration.Status = RaceRegistrationStatuses.JockeyInvited;
        }

        _context.Notifications.Add(new Notification
        {
            UserId = request.JockeyId,
            Title = "Bạn có lời mời tham gia cuộc đua",
            Message = string.IsNullOrWhiteSpace(request.Message)
                ? $"{ownerName} đã mời bạn tham gia cuộc đua {registration.Race.RaceName}."
                : request.Message.Trim(),
            IsRead = false,
            CreatedAt = now
        });

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return Ok(new
        {
            message = "Đã gửi lời mời cho Jockey.",
            invitationId = invitation.InvitationId,
            invitationStatus = invitation.Status
        });
    }

    private async Task<AssignmentRegistrationData?> LoadRegistrationContextAsync(int registrationId, int ownerId)
    {
        return await _context.RaceRegistrations
            .AsNoTracking()
            .Where(r =>
                r.RegistrationId == registrationId &&
                r.OwnerId == ownerId)
            .Select(r => new AssignmentRegistrationData
            {
                RegistrationId = r.RegistrationId,
                RegistrationStatus = r.Status,
                TournamentId = r.Race.TournamentId,
                TournamentName = r.Race.Tournament.TournamentName,
                RaceId = r.RaceId,
                RaceName = r.Race.RaceName,
                RaceDate = r.Race.RaceDate,
                RaceStatus = r.Race.Status,
                Location = r.Race.Location ?? r.Race.Tournament.Location,
                DistanceMeters = r.Race.DistanceMeters,
                HorseId = r.HorseId,
                HorseName = r.Horse.HorseName,
                BreedId = r.Horse.BreedId,
                BreedName = r.Horse.Breed.BreedName,
                Age = r.Horse.Age,
                HeightCm = r.Horse.HeightCm,
                WeightKg = r.Horse.WeightKg,
                HealthStatus = r.Horse.HealthStatus,
                HorseIsActive = r.Horse.IsActive,
                AssignedJockeyId = r.JockeyId,
                AssignedJockeyName = r.Jockey == null ? null : r.Jockey.JockeyNavigation.FullName
            })
            .FirstOrDefaultAsync();
    }

    private static bool CanOpenAssignmentPage(AssignmentRegistrationData data)
    {
        if (data.AssignedJockeyId != null &&
            data.RegistrationStatus == RaceRegistrationStatuses.ReadyToRace)
        {
            return true;
        }

        return AssignableRegistrationStatuses.Contains(data.RegistrationStatus) &&
            data.AssignedJockeyId == null &&
            data.RaceStatus != RaceStatuses.Completed &&
            data.RaceStatus != RaceStatuses.Cancelled;
    }

    private static string? ValidateCanAssignJockey(AssignmentRegistrationData data)
    {
        if (data.AssignedJockeyId != null &&
            data.RegistrationStatus == RaceRegistrationStatuses.ReadyToRace)
        {
            return "This registration already has an assigned jockey.";
        }

        if (data.AssignedJockeyId != null)
        {
            return "Đăng ký race đã có jockey.";
        }

        if (!AssignableRegistrationStatuses.Contains(data.RegistrationStatus))
        {
            return "Chỉ đăng ký Approved hoặc JockeyInvited mới có thể mời jockey.";
        }

        if (data.RaceStatus == RaceStatuses.Completed ||
            data.RaceStatus == RaceStatuses.Cancelled)
        {
            return "Race đã hoàn thành hoặc đã bị hủy, không thể mời jockey.";
        }

        return null;
    }

    private static string? ValidateCanAssignJockey(RaceRegistration registration)
    {
        if (registration.JockeyId != null &&
            registration.Status == RaceRegistrationStatuses.ReadyToRace)
        {
            return "This registration already has an assigned jockey.";
        }

        if (registration.JockeyId != null)
        {
            return "Đăng ký race đã có jockey.";
        }

        if (!AssignableRegistrationStatuses.Contains(registration.Status))
        {
            return "Chỉ đăng ký Approved hoặc JockeyInvited mới có thể mời jockey.";
        }

        if (registration.Race.Status == RaceStatuses.Completed ||
            registration.Race.Status == RaceStatuses.Cancelled)
        {
            return "Race đã hoàn thành hoặc đã bị hủy, không thể mời jockey.";
        }

        return null;
    }

    private static OwnerJockeyCandidateResponse BuildCandidateResponse(
        CandidateSource candidate,
        AssignmentRegistrationData registration)
    {
        var distanceSkillLevel = candidate.DistanceSkillLevel ?? JockeyDistanceSkillLevels.NoExperience;
        var normalizedAvailability = candidate.AvailabilityStatus ?? JockeyAvailabilityStatuses.Available;
        var availabilityScore = normalizedAvailability == JockeyAvailabilityStatuses.Available ? 30 : 0;
        var weightScore = ScoreWeight(candidate.WeightKg);
        var experienceScore = ScoreExperience(candidate.YearsOfExperience);
        var distanceScore = ScoreDistanceSkill(distanceSkillLevel);
        var breedExperienceScore = ScoreBreedSkill(candidate.BreedSkillLevel);
        var totalScore = availabilityScore + distanceScore + breedExperienceScore + experienceScore + weightScore;
        var cannotInviteReason = GetCannotInviteReason(candidate, registration);

        return new OwnerJockeyCandidateResponse
        {
            JockeyId = candidate.JockeyId,
            FullName = candidate.FullName,
            ProfileImageUrl = candidate.ProfileImageUrl,
            WeightKg = candidate.WeightKg,
            YearsOfExperience = candidate.YearsOfExperience,
            HealthStatus = candidate.HealthStatus,
            IsActive = candidate.IsActive,
            UserStatus = candidate.UserStatus,
            AvailabilityStatus = normalizedAvailability,
            DistanceSkillLevel = distanceSkillLevel,
            BreedSkillLevel = candidate.BreedSkillLevel,
            AvailabilityScore = availabilityScore,
            WeightScore = weightScore,
            ExperienceScore = experienceScore,
            DistanceScore = distanceScore,
            BreedExperienceScore = breedExperienceScore,
            TotalScore = totalScore,
            RecommendationLevel = GetRecommendationLevel(totalScore),
            PrimaryReason = GetPrimaryReason(availabilityScore, distanceScore, breedExperienceScore, experienceScore),
            AlreadyInvited = candidate.InvitationStatus != null,
            InvitationStatus = candidate.InvitationStatus,
            CanInvite = cannotInviteReason == null,
            CannotInviteReason = cannotInviteReason
        };
    }

    private static string? GetCannotInviteReason(CandidateSource candidate, AssignmentRegistrationData registration)
    {
        if (candidate.UserStatus != UserStatuses.Active || !candidate.IsActive)
        {
            return "Jockey is inactive";
        }

        if (candidate.InvitationStatus == InvitationStatuses.Pending)
        {
            return "Invitation pending";
        }

        if (candidate.InvitationStatus == InvitationStatuses.Accepted)
        {
            return "Jockey already assigned";
        }

        if (registration.AssignedJockeyId != null)
        {
            return "This registration already has an assigned jockey";
        }

        if (registration.RaceStatus == RaceStatuses.Completed ||
            registration.RaceStatus == RaceStatuses.Cancelled)
        {
            return "Race is completed or cancelled";
        }

        if (candidate.AvailabilityStatus == JockeyAvailabilityStatuses.Unavailable)
        {
            return "Jockey is unavailable on race day";
        }

        if (candidate.AvailabilityStatus == JockeyAvailabilityStatuses.RacingDay)
        {
            return "Jockey already has a race on this day";
        }

        if (!JockeyHealthStatuses.CanRace(candidate.HealthStatus))
        {
            return "Jockey health status is not eligible";
        }

        if (candidate.InvitationStatus != null)
        {
            return "Jockey has already been invited";
        }

        return null;
    }

    private static int ScoreDistanceSkill(string skillLevel)
    {
        return skillLevel switch
        {
            JockeyDistanceSkillLevels.Expert => 25,
            JockeyDistanceSkillLevels.Good => 18,
            JockeyDistanceSkillLevels.Basic => 10,
            _ => 0
        };
    }

    private static int ScoreBreedSkill(string? skillLevel)
    {
        return skillLevel switch
        {
            JockeyBreedSkillLevels.Expert => 20,
            JockeyBreedSkillLevels.Good => 14,
            JockeyBreedSkillLevels.Basic => 8,
            _ => 0
        };
    }

    private static int ScoreExperience(int yearsOfExperience)
    {
        if (yearsOfExperience >= 5)
        {
            return 15;
        }

        if (yearsOfExperience >= 3)
        {
            return 10;
        }

        if (yearsOfExperience >= 1)
        {
            return 5;
        }

        return 0;
    }

    private static int ScoreWeight(decimal weightKg)
    {
        if (weightKg >= 45 && weightKg <= 60)
        {
            return 10;
        }

        if (weightKg > 60 && weightKg <= 65)
        {
            return 7;
        }

        return 4;
    }

    private static string GetRecommendationLevel(int totalScore)
    {
        if (totalScore >= 80)
        {
            return "Excellent";
        }

        if (totalScore >= 60)
        {
            return "Good";
        }

        if (totalScore >= 40)
        {
            return "Fair";
        }

        return "Low";
    }

    private static string GetPrimaryReason(
        int availabilityScore,
        int distanceScore,
        int breedExperienceScore,
        int experienceScore)
    {
        if (distanceScore >= 18 && breedExperienceScore >= 14)
        {
            return "Strong distance skill and breed experience";
        }

        if (availabilityScore == 0)
        {
            return "Jockey is not available on race day";
        }

        if (distanceScore >= 18)
        {
            return "Strong distance skill";
        }

        if (breedExperienceScore >= 14)
        {
            return "Strong breed experience";
        }

        if (experienceScore >= 10)
        {
            return "Experienced jockey";
        }

        return "Basic match";
    }

    private static bool IsInactiveFilter(string? status)
    {
        return !string.IsNullOrWhiteSpace(status) &&
            !status.Equals("All", StringComparison.OrdinalIgnoreCase) &&
            !status.Equals(UserStatuses.Active, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class AssignmentRegistrationData
    {
        public int RegistrationId { get; set; }
        public string RegistrationStatus { get; set; } = null!;
        public int TournamentId { get; set; }
        public string TournamentName { get; set; } = null!;
        public int RaceId { get; set; }
        public string RaceName { get; set; } = null!;
        public DateTime RaceDate { get; set; }
        public string RaceStatus { get; set; } = null!;
        public string? Location { get; set; }
        public int DistanceMeters { get; set; }
        public int HorseId { get; set; }
        public string HorseName { get; set; } = null!;
        public int BreedId { get; set; }
        public string BreedName { get; set; } = null!;
        public int Age { get; set; }
        public decimal? HeightCm { get; set; }
        public decimal WeightKg { get; set; }
        public string HealthStatus { get; set; } = null!;
        public bool HorseIsActive { get; set; }
        public int? AssignedJockeyId { get; set; }
        public string? AssignedJockeyName { get; set; }
    }

    private sealed class CandidateSource
    {
        public int JockeyId { get; set; }
        public string FullName { get; set; } = null!;
        public string? ProfileImageUrl { get; set; }
        public decimal WeightKg { get; set; }
        public int YearsOfExperience { get; set; }
        public string HealthStatus { get; set; } = null!;
        public bool IsActive { get; set; }
        public string UserStatus { get; set; } = null!;
        public string? AvailabilityStatus { get; set; }
        public string? DistanceSkillLevel { get; set; }
        public string? BreedSkillLevel { get; set; }
        public string? InvitationStatus { get; set; }
    }
}
