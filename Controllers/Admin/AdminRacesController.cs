using System.Data;
using System.Text.Json;
using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Admin;
using Eliteracingleague.API.Extensions;
using Eliteracingleague.API.Models;
using Eliteracingleague.API.Services.Auditing;
using Eliteracingleague.API.Services.Notifications;
using Eliteracingleague.API.Services.Racing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Controllers.Admin;

[ApiController]
[Route("api/admin/races")]
[Authorize(Roles = UserRoles.Admin)]
public class AdminRacesController : ControllerBase
{
    private readonly EliteRacingLeagueContext _context;
    private readonly RaceSchedulingValidationService _validation;
    private readonly RacePredictionSettlementService _settlement;
    private readonly INotificationService _notifications;
    private readonly IAuditService _audit;

    public AdminRacesController(
        EliteRacingLeagueContext context,
        RaceSchedulingValidationService validation,
        RacePredictionSettlementService settlement,
        INotificationService notifications,
        IAuditService audit)
    {
        _context = context;
        _validation = validation;
        _settlement = settlement;
        _notifications = notifications;
        _audit = audit;
    }

    [HttpGet("tournament/{tournamentId:int}")]
    public async Task<IActionResult> GetTournamentRaces(int tournamentId, CancellationToken cancellationToken)
    {
        var races = await _context.Races.AsNoTracking()
            .Where(r => r.TournamentId == tournamentId)
            .OrderBy(r => r.RaceDate)
            .Select(r => new
            {
                Race = r,
                RegisteredCount = r.RaceRegistrations.Count(x =>
                    x.Status != RaceRegistrationStatuses.Rejected &&
                    x.Status != RaceRegistrationStatuses.Cancelled &&
                    x.Status != RaceRegistrationStatuses.Withdrawn),
                PrizeRuleCount = r.PrizeRules.Count,
                TotalPrizeAmount = r.PrizeRules.Sum(rule => (decimal?)rule.PrizeAmount) ?? 0m,
                HasGeneratedPrizeAwards = r.PrizeAwards.Any()
            })
            .ToListAsync(cancellationToken);
        return Ok(races.Select(x => new
        {
            x.Race.RaceId,
            x.Race.TournamentId,
            x.Race.RaceName,
            x.Race.RaceDate,
            x.Race.OriginalRaceDate,
            x.Race.DistanceMeters,
            x.Race.Location,
            x.Race.MaxHorses,
            x.Race.JockeySelectionDeadline,
            x.Race.PredictionDeadline,
            x.Race.Status,
            x.Race.PostponedAt,
            x.Race.PostponementReason,
            x.Race.CancelledAt,
            x.Race.CancellationReason,
            x.Race.LifecycleVersion,
            x.RegisteredCount,
            x.PrizeRuleCount,
            x.TotalPrizeAmount,
            x.HasGeneratedPrizeAwards,
            CanEditPrizeRules = x.Race.Status is RaceStatuses.Scheduled
                or RaceStatuses.AssignedReferee
                or RaceStatuses.RefereeReady
                or RaceStatuses.Postponed,
            RowVersion = Convert.ToBase64String(x.Race.RowVersion)
        }));
    }

