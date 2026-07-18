using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.Models;
using Eliteracingleague.API.Services;
using Eliteracingleague.API.Services.SystemTime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Controllers.Spectator;

[Authorize(Roles = UserRoles.Spectator)]
[ApiController]
[Route("api/spectator/rewards")]
public class SpectatorRewardsController : ControllerBase
{
    private readonly EliteRacingLeagueContext _context;
    private readonly SpectatorLeaderboardService _leaderboardService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public SpectatorRewardsController(
        EliteRacingLeagueContext context,
        SpectatorLeaderboardService leaderboardService,
        IDateTimeProvider dateTimeProvider)
    {
        _context = context;
        _leaderboardService = leaderboardService;
        _dateTimeProvider = dateTimeProvider;
    }

    private int GetUserId()
        => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    [HttpGet]
    public async Task<IActionResult> GetRewards()
    {
        var userId = GetUserId();
        var now = _dateTimeProvider.UtcNow;
        var activeSeason = await _leaderboardService.GetActiveSeasonAsync();
        var rewardSummary = await _leaderboardService.GetRewardSummaryAsync(userId);
        var myRank = await _leaderboardService.GetMyRankAsync(userId);
        var totalDays = await _leaderboardService.GetActiveSeasonTotalDaysAsync();

        var pointHistoryQuery = _context.RacePredictions
            .AsNoTracking()
            .Where(p =>
                p.SpectatorId == userId &&
                p.Status != RacePredictionStatuses.Cancelled &&
                (p.StakePoints > 0 || p.PointsAwarded > 0));

        if (activeSeason != null)
        {
            pointHistoryQuery = pointHistoryQuery.Where(p =>
                p.Race.Tournament.SeasonId == activeSeason.SeasonId);
        }
        else
        {
            pointHistoryQuery = pointHistoryQuery.Where(_ => false);
        }

        var pointHistory = await pointHistoryQuery
            .OrderByDescending(p => p.EvaluatedAt ?? p.UpdatedAt ?? p.CreatedAt)
            .Select(p => new
            {
                predictionId = p.PredictionId,
                seasonId = p.Race.Tournament.SeasonId,
                seasonName = p.Race.Tournament.Season.SeasonName,
                tournamentId = p.Race.TournamentId,
                tournamentName = p.Race.Tournament.TournamentName,
                raceName = p.Race.RaceName,
                predictedHorseName = p.PredictedRegistration.Horse.HorseName,
                actualWinnerHorseName = p.ActualWinnerRegistration != null
                    ? p.ActualWinnerRegistration.Horse.HorseName
                    : null,
                status = p.Status,
                isCorrect = p.IsCorrect,
                stakePoints = p.StakePoints,
                payoutPoints = p.PointsAwarded,
                points = p.PointsAwarded,
                netPoints = p.Status == RacePredictionStatuses.Evaluated
                    ? p.PointsAwarded - p.StakePoints
                    : -p.StakePoints,
                rewardAmount = p.RewardAmount,
                rewardStatus = p.RewardStatus,
                evaluatedAt = p.EvaluatedAt,
                awardedAt = p.EvaluatedAt ?? p.UpdatedAt ?? p.CreatedAt
            })
            .ToListAsync();

        var activeRewardRules = activeSeason == null
            ? new List<ActiveSeasonRewardRuleResponse>()
            : await _context.SeasonRewardRules
                .AsNoTracking()
                .Where(item => item.SeasonId == activeSeason.SeasonId)
                .OrderBy(item => item.RankPosition)
                .Select(item => new ActiveSeasonRewardRuleResponse
                {
                    SeasonRewardRuleId = item.SeasonRewardRuleId,
                    RankPosition = item.RankPosition,
                    RewardName = item.RewardName,
                    RewardDescription = item.RewardDescription,
                    BonusPoints = item.BonusPoints
                })
                .ToListAsync();

        var mySeasonRewards = await _context.SeasonRewards
            .AsNoTracking()
            .Where(item => item.SpectatorId == userId)
            .OrderByDescending(item => item.AwardedAt)
            .Select(item => new
            {
                item.SeasonRewardId,
                item.SeasonId,
                item.Season.SeasonName,
                item.RankPosition,
                item.FinalPoints,
                item.RewardName,
                item.RewardDescription,
                item.BonusPoints,
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
                item.AdminNote,
                canClaim = item.Status == SeasonRewardStatuses.Eligible &&
                           (!item.ClaimDeadline.HasValue || item.ClaimDeadline > now)
            })
            .ToListAsync();

        var walletTransactions = activeSeason == null
            ? new List<WalletTransactionResponse>()
            : await _context.PointTransactions
                .AsNoTracking()
                .Where(item =>
                    item.SpectatorSeasonWallet.SeasonId == activeSeason.SeasonId &&
                    item.SpectatorSeasonWallet.SpectatorId == userId)
                .OrderByDescending(item => item.CreatedAt)
                .Take(100)
                .Select(item => new WalletTransactionResponse
                {
                    PointTransactionId = item.PointTransactionId,
                    TransactionType = item.TransactionType,
                    Amount = item.Amount,
                    ScoreDelta = item.ScoreDelta,
                    BalanceBefore = item.BalanceBefore,
                    BalanceAfter = item.BalanceAfter,
                    ReferenceType = item.ReferenceType,
                    ReferenceId = item.ReferenceId,
                    Description = item.Description,
                    CreatedAt = item.CreatedAt
                })
                .ToListAsync();

        return Ok(new
        {
            activeSeasonId = activeSeason?.SeasonId,
            activeSeasonName = activeSeason?.SeasonName,
            rewardPoints = rewardSummary.RewardPoints,
            seasonScore = rewardSummary.RewardPoints,
            bettingPoints = rewardSummary.BettingPoints,
            totalStakePoints = rewardSummary.TotalStakePoints,
            totalPayoutPoints = rewardSummary.TotalPayoutPoints,
            netPoints = rewardSummary.NetPoints,
            correctPredictions = rewardSummary.CorrectPredictions,
            predictionAccuracy = rewardSummary.PredictionAccuracy,
            myRank,
            totalDays,
            activeRewardRules,
            mySeasonRewards,
            pointHistory,
            walletTransactions
        });
    }

