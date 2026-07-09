using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Controllers.Admin;

[Authorize(Roles = UserRoles.Admin)]
[ApiController]
[Route("api/admin/rewards")]
public class AdminRewardsController : ControllerBase
{
    private readonly EliteRacingLeagueContext _context;

    public AdminRewardsController(EliteRacingLeagueContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetRewards([FromQuery] string? status = null)
    {
        var query = _context.PrizeAwards
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!PrizeAwardStatuses.IsValid(status))
            {
                return BadRequest(new { message = "Invalid prize award status.", allowedValues = PrizeAwardStatuses.All });
            }

            query = query.Where(a => a.Status == status);
        }

        var rewards = await query
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new
            {
                prizeAwardId = a.PrizeAwardId,
                raceId = a.RaceId,
                raceName = a.Race.RaceName,
                tournamentId = a.Race.TournamentId,
                tournamentName = a.Race.Tournament.TournamentName,
                ownerId = a.OwnerId,
                ownerName = a.Owner.Owner.FullName,
                jockeyId = a.JockeyId,
                jockeyName = a.Jockey == null ? null : a.Jockey.JockeyNavigation.FullName,
                horseId = a.Registration.HorseId,
                horseName = a.Registration.Horse.HorseName,
                rankPosition = a.RankPosition,
                prizeAmount = a.PrizeAmount,
                status = a.Status,
                paidAt = a.PaidAt,
                createdAt = a.CreatedAt
            })
            .ToListAsync();

        return Ok(rewards);
    }

    [HttpPut("{id:int}/approve-payment")]
    public async Task<IActionResult> ApprovePayment(int id)
    {
        var reward = await _context.PrizeAwards
            .Include(a => a.Owner)
                .ThenInclude(o => o.Owner)
            .Include(a => a.Race)
                .ThenInclude(r => r.Tournament)
            .FirstOrDefaultAsync(a => a.PrizeAwardId == id);

        if (reward == null)
        {
            return NotFound(new { message = "Reward not found.", id });
        }

        if (reward.Status != PrizeAwardStatuses.UnderReview)
        {
            return BadRequest(new
            {
                message = "Only rewards under review can be marked as paid.",
                id,
                status = reward.Status
            });
        }

        var now = DateTime.UtcNow;
        reward.Status = PrizeAwardStatuses.Paid;
        reward.PaidAt = now;

        _context.Notifications.Add(new Notification
        {
            UserId = reward.OwnerId,
            Title = "Prize Payment Approved",
            Message = $"Your prize claim for {reward.Race.Tournament.TournamentName} has been approved and marked as paid.",
            IsRead = false,
            CreatedAt = now,
            ActionType = "OwnerRewards",
            ActionUrl = "/owner/result-reward",
            RelatedType = "PrizeAward",
            RelatedId = reward.PrizeAwardId
        });

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Reward payment approved successfully.",
            prizeAwardId = reward.PrizeAwardId,
            status = reward.Status,
            paidAt = reward.PaidAt
        });
    }

    [HttpPut("{id:int}/reject")]
    public async Task<IActionResult> RejectPayment(int id)
    {
        var reward = await _context.PrizeAwards
            .Include(a => a.Owner)
                .ThenInclude(o => o.Owner)
            .Include(a => a.Race)
                .ThenInclude(r => r.Tournament)
            .FirstOrDefaultAsync(a => a.PrizeAwardId == id);

        if (reward == null)
        {
            return NotFound(new { message = "Reward not found.", id });
        }

        if (reward.Status != PrizeAwardStatuses.UnderReview)
        {
            return BadRequest(new
            {
                message = "Only rewards under review can be rejected.",
                id,
                status = reward.Status
            });
        }

        var now = DateTime.UtcNow;
        reward.Status = PrizeAwardStatuses.Rejected;

        _context.Notifications.Add(new Notification
        {
            UserId = reward.OwnerId,
            Title = "Prize Payment Rejected",
            Message = $"Your prize claim for {reward.Race.Tournament.TournamentName} has been rejected by admin.",
            IsRead = false,
            CreatedAt = now,
            ActionType = "OwnerRewards",
            ActionUrl = "/owner/result-reward",
            RelatedType = "PrizeAward",
            RelatedId = reward.PrizeAwardId
        });

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Reward payment rejected successfully.",
            prizeAwardId = reward.PrizeAwardId,
            status = reward.Status
        });
    }
}
