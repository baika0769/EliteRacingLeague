using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Jockey;
using Eliteracingleague.API.Models;
using Eliteracingleague.API.Services;
using Eliteracingleague.API.Services.JockeyMatching;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Eliteracingleague.API.Controllers.Jockey;

[Route("api/jockey/invitations")]
[ApiController]
[Authorize(Roles = UserRoles.Jockey)]
public class JockeyInvitationsController : ControllerBase
{
    private readonly EliteRacingLeagueContext _context;
    private readonly JockeyAccessService _jockeyAccess;
    private readonly IJockeyMatchScoreService _matchScoreService;

    public JockeyInvitationsController(
        EliteRacingLeagueContext context,
        JockeyAccessService jockeyAccess,
        IJockeyMatchScoreService matchScoreService)
    {
        _context = context;
        _jockeyAccess = jockeyAccess;
        _matchScoreService = matchScoreService;
    }

    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingInvitations()
    {
        var jockeyId = GetCurrentJockeyId();

        if (jockeyId == null)
        {
            return InvalidToken();
        }

        var profileError = await ValidateActiveJockeyAsync(jockeyId.Value);

        if (profileError != null)
        {
            return profileError;
        }

        var jockey = await _context.Jockeys
            .AsNoTracking()
            .Include(j => j.JockeyDistanceExperiences)
            .Include(j => j.JockeyBreedExperiences)
            .FirstAsync(j => j.JockeyId == jockeyId.Value);

        var invitations = await _context.JockeyInvitations
            .AsNoTracking()
            .Include(i => i.Registration)
                .ThenInclude(r => r.Race)
                    .ThenInclude(r => r.Tournament)
            .Include(i => i.Registration)
                .ThenInclude(r => r.Horse)
                    .ThenInclude(h => h.Breed)
            .Include(i => i.InvitedByOwner)
                .ThenInclude(o => o.Owner)
            .Where(i => i.JockeyId == jockeyId.Value
                && i.Status == InvitationStatuses.Pending)
            .OrderByDescending(i => i.SentAt)
            .ToListAsync();

        var response = invitations.Select(i =>
        {
            var match = _matchScoreService.Calculate(
                jockey,
                i.Registration.Horse,
                i.Registration.Race,
                i.Registration.Horse.Breed);

            return new JockeyPendingInvitationResponse
            {
                InvitationId = i.InvitationId,
                RegistrationId = i.RegistrationId,
                TournamentId = i.Registration.Race.TournamentId,
                TournamentName = i.Registration.Race.Tournament.TournamentName,
                RaceId = i.Registration.RaceId,
                RaceName = i.Registration.Race.RaceName,
                RaceDate = i.Registration.Race.RaceDate,
                Location = i.Registration.Race.Location ?? i.Registration.Race.Tournament.Location,
                DistanceMeters = i.Registration.Race.DistanceMeters,
                SurfaceType = null,
                JockeySelectionDeadline = i.Registration.Race.JockeySelectionDeadline,
                HorseId = i.Registration.HorseId,
                HorseName = i.Registration.Horse.HorseName,
                HorseImageUrl = i.Registration.Horse.ImageUrl,
                BreedName = i.Registration.Horse.Breed.BreedName,
                Age = i.Registration.Horse.Age,
                HorseHealthStatus = i.Registration.Horse.HealthStatus,
                OwnerId = i.InvitedByOwnerId,
                OwnerName = i.InvitedByOwner.Owner.FullName,
                OwnerMessage = i.Message,
                FeeAmount = i.FeeAmount,
                Status = i.Status,
                SentAt = i.SentAt,
                MatchScore = match.MatchScore,
                MatchReasons = match.MatchReasons
            };
        }).ToList();

        return Ok(response);
    }

