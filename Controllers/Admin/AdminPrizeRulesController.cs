using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Admin;
using Eliteracingleague.API.Extensions;
using Eliteracingleague.API.Models;
using Eliteracingleague.API.Services.Auditing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Controllers.Admin;

[ApiController]
[Route("api/admin/races/{raceId:int}/prize-rules")]
[Authorize(Roles = UserRoles.Admin)]
public sealed class AdminPrizeRulesController : ControllerBase
{
    private const int MaxRulesPerRace = 100;
    private const decimal MaxPrizeAmount = 999_999_999_999.99m;

    private readonly EliteRacingLeagueContext _context;
    private readonly IAuditService _auditService;

    public AdminPrizeRulesController(
        EliteRacingLeagueContext context,
        IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    [HttpGet]
    public async Task<IActionResult> GetPrizeRules(
        int raceId,
        CancellationToken cancellationToken)
    {
        var race = await _context.Races
            .AsNoTracking()
            .Where(r => r.RaceId == raceId)
            .Select(r => new
            {
                r.RaceId,
                r.RaceName,
                r.Status,
                r.MaxHorses,
                r.TournamentId,
                TournamentName = r.Tournament.TournamentName,
                TournamentPrizePool = r.Tournament.PrizePool,
                HasGeneratedAwards = r.PrizeAwards.Any(),
                Rules = r.PrizeRules
                    .OrderBy(rule => rule.RankPosition)
                    .Select(rule => new
                    {
                        rule.PrizeRuleId,
                        rule.RaceId,
                        rule.RankPosition,
                        rule.PrizeAmount,
                        rule.Note
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (race == null)
            return NotFound(new { message = "Race not found.", raceId });

        var tournamentAllocated = await _context.PrizeRules
            .AsNoTracking()
            .Where(rule => rule.Race.TournamentId == race.TournamentId)
            .SumAsync(rule => (decimal?)rule.PrizeAmount, cancellationToken) ?? 0m;

        var raceAllocated = race.Rules.Sum(rule => rule.PrizeAmount);
        var remainingPool = race.TournamentPrizePool.HasValue
            ? race.TournamentPrizePool.Value - tournamentAllocated
            : (decimal?)null;

        return Ok(new
        {
            race.RaceId,
            race.RaceName,
            raceStatus = race.Status,
            race.MaxHorses,
            race.TournamentId,
            race.TournamentName,
            race.TournamentPrizePool,
            raceAllocatedPrize = raceAllocated,
            tournamentAllocatedPrize = tournamentAllocated,
            remainingTournamentPrizePool = remainingPool,
            canEdit = CanEdit(race.Status) && !race.HasGeneratedAwards,
            race.HasGeneratedAwards,
            rules = race.Rules
        });
    }

    [HttpPut]
    public async Task<IActionResult> ReplacePrizeRules(
        int raceId,
        [FromBody] UpsertPrizeRulesRequest request,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var adminId))
            return Unauthorized();

        var race = await _context.Races
            .Include(r => r.Tournament)
            .Include(r => r.PrizeRules)
            .Include(r => r.PrizeAwards)
            .FirstOrDefaultAsync(r => r.RaceId == raceId, cancellationToken);

        if (race == null)
            return NotFound(new { message = "Race not found.", raceId });

        if (!CanEdit(race.Status))
        {
            return BadRequest(new
            {
                message = "Prize rules can only be edited before the race starts.",
                raceId,
                raceStatus = race.Status
            });
        }

        if (race.PrizeAwards.Count > 0)
        {
            return BadRequest(new
            {
                message = "Prize rules cannot be changed after prize awards have been generated.",
                raceId,
                generatedAwardCount = race.PrizeAwards.Count
            });
        }

        request.Rules ??= new List<PrizeRuleItemRequest>();

        if (request.Rules.Count > MaxRulesPerRace || request.Rules.Count > race.MaxHorses)
        {
            return BadRequest(new
            {
                message = $"A race can have at most {Math.Min(MaxRulesPerRace, race.MaxHorses)} prize rules.",
                raceId,
                race.MaxHorses
            });
        }

        var duplicateRank = request.Rules
            .GroupBy(rule => rule.RankPosition)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateRank != null)
        {
            return BadRequest(new
            {
                message = $"Duplicate prize rule for rank {duplicateRank.Key}.",
                rankPosition = duplicateRank.Key
            });
        }

        var orderedRules = request.Rules
            .OrderBy(rule => rule.RankPosition)
            .ToList();

        for (var index = 0; index < orderedRules.Count; index++)
        {
            var rule = orderedRules[index];
            var expectedRank = index + 1;

            if (rule.RankPosition != expectedRank)
            {
                return BadRequest(new
                {
                    message = $"Prize ranks must be consecutive starting at 1. Expected rank {expectedRank}.",
                    expectedRank,
                    receivedRank = rule.RankPosition
                });
            }

            if (rule.PrizeAmount <= 0 || rule.PrizeAmount > MaxPrizeAmount)
            {
                return BadRequest(new
                {
                    message = $"Prize amount for rank {rule.RankPosition} must be greater than 0 and no more than {MaxPrizeAmount:N2}.",
                    rule.RankPosition,
                    rule.PrizeAmount
                });
            }

            if (!string.IsNullOrWhiteSpace(rule.Note) && rule.Note.Trim().Length > 255)
            {
                return BadRequest(new
                {
                    message = $"Prize note for rank {rule.RankPosition} cannot exceed 255 characters.",
                    rule.RankPosition
                });
            }
        }

        var requestedTotal = orderedRules.Sum(rule => rule.PrizeAmount);
        var otherRaceAllocated = await _context.PrizeRules
            .AsNoTracking()
            .Where(rule => rule.Race.TournamentId == race.TournamentId && rule.RaceId != raceId)
            .SumAsync(rule => (decimal?)rule.PrizeAmount, cancellationToken) ?? 0m;

        if (race.Tournament.PrizePool.HasValue &&
            otherRaceAllocated + requestedTotal > race.Tournament.PrizePool.Value)
        {
            return BadRequest(new
            {
                message = "The total prize rules exceed the tournament prize pool.",
                tournamentPrizePool = race.Tournament.PrizePool.Value,
                allocatedToOtherRaces = otherRaceAllocated,
                requestedForThisRace = requestedTotal,
                exceededBy = otherRaceAllocated + requestedTotal - race.Tournament.PrizePool.Value
            });
        }

        var oldRules = race.PrizeRules
            .OrderBy(rule => rule.RankPosition)
            .Select(rule => new
            {
                rule.PrizeRuleId,
                rule.RankPosition,
                rule.PrizeAmount,
                rule.Note
            })
            .ToArray();

        var incomingRanks = orderedRules
            .Select(rule => rule.RankPosition)
            .ToHashSet();

        var removedRules = race.PrizeRules
            .Where(rule => !incomingRanks.Contains(rule.RankPosition))
            .ToList();
        _context.PrizeRules.RemoveRange(removedRules);

        foreach (var requestRule in orderedRules)
        {
            var existing = race.PrizeRules
                .FirstOrDefault(rule => rule.RankPosition == requestRule.RankPosition);

            if (existing == null)
            {
                _context.PrizeRules.Add(new PrizeRule
                {
                    RaceId = race.RaceId,
                    RankPosition = requestRule.RankPosition,
                    PrizeAmount = requestRule.PrizeAmount,
                    Note = NormalizeNote(requestRule.Note)
                });
            }
            else
            {
                existing.PrizeAmount = requestRule.PrizeAmount;
                existing.Note = NormalizeNote(requestRule.Note);
            }
        }

        race.LifecycleVersion++;
        race.UpdatedAt = DateTime.UtcNow;

        await _auditService.WriteAsync(
            adminId,
            AuditActionTypes.Update,
            "PrizeRules",
            race.RaceId.ToString(),
            oldRules,
            orderedRules.Select(rule => new
            {
                rule.RankPosition,
                rule.PrizeAmount,
                Note = NormalizeNote(rule.Note)
            }).ToArray(),
            "Admin replaced the race prize rules.",
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        var savedRules = await _context.PrizeRules
            .AsNoTracking()
            .Where(rule => rule.RaceId == raceId)
            .OrderBy(rule => rule.RankPosition)
            .Select(rule => new
            {
                rule.PrizeRuleId,
                rule.RaceId,
                rule.RankPosition,
                rule.PrizeAmount,
                rule.Note
            })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            message = savedRules.Count == 0
                ? "All prize rules were removed from the race."
                : "Prize rules updated successfully.",
            raceId,
            raceName = race.RaceName,
            totalPrizeAmount = savedRules.Sum(rule => rule.PrizeAmount),
            ruleCount = savedRules.Count,
            rules = savedRules
        });
    }

    private static bool CanEdit(string status)
    {
        return status is RaceStatuses.Scheduled
            or RaceStatuses.AssignedReferee
            or RaceStatuses.RefereeReady
            or RaceStatuses.Postponed;
    }

    private static string? NormalizeNote(string? note)
    {
        return string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }
}
