using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Jockey;
using Eliteracingleague.API.Services;
using Eliteracingleague.API.Services.Rewards;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Controllers.Jockey;

[Route("api/jockey/rewards")]
[ApiController]
[Authorize(Roles = UserRoles.Jockey)]
public class JockeyRewardsController : ControllerBase
{
    private static readonly string[] EarnedStatuses =
    {
        PrizeAwardStatuses.ReadyToClaim,
        PrizeAwardStatuses.UnderReview,
        PrizeAwardStatuses.Paid
    };

    private readonly EliteRacingLeagueContext _context;
    private readonly JockeyAccessService _jockeyAccess;

    public JockeyRewardsController(
        EliteRacingLeagueContext context,
        JockeyAccessService jockeyAccess)
    {
        _context = context;
        _jockeyAccess = jockeyAccess;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(CancellationToken cancellationToken)
    {
        var access = await _jockeyAccess.ValidateActiveJockeyAsync(User);
        if (!access.Succeeded) return AccessError(access);

        var jockeyId = access.Jockey!.JockeyId;
        var payouts = _context.PrizePayouts
            .AsNoTracking()
            .Where(item =>
                item.RecipientUserId == jockeyId &&
                item.RecipientType == PrizePayoutRecipientTypes.Jockey);

        var totalPrizeEarned = await payouts
            .Where(item => EarnedStatuses.Contains(item.Status))
            .SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;

        var paidAmount = await payouts
            .Where(item => item.Status == PrizeAwardStatuses.Paid)
            .SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;

        var pendingAmount = await payouts
            .Where(item => item.Status == PrizeAwardStatuses.UnderReview)
            .SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;

        var raceWins = await payouts
            .Where(item =>
                EarnedStatuses.Contains(item.Status) &&
                item.PrizeAward.RankPosition == 1)
            .Select(item => item.PrizeAwardId)
            .Distinct()
            .CountAsync(cancellationToken);

        return Ok(new
        {
            totalPrizeEarned,
            paidAmount,
            pendingAmount,
            raceWins
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetRewards(
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var access = await _jockeyAccess.ValidateActiveJockeyAsync(User);
        if (!access.Succeeded) return AccessError(access);

        var jockeyId = access.Jockey!.JockeyId;
        limit = Math.Clamp(limit, 1, 100);

        var rewards = await _context.PrizePayouts
            .AsNoTracking()
            .Where(item =>
                item.RecipientUserId == jockeyId &&
                item.RecipientType == PrizePayoutRecipientTypes.Jockey)
            .OrderByDescending(item => item.PrizeAward.Race.RaceDate)
            .ThenByDescending(item => item.CreatedAt)
            .Take(limit)
            .Select(item => new
            {
                prizePayoutId = item.PrizePayoutId,
                prizeAwardId = item.PrizeAwardId,
                tournamentName = item.PrizeAward.Race.Tournament.TournamentName,
                raceName = item.PrizeAward.Race.RaceName,
                raceDate = item.PrizeAward.Race.RaceDate,
                horseName = item.PrizeAward.Registration.Horse.HorseName,
                ownerName = item.PrizeAward.Owner.Owner.FullName,
                rankPosition = item.PrizeAward.RankPosition,
                totalPrizeAmount = item.PrizeAward.PrizeAmount,
                payoutAmount = item.Amount,
                recipientType = item.RecipientType,
                status = item.Status,
                claimedAt = item.ClaimedAt,
                paidAt = item.PaidAt,
                paymentReference = item.PaymentReference,
                adminNote = item.AdminNote,
                canClaim = item.Status == PrizeAwardStatuses.ReadyToClaim
            })
            .ToListAsync(cancellationToken);

        return Ok(rewards);
    }

    [HttpPut("{prizePayoutId:int}/claim")]
    public async Task<IActionResult> ClaimReward(
        int prizePayoutId,
        CancellationToken cancellationToken)
    {
        var access = await _jockeyAccess.ValidateActiveJockeyAsync(User);
        if (!access.Succeeded) return AccessError(access);

        var jockeyId = access.Jockey!.JockeyId;
        var payout = await _context.PrizePayouts
            .Include(item => item.PrizeAward)
                .ThenInclude(item => item.Payouts)
            .FirstOrDefaultAsync(item =>
                item.PrizePayoutId == prizePayoutId &&
                item.RecipientUserId == jockeyId &&
                item.RecipientType == PrizePayoutRecipientTypes.Jockey,
                cancellationToken);

        if (payout == null)
            return NotFound(new { message = "Jockey prize payout not found." });

        if (payout.Status != PrizeAwardStatuses.ReadyToClaim)
        {
            return BadRequest(new
            {
                message = "Only payouts with ReadyToClaim status can be claimed.",
                status = payout.Status
            });
        }

        var now = DateTime.UtcNow;
        payout.Status = PrizeAwardStatuses.UnderReview;
        payout.ClaimedAt = now;
        payout.UpdatedAt = now;
        PrizePayoutService.SynchronizeAggregateStatus(payout.PrizeAward, now);

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "Jockey payout claim submitted successfully.",
            prizePayoutId = payout.PrizePayoutId,
            payoutAmount = payout.Amount,
            status = payout.Status
        });
    }

    private IActionResult AccessError(JockeyAccessResult access)
    {
        if (access.StatusCode == StatusCodes.Status401Unauthorized)
            return Unauthorized(new { message = access.Message });

        return StatusCode(access.StatusCode, new
        {
            message = access.Message,
            nextStep = access.NextStep
        });
    }
}