    [HttpGet("{invitationId:int}")]
    public async Task<IActionResult> GetInvitationDetail(int invitationId)
    {
        var jockeyId = GetCurrentJockeyId();

        if (jockeyId == null)
        {
            return InvalidToken();
        }

        var profileError = await ValidateActiveJockeyAsync(jockeyId.Value);

        if (profileError != null)
        {
            return profileError;
        }

        var jockey = await _context.Jockeys
            .AsNoTracking()
            .Include(j => j.JockeyDistanceExperiences)
            .Include(j => j.JockeyBreedExperiences)
            .FirstAsync(j => j.JockeyId == jockeyId.Value);

        var invitation = await _context.JockeyInvitations
            .AsNoTracking()
            .Include(i => i.Registration)
                .ThenInclude(r => r.Race)
                    .ThenInclude(r => r.Tournament)
            .Include(i => i.Registration)
                .ThenInclude(r => r.Horse)
                    .ThenInclude(h => h.Breed)
            .Include(i => i.InvitedByOwner)
                .ThenInclude(o => o.Owner)
            .FirstOrDefaultAsync(i =>
                i.InvitationId == invitationId &&
                i.JockeyId == jockeyId.Value);

        if (invitation == null)
        {
            return NotFound(new { message = "Invitation not found." });
        }

        var registration = invitation.Registration;
        var race = registration.Race;
        var horse = registration.Horse;
        var match = _matchScoreService.Calculate(jockey, horse, race, horse.Breed);

        return Ok(new JockeyInvitationDetailResponse
        {
            InvitationId = invitation.InvitationId,
            Status = invitation.Status,
            SentAt = invitation.SentAt,
            RespondedAt = invitation.RespondedAt,
            OwnerMessage = invitation.Message,
            FeeAmount = invitation.FeeAmount,
            Race = new JockeyInvitationRaceResponse
            {
                RaceId = race.RaceId,
                RaceName = race.RaceName,
                RaceDate = race.RaceDate,
                Location = race.Location ?? race.Tournament.Location,
                DistanceMeters = race.DistanceMeters,
                SurfaceType = null,
                JockeySelectionDeadline = race.JockeySelectionDeadline
            },
            Tournament = new JockeyInvitationTournamentResponse
            {
                TournamentId = race.TournamentId,
                TournamentName = race.Tournament.TournamentName
            },
            Horse = new JockeyInvitationHorseResponse
            {
                HorseId = horse.HorseId,
                HorseName = horse.HorseName,
                ImageUrl = horse.ImageUrl,
                BreedName = horse.Breed.BreedName,
                Age = horse.Age,
                HealthStatus = horse.HealthStatus
            },
            Owner = new JockeyInvitationOwnerResponse
            {
                OwnerId = invitation.InvitedByOwnerId,
                OwnerName = invitation.InvitedByOwner.Owner.FullName
            },
            MatchScore = match.MatchScore,
            MatchReasons = match.MatchReasons
        });
    }

    [HttpPut("{id}/accept")]
    public async Task<IActionResult> AcceptInvitation(int id)
    {
        var jockeyId = GetCurrentJockeyId();

        if (jockeyId == null)
        {
            return InvalidToken();
        }

        var profileError = await ValidateActiveJockeyAsync(jockeyId.Value);

        if (profileError != null)
        {
            return profileError;
        }

        var invitation = await LoadOwnedInvitationAsync(id, jockeyId.Value);

        if (invitation == null)
        {
            return NotFound(new { message = "Không tìm thấy lời mời." });
        }

        if (invitation.Registration.JockeyId != null)
        {
            return BadRequest(new
            {
                message = "Official jockey has already been selected for this registration."
            });
        }

        var healthStatus = await _context.Jockeys
            .AsNoTracking()
            .Where(j => j.JockeyId == jockeyId.Value)
            .Select(j => j.HealthStatus)
            .FirstAsync();

        if (!JockeyHealthStatuses.CanRace(healthStatus))
        {
            return BadRequest(new
            {
                message = "Jockey health status is not eligible to race.",
                healthStatus
            });
        }

        var deadline = invitation.Registration.Race.JockeySelectionDeadline;

        if (deadline.HasValue && DateTime.UtcNow > deadline.Value)
        {
            return BadRequest(new
            {
                message = "The jockey selection deadline has passed."
            });
        }

        if (invitation.Status != InvitationStatuses.Pending)
        {
            return BadRequest(new
            {
                message = "Lời mời không còn ở trạng thái chờ.",
                status = invitation.Status
            });
        }

        invitation.Status = InvitationStatuses.Accepted;
        invitation.RespondedAt = DateTime.UtcNow;

        var jockeyName = invitation.Jockey.JockeyNavigation.FullName;
        var horseName = invitation.Registration.Horse.HorseName;

        _context.Notifications.Add(new Notification
        {
            UserId = invitation.InvitedByOwnerId,
            Title = "Invitation Accepted",
            Message = !string.IsNullOrWhiteSpace(jockeyName) &&
                !string.IsNullOrWhiteSpace(horseName)
                    ? $"{jockeyName} accepted invitation for {horseName}."
                    : "A jockey accepted your invitation.",
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Đã chấp nhận lời mời. Vui lòng chờ Owner xác nhận chính thức.",
            status = invitation.Status
        });
    }

