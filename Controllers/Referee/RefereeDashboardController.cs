using System.Security.Claims;
using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Controllers.Referee;

[Authorize(Roles = UserRoles.RaceReferee)]
[ApiController]
[Route("api/referee/dashboard")]
public class RefereeDashboardController : ControllerBase
{
    private readonly EliteRacingLeagueContext _context;

    public RefereeDashboardController(EliteRacingLeagueContext context)
    {
        _context = context;
    }

    private int GetRefereeId()
    {
        return int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    }

    [HttpGet]
    public async Task<IActionResult> GetDashboard()
    {
        var refereeId = GetRefereeId();

        var assignedRaceIds = await _context.RefereeAssignments
            .Where(a => a.RefereeId == refereeId &&
                        a.Status == RefereeAssignmentStatuses.Assigned)
            .Select(a => a.RaceId)
            .ToListAsync();

        var assignedRaces = assignedRaceIds.Count;

        var pendingInspections = await _context.RaceRegistrations
            .Where(r => assignedRaceIds.Contains(r.RaceId))
            .CountAsync(r =>
                !_context.PreRaceInspections.Any(i =>
                    i.RaceId == r.RaceId &&
                    i.RegistrationId == r.RegistrationId));

        var passedInspections = await _context.PreRaceInspections
            .CountAsync(i =>
                assignedRaceIds.Contains(i.RaceId) &&
                i.RefereeId == refereeId &&
                i.Status == PreRaceInspectionStatuses.Passed);

        var failedInspections = await _context.PreRaceInspections
            .CountAsync(i =>
                assignedRaceIds.Contains(i.RaceId) &&
                i.RefereeId == refereeId &&
                i.Status == PreRaceInspectionStatuses.Failed);

        var submittedResults = await _context.RaceResults
            .CountAsync(r =>
                assignedRaceIds.Contains(r.RaceId) &&
                r.EnteredByRefereeId == refereeId);

        var confirmedResults = await _context.RaceResults
            .CountAsync(r =>
                assignedRaceIds.Contains(r.RaceId) &&
                r.EnteredByRefereeId == refereeId &&
                r.Status == RaceResultStatuses.RefereeConfirmed);

        var violationReports = await _context.RaceViolations
            .CountAsync(v =>
                assignedRaceIds.Contains(v.RaceId) &&
                v.RefereeId == refereeId);

        var submittedReports = await _context.RefereeReports
            .CountAsync(r =>
                assignedRaceIds.Contains(r.RaceId) &&
                r.RefereeId == refereeId);

        var upcomingRaces = await _context.RefereeAssignments
            .Where(a => a.RefereeId == refereeId &&
                        a.Status == RefereeAssignmentStatuses.Assigned)
            .OrderBy(a => a.Race.RaceDate)
            .Take(5)
            .Select(a => new
            {
                assignmentId = a.RefereeAssignmentId,
                raceId = a.RaceId,
                raceName = a.Race.RaceName,
                tournamentName = a.Race.Tournament.TournamentName,
                raceDate = a.Race.RaceDate,
                distanceMeters = a.Race.DistanceMeters,
                location = a.Race.Location,
                raceStatus = a.Race.Status,
                assignedAt = a.AssignedAt
            })
            .ToListAsync();

        var recentViolations = await _context.RaceViolations
            .Where(v => assignedRaceIds.Contains(v.RaceId) &&
                        v.RefereeId == refereeId)
            .OrderByDescending(v => v.CreatedAt)
            .Take(5)
            .Select(v => new
            {
                violationId = v.ViolationId,
                raceId = v.RaceId,
                raceName = v.Race.RaceName,
                horseName = v.Registration.Horse.HorseName,
                violationType = v.ViolationType,
                action = v.Action,
                penaltyPoints = v.PenaltyPoints,
                createdAt = v.CreatedAt
            })
            .ToListAsync();

        return Ok(new
        {
            summary = new
            {
                assignedRaces,
                pendingInspections,
                passedInspections,
                failedInspections,
                submittedResults,
                confirmedResults,
                violationReports,
                submittedReports
            },
            upcomingRaces,
            recentViolations
        });
    }
}