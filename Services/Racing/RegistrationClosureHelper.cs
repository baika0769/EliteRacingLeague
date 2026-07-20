using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Services.Racing;

public sealed record RegistrationClosureResult(
    int ExpiredInvitations,
    int CancelledAcceptedInvitations,
    int CancelledRegistrations)
{
    public bool HasChanges =>
        ExpiredInvitations > 0 ||
        CancelledAcceptedInvitations > 0 ||
        CancelledRegistrations > 0;
}

public static class RegistrationClosureHelper
{
    private static readonly string[] IncompleteRegistrationStatuses =
    {
        RaceRegistrationStatuses.Pending,
        RaceRegistrationStatuses.Approved,
        RaceRegistrationStatuses.JockeyInvited,
        RaceRegistrationStatuses.ReadyToRace
    };

    public static DateTime GetRegistrationCloseLocal(DateOnly registrationDeadline)
    {
        return registrationDeadline.ToDateTime(TimeOnly.MaxValue);
    }

    public static bool IsRegistrationClosed(Tournament tournament, DateTime localNow)
    {
        if (tournament.Status is
            TournamentStatuses.ClosedRegistration or
            TournamentStatuses.Ongoing or
            TournamentStatuses.Completed or
            TournamentStatuses.Cancelled)
        {
            return true;
        }

        return localNow > GetRegistrationCloseLocal(tournament.StartDate);
    }

    public static async Task<RegistrationClosureResult> ApplyAsync(
        EliteRacingLeagueContext context,
        IEnumerable<int> tournamentIds,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var ids = tournamentIds
            .Where(id => id > 0)
            .Distinct()
            .ToArray();

        if (ids.Length == 0)
        {
            return new RegistrationClosureResult(0, 0, 0);
        }

        var activeInvitations = await context.JockeyInvitations
            .Where(invitation =>
                ids.Contains(invitation.Registration.Race.TournamentId) &&
                (invitation.Status == InvitationStatuses.Pending ||
                 invitation.Status == InvitationStatuses.Accepted) &&
                invitation.Registration.JockeyId == null)
            .ToListAsync(cancellationToken);

        var expiredInvitations = 0;
        var cancelledAcceptedInvitations = 0;

        foreach (var invitation in activeInvitations)
        {
            if (invitation.Status == InvitationStatuses.Pending)
            {
                invitation.Status = InvitationStatuses.Expired;
                invitation.ResponseNote =
                    "Invitation expired because tournament registration closed.";
                expiredInvitations++;
            }
            else
            {
                invitation.Status = InvitationStatuses.Cancelled;
                invitation.ResponseNote =
                    "Accepted invitation was cancelled because no official jockey was selected before registration closed.";
                cancelledAcceptedInvitations++;
            }

            invitation.RespondedAt = utcNow;
        }

        var incompleteRegistrations = await context.RaceRegistrations
            .Where(registration =>
                ids.Contains(registration.Race.TournamentId) &&
                registration.JockeyId == null &&
                IncompleteRegistrationStatuses.Contains(registration.Status))
            .ToListAsync(cancellationToken);

        foreach (var registration in incompleteRegistrations)
        {
            registration.Status = RaceRegistrationStatuses.Cancelled;
            registration.AdminNote =
                "Cancelled automatically because registration closed before an official jockey was confirmed.";
        }

        return new RegistrationClosureResult(
            expiredInvitations,
            cancelledAcceptedInvitations,
            incompleteRegistrations.Count);
    }
}
