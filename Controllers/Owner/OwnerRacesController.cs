using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Owner;
using Eliteracingleague.API.Models;
using Eliteracingleague.API.Services.Racing;
using Eliteracingleague.API.Services.SystemTime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Controllers.Owner;

[Route("api/owner/races")]
[ApiController]
[Authorize(Roles = UserRoles.HorseOwner)]
public class OwnerRacesController : OwnerBaseController
{
    private static readonly string[] BusyRegistrationStatuses =
    {
        RaceRegistrationStatuses.ReadyToRace,
        RaceRegistrationStatuses.Completed
    };

    private readonly IDateTimeProvider _dateTimeProvider;

    public OwnerRacesController(
        EliteRacingLeagueContext context,
        IDateTimeProvider dateTimeProvider) : base(context)
    {
        _dateTimeProvider = dateTimeProvider;
    }

    [HttpGet("{raceId:int}")]
    public async Task<IActionResult> GetRaceDetail(int raceId)
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
            .Where(r =>
                r.RaceId == raceId &&
                (
                    RaceStatuses.RegisterableStatuses.Contains(r.Status) ||
                    r.RaceRegistrations.Any(rr => rr.OwnerId == ownerId.Value)
                ))
            .Select(r => new
            {
                r.RaceId,
                r.TournamentId,
                r.RaceName,
                r.RaceDate,
                r.DistanceMeters,
                r.Location,
                r.MaxHorses,
                r.Status,
                r.JockeySelectionDeadline,
                r.PredictionDeadline,
                TournamentName = r.Tournament.TournamentName,
                TournamentDescription = r.Tournament.Description,
                TournamentStatus = r.Tournament.Status,
                TournamentImageUrl = r.Tournament.ImageUrl,
                r.Tournament.PrizePool,
                r.Tournament.Rules,
                TournamentStartDate = r.Tournament.StartDate,
                TournamentEndDate = r.Tournament.EndDate,
                TournamentLocation = r.Tournament.Location,
                Registration = r.RaceRegistrations
                    .Where(rr => rr.OwnerId == ownerId.Value)
                    .OrderByDescending(rr => rr.SubmittedAt)
                    .ThenByDescending(rr => rr.RegistrationId)
                    .Select(rr => new
                    {
                        rr.RegistrationId,
                        rr.Status,
                        rr.Horse.HorseName,
                        OfficialJockeyName = rr.Jockey != null
                            ? rr.Jockey.JockeyNavigation.FullName
                            : null
                    })
                    .FirstOrDefault(),
                RefereeAssignment = r.RefereeAssignments
                    .Where(ra => ra.Status == RefereeAssignmentStatuses.Assigned)
                    .OrderByDescending(ra => ra.AssignedAt)
                    .ThenByDescending(ra => ra.RefereeAssignmentId)
                    .Select(ra => new
                    {
                        ra.RefereeAssignmentId,
                        ra.RefereeId,
                        RefereeName = ra.Referee.Referee.FullName,
                        RefereeEmail = ra.Referee.Referee.Email,
                        RefereePhone = ra.Referee.Referee.Phone,
                        ra.Referee.LicenseNo,
                        ra.Referee.ExperienceYears,
                        ra.Status,
                        ra.AssignedAt
                    })
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync();

        if (race == null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy race hoặc bạn không có quyền xem race này."
            });
        }

        var response = new OwnerRaceDetailResponse
        {
            RaceId = race.RaceId,
            TournamentId = race.TournamentId,
            TournamentName = race.TournamentName,
            TournamentDescription = race.TournamentDescription,
            TournamentStatus = race.TournamentStatus,
            TournamentImageUrl = race.TournamentImageUrl,
            PrizePool = race.PrizePool,
            Rules = race.Rules,
            TournamentStartDate = race.TournamentStartDate.ToString("yyyy-MM-dd"),
            TournamentEndDate = race.TournamentEndDate.ToString("yyyy-MM-dd"),
            RaceName = race.RaceName,
            RaceDate = race.RaceDate.ToString("yyyy-MM-dd"),
            Location = race.Location ?? race.TournamentLocation,
            Distance = race.DistanceMeters,
            MaxHorses = race.MaxHorses,
            Status = race.Status,
            JockeySelectionDeadline = race.JockeySelectionDeadline?.ToString("yyyy-MM-dd HH:mm"),
            PredictionDeadline = race.PredictionDeadline?.ToString("yyyy-MM-dd HH:mm"),
            RegistrationId = race.Registration?.RegistrationId,
            RegistrationStatus = race.Registration?.Status,
            HorseName = race.Registration?.HorseName,
            OfficialJockeyName = race.Registration?.OfficialJockeyName,
            RefereeAssignmentId = race.RefereeAssignment?.RefereeAssignmentId,
            RefereeId = race.RefereeAssignment?.RefereeId,
            RefereeName = race.RefereeAssignment?.RefereeName,
            RefereeEmail = race.RefereeAssignment?.RefereeEmail,
            RefereePhone = race.RefereeAssignment?.RefereePhone,
            RefereeLicenseNo = race.RefereeAssignment?.LicenseNo,
            RefereeExperienceYears = race.RefereeAssignment?.ExperienceYears,
            RefereeAssignmentStatus = race.RefereeAssignment?.Status,
            RefereeAssignedAt = race.RefereeAssignment?.AssignedAt.ToString("yyyy-MM-dd HH:mm")
        };

