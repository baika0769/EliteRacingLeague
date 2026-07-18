using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Admin;
using Eliteracingleague.API.Extensions;
using Eliteracingleague.API.Services.Auditing;
using Eliteracingleague.API.Services.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Controllers.Owner;

[ApiController]
[Route("api/owner/registrations")]
[Authorize(Roles = UserRoles.HorseOwner)]
public class OwnerRegistrationLifecycleController : ControllerBase
{
    private readonly EliteRacingLeagueContext _context;
    private readonly INotificationService _notifications;
    private readonly IAuditService _audit;

    public OwnerRegistrationLifecycleController(
        EliteRacingLeagueContext context,
        INotificationService notifications,
        IAuditService audit)
    {
        _context = context;
        _notifications = notifications;
        _audit = audit;
    }

    [HttpPost("{registrationId:int}/withdraw")]
    public async Task<IActionResult> Withdraw(
        int registrationId,
        WithdrawRegistrationRequest request,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var ownerId)) return Unauthorized();

        var registration = await _context.RaceRegistrations
            .Include(r => r.Race)
            .Include(r => r.JockeyInvitations)
            .FirstOrDefaultAsync(r => r.RegistrationId == registrationId && r.OwnerId == ownerId, cancellationToken);
        if (registration == null) return NotFound(new { message = "Registration not found." });

        if (registration.Status is RaceRegistrationStatuses.Rejected or RaceRegistrationStatuses.Cancelled or
            RaceRegistrationStatuses.Withdrawn or RaceRegistrationStatuses.Completed)
            return Conflict(new { message = "Registration can no longer be withdrawn.", registration.Status });

        if (registration.Race.Status is RaceStatuses.Ongoing or RaceStatuses.Finished or RaceStatuses.ResultPending or
            RaceStatuses.Published or RaceStatuses.Cancelled)
            return Conflict(new { message = "Registration cannot be withdrawn after the race starts." });

        var oldStatus = registration.Status;
        var now = DateTime.UtcNow;
        registration.Status = RaceRegistrationStatuses.Withdrawn;
        registration.WithdrawalReason = request.Reason.Trim();
        registration.WithdrawnAt = now;
        registration.WithdrawnByUserId = ownerId;
        registration.JockeyId = null;
        registration.JockeyConfirmedAt = null;

        foreach (var invitation in registration.JockeyInvitations.Where(i => i.Status == InvitationStatuses.Pending))
        {
            invitation.Status = InvitationStatuses.Cancelled;
            invitation.RespondedAt = now;
            invitation.ResponseNote = "Registration withdrawn by owner.";
        }

        await _notifications.CreateForAdminsAsync(
            "Race Registration Withdrawn",
            $"Owner withdrew registration #{registrationId}. Reason: {request.Reason.Trim()}",
            "RaceRegistrationWithdrawn",
            "/admin/registrations",
            "RaceRegistration",
            registrationId,
            cancellationToken);
        await _audit.WriteAsync(ownerId, AuditActionTypes.StatusChange, "RaceRegistration", registrationId.ToString(),
            new { Status = oldStatus }, new { registration.Status }, request.Reason, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new { message = "Registration withdrawn.", registrationId, registration.Status });
    }
}
