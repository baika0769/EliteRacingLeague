using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Owner;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Controllers.Owner;

[Route("api/owner/dashboard")]
[ApiController]
[Authorize(Roles = UserRoles.HorseOwner)]
public class OwnerDashboardController : OwnerBaseController
{
    private static readonly string[] ApprovedRegistrationStatuses =
    {
        RaceRegistrationStatuses.Approved,
        RaceRegistrationStatuses.JockeyInvited,
        RaceRegistrationStatuses.ReadyToRace
    };

    public OwnerDashboardController(EliteRacingLeagueContext context) : base(context)
    {
    }

    [HttpGet("overview")]
    public async Task<IActionResult> GetDashboardOverview()
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

        var totalHorse = await _context.Horses
            .CountAsync(h => h.OwnerId == ownerId.Value && h.IsActive);

        var registrations = await _context.RaceRegistrations
            .CountAsync(r =>
                r.OwnerId == ownerId.Value &&
                r.Status != RaceRegistrationStatuses.Cancelled &&
                r.Status != RaceRegistrationStatuses.Rejected &&
                r.Race.Status != RaceStatuses.Cancelled &&
                r.Race.Tournament.Status != TournamentStatuses.Cancelled);

        var pendingInvitations = await _context.JockeyInvitations
            .CountAsync(i =>
                i.InvitedByOwnerId == ownerId.Value &&
                i.Status == InvitationStatuses.Pending &&
                i.Registration.Race.Status != RaceStatuses.Cancelled &&
                i.Registration.Race.Tournament.Status != TournamentStatuses.Cancelled);

        var approvedRaces = await _context.RaceRegistrations
            .Where(r =>
                r.OwnerId == ownerId.Value &&
                ApprovedRegistrationStatuses.Contains(r.Status) &&
                r.Race.Status != RaceStatuses.Cancelled &&
                r.Race.Tournament.Status != TournamentStatuses.Cancelled)
            .Select(r => r.RaceId)
            .Distinct()
            .CountAsync();

        var response = new OwnerDashboardOverviewResponse
        {
            TotalHorse = totalHorse,
            Registrations = registrations,
            PendingInvitations = pendingInvitations,
            ApprovedRaces = approvedRaces
        };

        return Ok(response);
    }

    [HttpGet("approved-registrations")]
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
}