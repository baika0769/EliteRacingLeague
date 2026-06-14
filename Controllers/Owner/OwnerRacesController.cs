using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Owner;
using Eliteracingleague.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Controllers.Owner;

[Route("api/owner/races")]
[ApiController]
[Authorize(Roles = UserRoles.HorseOwner)]
public class OwnerRacesController : OwnerBaseController
{
    public OwnerRacesController(EliteRacingLeagueContext context) : base(context)
    {
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
                    r.Status == "Open" ||
                    r.RaceRegistrations.Any(rr => rr.OwnerId == ownerId.Value)
                ))
            .Select(r => new
            {
                r.RaceId,
                r.RaceName,
                r.RaceDate,
                r.DistanceMeters,
                r.Location,
                r.Status,
                TournamentName = r.Tournament.TournamentName,
                TournamentLocation = r.Tournament.Location
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
            TournamentName = race.TournamentName,
            RaceName = race.RaceName,
            RaceDate = race.RaceDate.ToString("yyyy-MM-dd"),
            Location = race.Location ?? race.TournamentLocation,
            Distance = race.DistanceMeters,
            Status = race.Status
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

        if (registration.Race.Status == RaceStatuses.Cancelled ||
            registration.Race.Status == RaceStatuses.Completed)
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

        var invitationExists = await _context.JockeyInvitations
            .AnyAsync(i =>
                i.RegistrationId == registrationId &&
                i.JockeyId == request.JockeyId);

        if (invitationExists)
        {
            return BadRequest(new
            {
                message = "Jockey này đã được mời cho đăng ký race này."
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
        registration.Status = RaceRegistrationStatuses.JockeyInvited;

        _context.Notifications.Add(new Notification
        {
            UserId = request.JockeyId,
            Title = "Bạn có lời mời tham gia cuộc đua",
            Message = $"{ownerName} đã mời bạn tham gia cuộc đua {registration.Race.RaceName}.",
            IsRead = false,
            CreatedAt = now
        });

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return Ok(new
        {
            message = "Đã gửi lời mời jockey.",
            invitationId = invitation.InvitationId,
            status = invitation.Status
        });
    }
}