    [HttpPost("tournament/{tournamentId:int}")]
    public async Task<IActionResult> CreateRace(
        int tournamentId,
        CreateRaceRequest request,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var adminId)) return Unauthorized();
        var tournament = await _context.Tournaments
            .Include(t => t.Season)
            .FirstOrDefaultAsync(t => t.TournamentId == tournamentId, cancellationToken);
        if (tournament == null) return NotFound(new { message = "Tournament not found." });
        if (tournament.Status is TournamentStatuses.Completed or TournamentStatuses.Cancelled)
            return BadRequest(new { message = "Completed or cancelled tournament cannot receive new races." });
        if (tournament.Season.Status is SeasonStatuses.Closed or SeasonStatuses.Cancelled or SeasonStatuses.Settling)
            return BadRequest(new { message = "The tournament season does not allow race creation." });

        var alreadyHasRace = await _context.Races
            .AsNoTracking()
            .AnyAsync(r => r.TournamentId == tournamentId, cancellationToken);

        if (alreadyHasRace)
        {
            return Conflict(new
            {
                code = "TOURNAMENT_ALREADY_HAS_RACE",
                message = "Each tournament can contain only one race. Edit the existing race instead of creating another one.",
                tournamentId
            });
        }

        await _validation.ValidateRaceDatesAsync(tournament, request.RaceDate,
            request.JockeySelectionDeadline, request.PredictionDeadline, null, cancellationToken);

        var now = DateTime.UtcNow;
        var race = new Race
        {
            TournamentId = tournamentId,
            RaceName = request.RaceName.Trim(),
            RaceDate = request.RaceDate,
            DistanceMeters = request.DistanceMeters,
            Location = string.IsNullOrWhiteSpace(request.Location) ? tournament.Location : request.Location.Trim(),
            MaxHorses = request.MaxHorses,
            JockeySelectionDeadline = request.JockeySelectionDeadline ?? request.RaceDate.AddDays(-1),
            PredictionDeadline = request.PredictionDeadline ?? request.RaceDate.AddHours(-1),
            Status = RaceStatuses.Scheduled,
            LifecycleVersion = 1,
            CreatedAt = now,
            UpdatedAt = now
        };
        _context.Races.Add(race);
        await _context.SaveChangesAsync(cancellationToken);

        if (request.RefereeId.HasValue)
        {
            await AssignRefereeAsync(race, request.RefereeId.Value, adminId, cancellationToken);
        }

        await _audit.WriteAsync(adminId, AuditActionTypes.Create, "Race",
            race.RaceId.ToString(), null, new
            {
                race.TournamentId, race.RaceName, race.RaceDate,
                race.DistanceMeters, race.MaxHorses, race.Status
            }, null, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new { message = "Race created.", race.RaceId, race.Status });
    }

    [HttpPut("{raceId:int}")]
    public async Task<IActionResult> UpdateRace(
        int raceId,
        UpdateRaceRequest request,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var adminId)) return Unauthorized();
        var race = await _context.Races
            .Include(r => r.Tournament)
            .FirstOrDefaultAsync(r => r.RaceId == raceId, cancellationToken);
        if (race == null) return NotFound(new { message = "Race not found." });
        if (race.Status is RaceStatuses.Ongoing or RaceStatuses.Finished or RaceStatuses.ResultPending or RaceStatuses.Published or RaceStatuses.Cancelled)
            return BadRequest(new { message = "This race can no longer be edited." });

        _context.Entry(race).Property(x => x.RowVersion).OriginalValue = request.RowVersion;
        var hasRegistrations = await _context.RaceRegistrations.AnyAsync(r =>
            r.RaceId == raceId && r.Status != RaceRegistrationStatuses.Rejected &&
            r.Status != RaceRegistrationStatuses.Cancelled && r.Status != RaceRegistrationStatuses.Withdrawn,
            cancellationToken);
        if (hasRegistrations && (race.RaceDate != request.RaceDate ||
                                 race.DistanceMeters != request.DistanceMeters ||
                                 race.MaxHorses != request.MaxHorses))
            return BadRequest(new { message = "Use the postpone flow for date changes. Distance and capacity cannot change after registrations exist." });

        await _validation.ValidateRaceDatesAsync(race.Tournament, request.RaceDate,
            request.JockeySelectionDeadline, request.PredictionDeadline, raceId, cancellationToken);

        var old = new { race.RaceName, race.RaceDate, race.DistanceMeters, race.Location, race.MaxHorses, race.JockeySelectionDeadline, race.PredictionDeadline };
        race.RaceName = request.RaceName.Trim();
        race.RaceDate = request.RaceDate;
        race.DistanceMeters = request.DistanceMeters;
        race.Location = string.IsNullOrWhiteSpace(request.Location) ? race.Tournament.Location : request.Location.Trim();
        race.MaxHorses = request.MaxHorses;
        race.JockeySelectionDeadline = request.JockeySelectionDeadline;
        race.PredictionDeadline = request.PredictionDeadline;
        race.LifecycleVersion++;
        race.UpdatedAt = DateTime.UtcNow;

        await _audit.WriteAsync(adminId, AuditActionTypes.Update, "Race", raceId.ToString(), old,
            new { race.RaceName, race.RaceDate, race.DistanceMeters, race.Location, race.MaxHorses, race.JockeySelectionDeadline, race.PredictionDeadline },
            null, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Race updated.", race.RaceId, race.Status, RowVersion = Convert.ToBase64String(race.RowVersion) });
    }

    [HttpPost("{raceId:int}/postpone")]
    public async Task<IActionResult> PostponeRace(
        int raceId,
        PostponeRaceRequest request,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var adminId)) return Unauthorized();
        var race = await _context.Races.Include(r => r.Tournament)
            .FirstOrDefaultAsync(r => r.RaceId == raceId, cancellationToken);
        if (race == null) return NotFound(new { message = "Race not found." });
        if (race.Status is RaceStatuses.Ongoing or RaceStatuses.Finished or RaceStatuses.ResultPending or RaceStatuses.Published or RaceStatuses.Cancelled)
            return BadRequest(new { message = "The race can no longer be postponed." });
        if (request.NewRaceDate <= DateTime.UtcNow)
            return BadRequest(new { message = "New race date must be in the future." });

        var predictionDeadline = request.NewPredictionDeadline ?? request.NewRaceDate.AddHours(-1);
        var jockeyDeadline = request.NewJockeySelectionDeadline ?? request.NewRaceDate.AddDays(-1);
        await _validation.ValidateRaceDatesAsync(race.Tournament, request.NewRaceDate,
            jockeyDeadline, predictionDeadline, raceId, cancellationToken);

        var oldDate = race.RaceDate;
        race.OriginalRaceDate ??= oldDate;
        race.RaceDate = request.NewRaceDate;
        race.PredictionDeadline = predictionDeadline;
        race.JockeySelectionDeadline = jockeyDeadline;
        race.PostponedAt = DateTime.UtcNow;
        race.PostponementReason = request.Reason.Trim();
        race.Status = RaceStatuses.Postponed;
        race.LifecycleVersion++;
        race.UpdatedAt = DateTime.UtcNow;

        await NotifyRaceParticipantsAsync(raceId, "Race Postponed",
            $"{race.RaceName} was postponed from {oldDate:g} to {race.RaceDate:g}. Reason: {request.Reason.Trim()}",
            "RacePostponed", cancellationToken);
        await _audit.WriteAsync(adminId, AuditActionTypes.Postpone, "Race", raceId.ToString(),
            new { RaceDate = oldDate }, new { race.RaceDate, race.Status }, request.Reason, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Race postponed. Use /resume after confirming the new schedule.", race.RaceDate, race.Status });
    }

    [HttpPost("{raceId:int}/resume")]
    public async Task<IActionResult> ResumeRace(int raceId, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var adminId)) return Unauthorized();
        var race = await _context.Races
            .Include(r => r.RefereeAssignments)
            .Include(r => r.Tournament)
            .FirstOrDefaultAsync(r => r.RaceId == raceId, cancellationToken);
        if (race == null) return NotFound(new { message = "Race not found." });
        if (race.Status != RaceStatuses.Postponed)
            return BadRequest(new { message = "Only a postponed race can be resumed." });
        if (race.RaceDate <= DateTime.UtcNow)
            return BadRequest(new { message = "Race date must be moved to the future before resuming." });

        var registrationIsClosed =
            race.Tournament.Status == TournamentStatuses.ClosedRegistration ||
            race.Tournament.Status == TournamentStatuses.Ongoing;
        var hasActiveRefereeAssignment = race.RefereeAssignments.Any(x =>
            x.Status == RefereeAssignmentStatuses.Assigned);

        race.Status = registrationIsClosed && hasActiveRefereeAssignment
            ? RaceStatuses.AssignedReferee
            : RaceStatuses.Scheduled;
        race.LifecycleVersion++;
        race.UpdatedAt = DateTime.UtcNow;
        await NotifyRaceParticipantsAsync(raceId, "Race Schedule Confirmed",
            $"The new schedule for {race.RaceName} is confirmed at {race.RaceDate:g}.",
            "RaceResumed", cancellationToken);
        await _audit.WriteAsync(adminId, AuditActionTypes.StatusChange, "Race", raceId.ToString(),
            new { Status = RaceStatuses.Postponed }, new { race.Status }, "Resume postponed race", cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Race resumed.", race.Status, race.RaceDate });
    }

    [HttpPost("{raceId:int}/cancel")]
    public async Task<IActionResult> CancelRace(
        int raceId,
        CancelRaceRequest request,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var adminId)) return Unauthorized();
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var race = await _context.Races
            .Include(r => r.Tournament).ThenInclude(t => t.Season)
            .Include(r => r.RaceRegistrations).ThenInclude(r => r.JockeyInvitations)
            .Include(r => r.RaceResults)
            .Include(r => r.PrizeAwards).ThenInclude(a => a.Payouts)
            .FirstOrDefaultAsync(r => r.RaceId == raceId, cancellationToken);
        if (race == null) return NotFound(new { message = "Race not found." });
        if (race.Status == RaceStatuses.Cancelled)
            return Ok(new { message = "Race was already cancelled.", race.Status });
        if (race.Tournament.Season.Status != SeasonStatuses.Active)
            return BadRequest(new { message = "Race cannot be cancelled after season settlement starts." });
        if (race.PrizeAwards.SelectMany(x => x.Payouts)
            .Any(x => x.Status is PrizeAwardStatuses.UnderReview or PrizeAwardStatuses.Paid))
            return BadRequest(new { message = "Resolve claimed or paid owner/jockey payouts before cancelling this race." });

        var oldStatus = race.Status;
        var settlement = await _settlement.RefundForCancelledRaceAsync(raceId, request.Reason, cancellationToken);
        var now = DateTime.UtcNow;

        foreach (var result in race.RaceResults)
        {
            _context.RaceResultRevisions.Add(new RaceResultRevision
            {
                RaceId = raceId,
                ResultId = result.ResultId,
                RegistrationId = result.RegistrationId,
                VersionNumber = result.RevisionNumber + 1,
                ChangeType = AuditActionTypes.Cancel,
                SnapshotJson = JsonSerializer.Serialize(new { result.FinishPosition, result.FinishTimeSeconds, result.OutcomeStatus, result.Status }),
                Reason = request.Reason.Trim(),
                ChangedByUserId = adminId,
                CreatedAt = now
            });
            result.Status = RaceResultStatuses.Returned;
            result.PublishedAt = null;
            result.AdminConfirmedBy = null;
            result.RevisionNumber++;
            result.UpdatedAt = now;
        }

        _context.PrizeAwards.RemoveRange(race.PrizeAwards);
        foreach (var registration in race.RaceRegistrations.Where(x =>
                     x.Status != RaceRegistrationStatuses.Rejected &&
                     x.Status != RaceRegistrationStatuses.Withdrawn))
        {
            registration.Status = RaceRegistrationStatuses.Cancelled;
            registration.AdminNote = request.Reason.Trim();
            foreach (var invitation in registration.JockeyInvitations.Where(x => x.Status == InvitationStatuses.Pending))
            {
                invitation.Status = InvitationStatuses.Cancelled;
                invitation.RespondedAt = now;
            }
        }

        race.Status = RaceStatuses.Cancelled;
        race.CancelledAt = now;
        race.CancellationReason = request.Reason.Trim();
        race.LifecycleVersion++;
        race.UpdatedAt = now;

        var standings = await _context.TournamentStandings.Where(x => x.TournamentId == race.TournamentId).ToListAsync(cancellationToken);
        _context.TournamentStandings.RemoveRange(standings);
        var otherRaces = await _context.Races.AsNoTracking().Where(x => x.TournamentId == race.TournamentId && x.RaceId != raceId).ToListAsync(cancellationToken);
        if (otherRaces.Count > 0 && otherRaces.All(x => x.Status == RaceStatuses.Cancelled))
            race.Tournament.Status = TournamentStatuses.Cancelled;
        else if (race.Tournament.Status == TournamentStatuses.Completed)
            race.Tournament.Status = TournamentStatuses.Ongoing;
        race.Tournament.UpdatedAt = now;

        await NotifyRaceParticipantsAsync(raceId, "Race Cancelled",
            $"{race.RaceName} was cancelled. Prediction stakes were refunded. Reason: {request.Reason.Trim()}",
            "RaceCancelled", cancellationToken);
        await _audit.WriteAsync(adminId, AuditActionTypes.Cancel, "Race", raceId.ToString(),
            new { Status = oldStatus }, new { race.Status, settlement.PredictionsAffected, settlement.StakePointsRefunded, settlement.PayoutPointsReversed },
            request.Reason, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Ok(new
        {
            message = "Race cancelled and prediction points settled.",
            race.Status,
            settlement.PredictionsAffected,
            settlement.StakePointsRefunded,
            settlement.PayoutPointsReversed
        });
    }

    private async Task AssignRefereeAsync(Race race, int refereeId, int adminId, CancellationToken cancellationToken)
    {
        var eligible = await _context.RaceReferees.AsNoTracking().AnyAsync(r =>
            r.RefereeId == refereeId && r.IsActive &&
            r.Referee.Role == UserRoles.RaceReferee && r.Referee.Status == UserStatuses.Active,
            cancellationToken);
        if (!eligible) throw new InvalidOperationException("Selected referee is not active.");
        var conflict = await _context.RefereeAssignments.AsNoTracking().AnyAsync(a =>
            a.RefereeId == refereeId && a.Status == RefereeAssignmentStatuses.Assigned &&
            a.Race.Status != RaceStatuses.Cancelled && a.Race.RaceDate == race.RaceDate,
            cancellationToken);
        if (conflict) throw new InvalidOperationException("Referee already has a race at the same time.");

        _context.RefereeAssignments.Add(new RefereeAssignment
        {
            RaceId = race.RaceId,
            RefereeId = refereeId,
            AssignedBy = adminId,
            Status = RefereeAssignmentStatuses.Assigned,
            AssignedAt = DateTime.UtcNow
        });
        var tournamentStatus = race.Tournament is not null
            ? race.Tournament.Status
            : await _context.Tournaments
                .Where(t => t.TournamentId == race.TournamentId)
                .Select(t => t.Status)
                .FirstAsync(cancellationToken);

        if (race.Status == RaceStatuses.Scheduled &&
            (tournamentStatus == TournamentStatuses.ClosedRegistration ||
             tournamentStatus == TournamentStatuses.Ongoing))
        {
            race.Status = RaceStatuses.AssignedReferee;
        }
        else if (race.Status == RaceStatuses.AssignedReferee &&
                 tournamentStatus != TournamentStatuses.ClosedRegistration &&
                 tournamentStatus != TournamentStatuses.Ongoing)
        {
            race.Status = RaceStatuses.Scheduled;
        }

        await _notifications.CreateForUserAsync(refereeId, "Race Assigned",
            $"You were assigned to {race.RaceName}.", "RefereeRaceAssignment",
            "/referee/races", "Race", race.RaceId, cancellationToken);
    }

    private async Task NotifyRaceParticipantsAsync(
        int raceId,
        string title,
        string message,
        string actionType,
        CancellationToken cancellationToken)
    {
        var registrations = await _context.RaceRegistrations.AsNoTracking()
            .Where(r => r.RaceId == raceId)
            .Select(r => new { r.OwnerId, r.JockeyId })
            .ToListAsync(cancellationToken);
        var referees = await _context.RefereeAssignments.AsNoTracking()
            .Where(a => a.RaceId == raceId)
            .Select(a => a.RefereeId)
            .ToListAsync(cancellationToken);
        var spectators = await _context.RacePredictions.AsNoTracking()
            .Where(p => p.RaceId == raceId)
            .Select(p => p.SpectatorId)
            .ToListAsync(cancellationToken);
        var userIds = registrations.Select(x => x.OwnerId)
            .Concat(registrations.Where(x => x.JockeyId.HasValue).Select(x => x.JockeyId!.Value))
            .Concat(referees).Concat(spectators).Distinct().ToList();
        foreach (var userId in userIds)
            await _notifications.CreateForUserAsync(userId, title, message,
                actionType, "/", "Race", raceId, cancellationToken);
    }
}
