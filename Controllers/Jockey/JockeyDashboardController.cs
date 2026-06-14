using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Jockey;
using Eliteracingleague.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Controllers.Jockey;

[Route("api/jockey/dashboard")]
[ApiController]
[Authorize(Roles = UserRoles.Jockey)]
public class JockeyDashboardController : ControllerBase
{
    private static readonly string[] AcceptedRegistrationStatuses =
    {
        RaceRegistrationStatuses.ReadyToRace
    };

    private readonly EliteRacingLeagueContext _context;
    private readonly JockeyAccessService _jockeyAccess;

    public JockeyDashboardController(EliteRacingLeagueContext context, JockeyAccessService jockeyAccess)
    {
        _context = context;
        _jockeyAccess = jockeyAccess;
    }

    [HttpGet]
    public async Task<IActionResult> GetDashboard()
    {
        var access = await _jockeyAccess.ValidateActiveJockeyAsync(User);

        if (!access.Succeeded)
        {
            return AccessError(access);
        }

        var user = access.User!;
        var jockey = access.Jockey!;
        var jockeyId = jockey.JockeyId;

        var now = DateTime.UtcNow;

        var pendingInvitations = await _context.JockeyInvitations
            .AsNoTracking()
            .CountAsync(i => i.JockeyId == jockeyId
                && i.Status == InvitationStatuses.Pending);

        var acceptedRaces = await _context.RaceRegistrations
            .AsNoTracking()
            .CountAsync(r => r.JockeyId == jockeyId
                && AcceptedRegistrationStatuses.Contains(r.Status));

        var upcomingRaces = await _context.RaceRegistrations
            .AsNoTracking()
            .Where(r => r.JockeyId == jockeyId
                && AcceptedRegistrationStatuses.Contains(r.Status)
                && r.Race.RaceDate > now
                && r.Race.Status != RaceStatuses.Cancelled
                && r.Race.Status != RaceStatuses.Completed)
            .Select(r => r.RaceId)
            .Distinct()
            .CountAsync();

        var completedRaces = await _context.RaceRegistrations
            .AsNoTracking()
            .Where(r => r.JockeyId == jockeyId
                && (r.Status == RaceRegistrationStatuses.Completed
                    || r.Race.Status == RaceStatuses.Completed))
            .Select(r => r.RaceId)
            .Distinct()
            .CountAsync();

        return Ok(new JockeyDashboardResponse
        {
            PendingInvitations = pendingInvitations,
            AcceptedRaces = acceptedRaces,
            UpcomingRaces = upcomingRaces,
            CompletedRaces = completedRaces,
            ProfileStatus = user.Status,
            HealthStatus = jockey.HealthStatus
        });
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
}
