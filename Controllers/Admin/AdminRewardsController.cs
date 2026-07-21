using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.Models;
using Eliteracingleague.API.Services.Rewards;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

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
    public async Task<IActionResult> GetRewards(
        [FromQuery] string? status = null,
        [FromQuery] string? recipientType = null,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(status) && !PrizeAwardStatuses.IsValid(status))
        {
            return BadRequest(new
            {
                message = "Invalid payout status.",
                allowedValues = PrizeAwardStatuses.All
            });
        }

        if (!string.IsNullOrWhiteSpace(recipientType) &&
            !PrizePayoutRecipientTypes.IsValid(recipientType))
        {
            return BadRequest(new
            {
                message = "Invalid recipient type.",
                allowedValues = PrizePayoutRecipientTypes.All
            });
        }

        var query = _context.PrizePayouts.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(item => item.Status == status);

        if (!string.IsNullOrWhiteSpace(recipientType))
            query = query.Where(item => item.RecipientType == recipientType);

        var payouts = await query
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => new
            {
                prizePayoutId = item.PrizePayoutId,
                prizeAwardId = item.PrizeAwardId,
                raceId = item.PrizeAward.RaceId,
                raceName = item.PrizeAward.Race.RaceName,
                tournamentId = item.PrizeAward.Race.TournamentId,
                tournamentName = item.PrizeAward.Race.Tournament.TournamentName,
                recipientUserId = item.RecipientUserId,
                recipientType = item.RecipientType,
                recipientName = item.RecipientUser.FullName,
                ownerId = item.PrizeAward.OwnerId,
                ownerName = item.PrizeAward.Owner.Owner.FullName,
                jockeyId = item.PrizeAward.JockeyId,
                jockeyName = item.PrizeAward.Jockey == null
                    ? null
                    : item.PrizeAward.Jockey.JockeyNavigation.FullName,
                horseId = item.PrizeAward.Registration.HorseId,
                horseName = item.PrizeAward.Registration.Horse.HorseName,
                rankPosition = item.PrizeAward.RankPosition,
                totalPrizeAmount = item.PrizeAward.PrizeAmount,
                payoutAmount = item.Amount,
                status = item.Status,
                claimedAt = item.ClaimedAt,
                paidAt = item.PaidAt,
                rejectedAt = item.RejectedAt,
                paymentReference = item.PaymentReference,
                adminNote = item.AdminNote,
                createdAt = item.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(payouts);
    }

    [HttpPut("{payoutId:int}/approve-payment")]
    public async Task<IActionResult> ApprovePayment(
        int payoutId,
        [FromBody] ReviewPrizePayoutRequest? request,
        CancellationToken cancellationToken)
    {
        var payout = await _context.PrizePayouts
            .Include(item => item.RecipientUser)
            .Include(item => item.PrizeAward)
                .ThenInclude(item => item.Payouts)
            .Include(item => item.PrizeAward)
                .ThenInclude(item => item.Race)
                    .ThenInclude(item => item.Tournament)
            .FirstOrDefaultAsync(item => item.PrizePayoutId == payoutId, cancellationToken);

        if (payout == null)
            return NotFound(new { message = "Prize payout not found.", payoutId });

        if (payout.Status != PrizeAwardStatuses.UnderReview)
        {
            return BadRequest(new
            {
                message = "Only payouts under review can be marked as paid.",
                payoutId,
                status = payout.Status
            });
        }

        var paymentReference = Normalize(request?.PaymentReference);
        if (string.IsNullOrWhiteSpace(paymentReference))
        {
            return BadRequest(new
            {
                message = "Payment reference is required after the external bank/gateway transfer is completed."
            });
        }

        var now = DateTime.UtcNow;
        payout.Status = PrizeAwardStatuses.Paid;
        payout.PaidAt = now;
        payout.RejectedAt = null;
        payout.PaymentReference = paymentReference;
        payout.AdminNote = Normalize(request?.AdminNote);
        payout.UpdatedAt = now;

        PrizePayoutService.SynchronizeAggregateStatus(payout.PrizeAward, now);

        _context.Notifications.Add(new Notification
        {
            UserId = payout.RecipientUserId,
            Title = "Prize Payment Approved",
            Message = $"Your {payout.RecipientType.ToLowerInvariant()} payout for {payout.PrizeAward.Race.Tournament.TournamentName} was marked as paid. Reference: {paymentReference}.",
            IsRead = false,
            CreatedAt = now,
            ActionType = payout.RecipientType == PrizePayoutRecipientTypes.Owner
                ? "OwnerRewards"
                : "JockeyRewards",
            ActionUrl = payout.RecipientType == PrizePayoutRecipientTypes.Owner
                ? "/owner/rewards"
                : "/jockey/rewards",
            RelatedType = "PrizePayout",
            RelatedId = payout.PrizePayoutId
        });

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "Prize payout marked as paid successfully.",
            prizePayoutId = payout.PrizePayoutId,
            prizeAwardId = payout.PrizeAwardId,
            recipientType = payout.RecipientType,
            payoutAmount = payout.Amount,
            status = payout.Status,
            paidAt = payout.PaidAt,
            paymentReference = payout.PaymentReference
        });
    }

    [HttpPut("{payoutId:int}/reject")]
    public async Task<IActionResult> RejectPayment(
        int payoutId,
        [FromBody] ReviewPrizePayoutRequest? request,
        CancellationToken cancellationToken)
    {
        var payout = await _context.PrizePayouts
            .Include(item => item.RecipientUser)
            .Include(item => item.PrizeAward)
                .ThenInclude(item => item.Payouts)
            .Include(item => item.PrizeAward)
                .ThenInclude(item => item.Race)
                    .ThenInclude(item => item.Tournament)
            .FirstOrDefaultAsync(item => item.PrizePayoutId == payoutId, cancellationToken);

        if (payout == null)
            return NotFound(new { message = "Prize payout not found.", payoutId });

        if (payout.Status != PrizeAwardStatuses.UnderReview)
        {
            return BadRequest(new
            {
                message = "Only payouts under review can be rejected.",
                payoutId,
                status = payout.Status
            });
        }

        var now = DateTime.UtcNow;
        payout.Status = PrizeAwardStatuses.Rejected;
        payout.RejectedAt = now;
        payout.PaidAt = null;
        payout.PaymentReference = null;
        payout.AdminNote = Normalize(request?.AdminNote);
        payout.UpdatedAt = now;

        PrizePayoutService.SynchronizeAggregateStatus(payout.PrizeAward, now);

        _context.Notifications.Add(new Notification
        {
            UserId = payout.RecipientUserId,
            Title = "Prize Payment Rejected",
            Message = $"Your {payout.RecipientType.ToLowerInvariant()} payout for {payout.PrizeAward.Race.Tournament.TournamentName} was rejected by admin.",
            IsRead = false,
            CreatedAt = now,
            ActionType = payout.RecipientType == PrizePayoutRecipientTypes.Owner
                ? "OwnerRewards"
                : "JockeyRewards",
            ActionUrl = payout.RecipientType == PrizePayoutRecipientTypes.Owner
                ? "/owner/rewards"
                : "/jockey/rewards",
            RelatedType = "PrizePayout",
            RelatedId = payout.PrizePayoutId
        });

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "Prize payout rejected successfully.",
            prizePayoutId = payout.PrizePayoutId,
            prizeAwardId = payout.PrizeAwardId,
            recipientType = payout.RecipientType,
            status = payout.Status
        });
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetRewardSummary(CancellationToken cancellationToken)
    {
        var prizePayments = await _context.PrizePayouts
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Total = group.Count(),
                ReadyToClaim = group.Count(item => item.Status == PrizeAwardStatuses.ReadyToClaim),
                UnderReview = group.Count(item => item.Status == PrizeAwardStatuses.UnderReview),
                Paid = group.Count(item => item.Status == PrizeAwardStatuses.Paid),
                Rejected = group.Count(item => item.Status == PrizeAwardStatuses.Rejected),
                TotalAmount = group.Sum(item => item.Amount),
                PaidAmount = group.Where(item => item.Status == PrizeAwardStatuses.Paid)
                    .Sum(item => (decimal?)item.Amount) ?? 0m,
                OwnerAmount = group.Where(item => item.RecipientType == PrizePayoutRecipientTypes.Owner)
                    .Sum(item => (decimal?)item.Amount) ?? 0m,
                JockeyAmount = group.Where(item => item.RecipientType == PrizePayoutRecipientTypes.Jockey)
                    .Sum(item => (decimal?)item.Amount) ?? 0m
            })
            .FirstOrDefaultAsync(cancellationToken);

        var seasonRewards = await _context.SeasonRewards
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Total = group.Count(),
                Eligible = group.Count(item => item.Status == SeasonRewardStatuses.Eligible),
                Claimed = group.Count(item => item.Status == SeasonRewardStatuses.Claimed),
                Approved = group.Count(item => item.Status == SeasonRewardStatuses.Approved),
                Preparing = group.Count(item => item.Status == SeasonRewardStatuses.Preparing),
                Shipped = group.Count(item => item.Status == SeasonRewardStatuses.Shipped),
                Delivered = group.Count(item => item.Status == SeasonRewardStatuses.Delivered),
                Rejected = group.Count(item => item.Status == SeasonRewardStatuses.Rejected),
                Expired = group.Count(item => item.Status == SeasonRewardStatuses.Expired)
            })
            .FirstOrDefaultAsync(cancellationToken);

        return Ok(new
        {
            prizePayments = prizePayments ?? new
            {
                Total = 0,
                ReadyToClaim = 0,
                UnderReview = 0,
                Paid = 0,
                Rejected = 0,
                TotalAmount = 0m,
                PaidAmount = 0m,
                OwnerAmount = 0m,
                JockeyAmount = 0m
            },
            seasonRewards = seasonRewards ?? new
            {
                Total = 0,
                Eligible = 0,
                Claimed = 0,
                Approved = 0,
                Preparing = 0,
                Shipped = 0,
                Delivered = 0,
                Rejected = 0,
                Expired = 0
            }
        });
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public sealed class ReviewPrizePayoutRequest
    {
        [StringLength(200)]
        public string? PaymentReference { get; set; }

        [StringLength(1000)]
        public string? AdminNote { get; set; }
    }

    [HttpGet("season-rewards")]
    public async Task<IActionResult> GetSeasonRewards(
        [FromQuery] int? seasonId = null,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(status) && !SeasonRewardStatuses.IsValid(status))
        {
            return BadRequest(new
            {
                message = "Invalid season reward status.",
                allowedValues = SeasonRewardStatuses.All
            });
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _context.SeasonRewards
            .AsNoTracking()
            .AsQueryable();

        if (seasonId.HasValue)
            query = query.Where(item => item.SeasonId == seasonId.Value);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(item => item.Status == status);

        var normalizedSearch = string.IsNullOrWhiteSpace(search)
            ? null
            : search.Trim();

        if (normalizedSearch != null)
        {
            query = query.Where(item =>
                item.RewardName.Contains(normalizedSearch) ||
                item.Season.SeasonName.Contains(normalizedSearch) ||
                item.Spectator.FullName.Contains(normalizedSearch) ||
                item.Spectator.Email.Contains(normalizedSearch) ||
                (item.RewardItem != null && item.RewardItem.Name.Contains(normalizedSearch)));
        }

        var totalItems = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(item => item.AwardedAt)
            .ThenBy(item => item.RankPosition)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new
            {
                item.SeasonRewardId,
                item.SeasonId,
                item.Season.SeasonName,
                item.SpectatorId,
                SpectatorName = item.Spectator.FullName,
                SpectatorEmail = item.Spectator.Email,
                item.RankPosition,
                item.FinalPoints,
                item.RewardName,
                item.RewardDescription,
                item.BonusPoints,
                item.RewardItemId,
                RewardItemName = item.RewardItem == null ? null : item.RewardItem.Name,
                RewardItemSku = item.RewardItem == null ? null : item.RewardItem.Sku,
                item.Quantity,
                item.InventoryReserved,
                item.IsBonusApplied,
                item.AppliedToSeasonId,
                item.AppliedAt,
                item.Status,
                item.AwardedAt,
                item.ClaimDeadline,
                item.ClaimedAt,
                item.ApprovedAt,
                item.PreparingAt,
                item.DeliveredAt,
                item.RejectedAt,
                item.ReceiverName,
                item.ReceiverPhone,
                item.DeliveryAddress,
                item.AdminNote
            })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            page,
            pageSize,
            totalItems,
            totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize),
            items
        });
    }

}
