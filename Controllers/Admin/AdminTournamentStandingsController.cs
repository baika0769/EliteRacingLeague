using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Admin;
using Eliteracingleague.API.Extensions;
using Eliteracingleague.API.Services.Auditing;
using Eliteracingleague.API.Services.Racing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Controllers.Admin;

[ApiController]
[Route("api/admin/tournaments/{tournamentId:int}/standings")]
[Authorize(Roles = UserRoles.Admin)]
public class AdminTournamentStandingsController : ControllerBase
{
    private readonly EliteRacingLeagueContext _context;
    private readonly TournamentStandingService _service;
    private readonly IAuditService _audit;

    public AdminTournamentStandingsController(
        EliteRacingLeagueContext context,
        TournamentStandingService service,
        IAuditService audit)
    {
        _context = context;
        _service = service;
        _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> Get(int tournamentId, CancellationToken cancellationToken)
    {
        var rows = await _context.TournamentStandings.AsNoTracking()
            .Where(x => x.TournamentId == tournamentId)
            .OrderBy(x => x.FinalRank)
            .Select(x => new
            {
                x.FinalRank,
                x.HorseId,
                HorseName = x.Horse.HorseName,
                x.OwnerId,
                OwnerName = x.Owner.Owner.FullName,
                x.JockeyId,
                JockeyName = x.Jockey == null ? null : x.Jockey.JockeyNavigation.FullName,
                x.TotalPoints,
                x.Wins,
                x.SecondPlaces,
                x.ThirdPlaces,
                x.CompletedRaces,
                x.TotalFinishTimeSeconds,
                x.IsFinal,
                x.CalculatedAt,
                x.FinalizedAt
            })
            .ToListAsync(cancellationToken);
        return Ok(rows);
    }

    [HttpPost("recalculate")]
    public async Task<IActionResult> Recalculate(int tournamentId, CancellationToken cancellationToken)
    {
        var rows = await _service.RecalculateAsync(tournamentId, false, cancellationToken);
        return Ok(new { message = "Provisional standings recalculated.", count = rows.Count });
    }

    [HttpPost("finalize")]
    public async Task<IActionResult> Finalize(
        int tournamentId,
        FinalizeTournamentStandingsRequest request,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var adminId)) return Unauthorized();
        var rows = await _service.RecalculateAsync(tournamentId, true, cancellationToken);
        await _audit.WriteAsync(adminId, AuditActionTypes.Approve,
            "TournamentStanding", tournamentId.ToString(), null,
            new { Finalized = true, Count = rows.Count }, request.ConfirmationNote,
            cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Tournament standings finalized and tournament completed.", count = rows.Count });
    }
}
