using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Owner.Rewards;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Controllers.Owner;

[Route("api/owner/rewards")]
[ApiController]
[Authorize(Roles = UserRoles.HorseOwner)]
public class OwnerRewardsController : OwnerBaseController
{
    private static readonly string[] VisibleRewardStatuses =
    {
        PrizeAwardStatuses.ReadyToClaim,
        PrizeAwardStatuses.UnderReview,
        PrizeAwardStatuses.Paid
    };

    public OwnerRewardsController(EliteRacingLeagueContext context) : base(context)
    {
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
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

        var totalPrizeEarned = await _context.PrizeAwards
            .Where(a => a.OwnerId == ownerId.Value)
            .SumAsync(a => (decimal?)a.PrizeAmount) ?? 0;

        var claimedRewards = await _context.PrizeAwards
            .Where(a =>
                a.OwnerId == ownerId.Value &&
                a.Status == PrizeAwardStatuses.Paid)
            .SumAsync(a => (decimal?)a.PrizeAmount) ?? 0;

        var tournamentWins = await _context.PrizeAwards
            .CountAsync(a =>
                a.OwnerId == ownerId.Value &&
                a.RankPosition == 1);

        return Ok(new OwnerRewardSummaryResponse
        {
            TotalPrizeEarned = totalPrizeEarned,
            ClaimedRewards = claimedRewards,
            TournamentWins = tournamentWins
        });
    }

    [HttpGet("available")]
    public async Task<IActionResult> GetAvailableRewards([FromQuery] int limit = 10)
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

        limit = Math.Clamp(limit, 1, 50);

        var rewardData = await _context.PrizeAwards
            .AsNoTracking()
            .Where(a =>
                a.OwnerId == ownerId.Value &&
                VisibleRewardStatuses.Contains(a.Status))
            .OrderByDescending(a => a.Race.RaceDate)
            .ThenByDescending(a => a.CreatedAt)
            .Take(limit)
            .Select(a => new
            {
                a.PrizeAwardId,
                TournamentName = a.Race.Tournament.TournamentName,
                RaceDate = a.Race.RaceDate,
                HorseName = a.Registration.Horse.HorseName,
                a.RankPosition,
                a.PrizeAmount,
                a.Status
            })
            .ToListAsync();

        var rewards = rewardData.Select(a => new OwnerAvailableRewardResponse
        {
            PrizeAwardId = a.PrizeAwardId,
            TournamentName = a.TournamentName,
            RaceDate = a.RaceDate,
            HorseName = a.HorseName,
            RankPosition = a.RankPosition,
            PrizeAmount = a.PrizeAmount,
            Status = a.Status,
            CanClaim = PrizeAwardStatuses.CanClaim(a.Status)
        });

        return Ok(rewards);
    }

    [HttpPut("{prizeAwardId:int}/claim")]
    public async Task<IActionResult> ClaimReward(int prizeAwardId)
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

        var prizeAward = await _context.PrizeAwards
            .FirstOrDefaultAsync(a =>
                a.PrizeAwardId == prizeAwardId &&
                a.OwnerId == ownerId.Value);

        if (prizeAward == null)
        {
            return NotFound(new
            {
                message = "Reward not found."
            });
        }

        if (!PrizeAwardStatuses.CanClaim(prizeAward.Status))
        {
            return BadRequest(new
            {
                message = "Only rewards with ReadyToClaim status can be claimed."
            });
        }

        prizeAward.Status = PrizeAwardStatuses.UnderReview;
        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Reward claim submitted successfully.",
            prizeAwardId = prizeAward.PrizeAwardId,
            status = prizeAward.Status
        });
    }
}
