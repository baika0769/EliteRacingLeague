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

    [HttpGet("summary")]
    public async Task<IActionResult> GetRewardSummary(CancellationToken cancellationToken)
    {
        var prizePayments = await _context.PrizeAwards
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Total = group.Count(),
                ReadyToClaim = group.Count(item => item.Status == PrizeAwardStatuses.ReadyToClaim),
                UnderReview = group.Count(item => item.Status == PrizeAwardStatuses.UnderReview),
                Paid = group.Count(item => item.Status == PrizeAwardStatuses.Paid),
                Rejected = group.Count(item => item.Status == PrizeAwardStatuses.Rejected),
                TotalAmount = group.Sum(item => item.PrizeAmount),
                PaidAmount = group.Where(item => item.Status == PrizeAwardStatuses.Paid)
                    .Sum(item => (decimal?)item.PrizeAmount) ?? 0m
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
                PaidAmount = 0m
            },
            seasonRewards = seasonRewards ?? new
            {
                Total = 0,
                Eligible = 0,
                Claimed = 0,
                Approved = 0,
                Preparing = 0,
                Delivered = 0,
                Rejected = 0,
                Expired = 0
            }
        });
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