        return Ok(response);
    }

    [HttpPost("registrations/{registrationId:int}/jockey-invitations")]
    public async Task<IActionResult> InviteJockey(int registrationId, InviteJockeyRequest request)
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
                .ThenInclude(race => race.Tournament)
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

        var localNow = _dateTimeProvider.GetLocalNow(_dateTimeProvider.TimeZoneId);
        if (RegistrationClosureHelper.IsRegistrationClosed(
            registration.Race.Tournament,
            localNow))
        {
            await RegistrationClosureHelper.ApplyAsync(
                _context,
                new[] { registration.Race.TournamentId },
                _dateTimeProvider.UtcNow);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return BadRequest(new
            {
                message = "Registration is closed. Jockey invitations are no longer allowed."
            });
        }

        if (registration.JockeyId != null)
        {
            return BadRequest(new { message = "Đăng ký race đã có jockey." });
        }

        if (registration.Status != RaceRegistrationStatuses.Approved &&
            registration.Status != RaceRegistrationStatuses.JockeyInvited)
        {
            return BadRequest(new
            {
                message = "Chỉ đăng ký đã được duyệt hoặc đang chờ jockey mới có thể mời jockey.",
                status = registration.Status
            });
        }

        if (RaceStatuses.IsClosedForJockeyAssignment(registration.Race.Status))
        {
            return BadRequest(new
            {
                message = "Race đã kết thúc hoặc đã bị hủy, không thể mời jockey.",
                raceStatus = registration.Race.Status
            });
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
                message = "Jockey health status is not eligible",
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

        var existingInvitationStatus = await _context.JockeyInvitations
            .AsNoTracking()
            .Where(i =>
                i.RegistrationId == registrationId &&
                i.JockeyId == request.JockeyId)
            .Select(i => i.Status)
            .FirstOrDefaultAsync();

        if (existingInvitationStatus == InvitationStatuses.Pending)
        {
            return BadRequest(new
            {
                message = "Invitation pending",
                invitationStatus = existingInvitationStatus
            });
        }

        if (existingInvitationStatus == InvitationStatuses.Accepted)
        {
            return BadRequest(new
            {
                message = "Jockey already assigned",
                invitationStatus = existingInvitationStatus
            });
        }

        if (existingInvitationStatus != null)
        {
            return BadRequest(new
            {
                message = "Jockey này đã từng được mời cho đăng ký race này, không thể tạo trùng theo ràng buộc hiện có.",
                invitationStatus = existingInvitationStatus
            });
        }

        var ownerName = await _context.Users
            .AsNoTracking()
            .Where(u => u.UserId == ownerId.Value)
            .Select(u => u.FullName)
            .FirstAsync();

        var now = _dateTimeProvider.UtcNow;
        var invitation = new JockeyInvitation
        {
            RegistrationId = registrationId,
            JockeyId = request.JockeyId,
            InvitedByOwnerId = ownerId.Value,
            Status = InvitationStatuses.Pending,
            FeeAmount = request.FeeAmount,
            Message = string.IsNullOrWhiteSpace(request.Message) ? null : request.Message.Trim(),
            SentAt = now,
            ExpiresAt = RegistrationClosureHelper.GetRegistrationCloseLocal(
                registration.Race.Tournament.StartDate)
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
            CreatedAt = now,
            ActionType = "JockeyInvitation",
            ActionUrl = "/jockey/invitations",
            RelatedType = "RaceRegistration",
            RelatedId = registration.RegistrationId
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
}