    [HttpPost("{rewardId:int}/claim")]
    public async Task<IActionResult> ClaimReward(
        int rewardId,
        [FromBody] ClaimSeasonRewardRequest request)
    {
        var userId = GetUserId();
        var reward = await _context.SeasonRewards
            .Include(item => item.Season)
            .FirstOrDefaultAsync(item =>
                item.SeasonRewardId == rewardId &&
                item.SpectatorId == userId);

        if (reward == null)
        {
            return NotFound(new { message = "Season reward not found." });
        }

        if (reward.Status != SeasonRewardStatuses.Eligible)
        {
            return BadRequest(new
            {
                message = "Only an eligible reward can be claimed.",
                rewardId,
                status = reward.Status
            });
        }

        var now = _dateTimeProvider.UtcNow;
        if (reward.ClaimDeadline.HasValue && now > reward.ClaimDeadline.Value)
        {
            reward.Status = SeasonRewardStatuses.Expired;
            await _context.SaveChangesAsync();

            return BadRequest(new
            {
                message = "The reward claim deadline has passed.",
                rewardId,
                reward.ClaimDeadline
            });
        }

        reward.ReceiverName = request.ReceiverName.Trim();
        reward.ReceiverPhone = request.ReceiverPhone.Trim();
        reward.DeliveryAddress = request.DeliveryAddress.Trim();
        reward.Status = SeasonRewardStatuses.Claimed;
        reward.ClaimedAt = now;
        reward.AdminNote = null;

        var adminIds = await _context.Users
            .AsNoTracking()
            .Where(item => item.Role == UserRoles.Admin && item.Status == UserStatuses.Active)
            .Select(item => item.UserId)
            .ToListAsync();

        foreach (var adminId in adminIds)
        {
            _context.Notifications.Add(new Notification
            {
                UserId = adminId,
                Title = "Season Reward Claimed",
                Message = $"A spectator claimed {reward.RewardName} from season {reward.Season.SeasonName}.",
                IsRead = false,
                CreatedAt = now,
                ActionType = "AdminSeasonRewards",
                ActionUrl = "/admin/seasons",
                RelatedType = "SeasonReward",
                RelatedId = reward.SeasonRewardId
            });
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Reward claim submitted successfully.",
            rewardId = reward.SeasonRewardId,
            reward.Status,
            reward.ClaimedAt
        });
    }
}

public class ClaimSeasonRewardRequest
{
    [Required]
    [StringLength(200, MinimumLength = 2)]
    public string ReceiverName { get; set; } = string.Empty;

    [Required]
    [StringLength(30, MinimumLength = 8)]
    [RegularExpression(@"^[0-9+\-\s()]+$", ErrorMessage = "Receiver phone is invalid.")]
    public string ReceiverPhone { get; set; } = string.Empty;

    [Required]
    [StringLength(500, MinimumLength = 5)]
    public string DeliveryAddress { get; set; } = string.Empty;
}

public class ActiveSeasonRewardRuleResponse
{
    public int SeasonRewardRuleId { get; set; }
    public int RankPosition { get; set; }
    public string RewardName { get; set; } = string.Empty;
    public string? RewardDescription { get; set; }
    public int BonusPoints { get; set; }
}

public class WalletTransactionResponse
{
    public int PointTransactionId { get; set; }
    public string TransactionType { get; set; } = string.Empty;
    public int Amount { get; set; }
    public int ScoreDelta { get; set; }
    public int BalanceBefore { get; set; }
    public int BalanceAfter { get; set; }
    public string? ReferenceType { get; set; }
    public int? ReferenceId { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
}