    [HttpPut("{id}/reject")]
    public async Task<IActionResult> RejectInvitation(int id)
    {
        var jockeyId = GetCurrentJockeyId();

        if (jockeyId == null)
        {
            return InvalidToken();
        }

        var profileError = await ValidateActiveJockeyAsync(jockeyId.Value);

        if (profileError != null)
        {
            return profileError;
        }

        var invitation = await LoadOwnedInvitationAsync(id, jockeyId.Value);

        if (invitation == null)
        {
            return NotFound(new { message = "Không tìm thấy lời mời." });
        }

        if (invitation.Status != InvitationStatuses.Pending)
        {
            return BadRequest(new
            {
                message = "Lời mời không còn ở trạng thái chờ.",
                status = invitation.Status
            });
        }

        invitation.Status = InvitationStatuses.Rejected;
        invitation.RespondedAt = DateTime.UtcNow;

        var jockeyName = invitation.Jockey.JockeyNavigation.FullName;
        var horseName = invitation.Registration.Horse.HorseName;

        _context.Notifications.Add(new Notification
        {
            UserId = invitation.InvitedByOwnerId,
            Title = "Invitation Rejected",
            Message = !string.IsNullOrWhiteSpace(jockeyName) &&
                !string.IsNullOrWhiteSpace(horseName)
                    ? $"{jockeyName} rejected invitation for {horseName}."
                    : "A jockey rejected your invitation.",
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Đã từ chối lời mời.",
            status = invitation.Status
        });
    }

    private int? GetCurrentJockeyId()
    {
        return _jockeyAccess.GetCurrentUserId(User);
    }

    private IActionResult InvalidToken()
    {
        return Unauthorized(new { message = "Token không hợp lệ." });
    }

    private async Task<IActionResult?> ValidateActiveJockeyAsync(int jockeyId)
    {
        if (_jockeyAccess != null)
        {
            var access = await _jockeyAccess.ValidateActiveJockeyAsync(User);
            return access.Succeeded ? null : AccessError(access);
        }

        var data = await _context.Jockeys
            .AsNoTracking()
            .Where(j => j.JockeyId == jockeyId)
            .Select(j => new
            {
                j.IsActive,
                j.JockeyNavigation.Role,
                UserStatus = j.JockeyNavigation.Status,
                j.JockeyNavigation.EmailVerified
            })
            .FirstOrDefaultAsync();

        if (data == null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Không tìm thấy hồ sơ jockey."
            });
        }

        if (data.Role != UserRoles.Jockey)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Tài khoản không có quyền Jockey."
            });
        }

        if (!data.EmailVerified)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Email chưa được xác thực.",
                nextStep = AuthNextSteps.VerifyEmail
            });
        }

        if (data.UserStatus == UserStatuses.Banned)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Tài khoản đã bị khóa.",
                nextStep = AuthNextSteps.AccountBlocked
            });
        }

        if (data.UserStatus == UserStatuses.Inactive)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Tài khoản đang bị vô hiệu hóa.",
                nextStep = AuthNextSteps.ContactSupport
            });
        }

        if (data.UserStatus != UserStatuses.Active || !data.IsActive)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Tài khoản jockey chưa được kích hoạt.",
                status = data.UserStatus,
                isActive = data.IsActive,
                nextStep = AuthNextSteps.WaitForActivation
            });
        }

        return null;
    }

    private IActionResult AccessError(JockeyAccessResult access)
    {
        if (access.StatusCode == StatusCodes.Status401Unauthorized)
        {
            return Unauthorized(new { message = access.Message });
        }

        return StatusCode(access.StatusCode, new
        {
            message = access.Message,
            nextStep = access.NextStep
        });
    }

    private async Task<JockeyInvitation?> LoadOwnedInvitationAsync(int invitationId, int jockeyId)
    {
        return await _context.JockeyInvitations
            .Include(i => i.Registration)
                .ThenInclude(r => r.Race)
            .Include(i => i.Registration)
                .ThenInclude(r => r.Horse)
            .Include(i => i.Jockey)
                .ThenInclude(j => j.JockeyNavigation)
            .FirstOrDefaultAsync(i => i.InvitationId == invitationId
                && i.JockeyId == jockeyId);
    }
}
