using System.ComponentModel.DataAnnotations;
using System.Data;
using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Admin;
using Eliteracingleague.API.Extensions;
using Eliteracingleague.API.Models;
using Eliteracingleague.API.Services;
using Eliteracingleague.API.Services.Rewards;
using Eliteracingleague.API.Services.Email;
using Eliteracingleague.API.Services.Auditing;
using Eliteracingleague.API.Services.SystemTime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Controllers.Admin;

[Authorize(Roles = UserRoles.Admin)]
[ApiController]
[Route("api/admin/seasons")]
public class AdminSeasonsController : ControllerBase
{
    private const int MaxPredictionPoints = 1_000_000;
    private const int MaxRewardRules = 100;
    private const int MaxRewardBonusPoints = 1_000_000;
    private const int MaxSeasonDurationDays = 3660;

    private readonly EliteRacingLeagueContext _context;
    private readonly SpectatorLeaderboardService _leaderboardService;
    private readonly SpectatorWalletService _spectatorWalletService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly RewardInventoryService _rewardInventoryService;
    private readonly IAuditService _auditService;
    private readonly SeasonRewardEmailService _rewardEmailService;

    public AdminSeasonsController(
        EliteRacingLeagueContext context,
        SpectatorLeaderboardService leaderboardService,
        SpectatorWalletService spectatorWalletService,
        IDateTimeProvider dateTimeProvider,
        RewardInventoryService rewardInventoryService,
        IAuditService auditService,
        SeasonRewardEmailService rewardEmailService)
    {
        _context = context;
        _leaderboardService = leaderboardService;
        _spectatorWalletService = spectatorWalletService;
        _dateTimeProvider = dateTimeProvider;
        _rewardInventoryService = rewardInventoryService;
        _auditService = auditService;
        _rewardEmailService = rewardEmailService;
    }

    [HttpGet]
    public async Task<IActionResult> GetSeasons()
    {
        var seasons = await _context.Seasons
            .AsNoTracking()
            .OrderByDescending(s => s.StartDate)
            .ThenByDescending(s => s.SeasonId)
            .Select(s => new
            {
                s.SeasonId,
                s.SeasonName,
                s.StartDate,
                s.EndDate,
                s.Status,
                s.PointsPerCorrectPrediction,
                s.CreatedAt,
                s.UpdatedAt,
                TournamentCount = s.Tournaments.Count,
                RewardRuleCount = s.SeasonRewardRules.Count,
                RewardCount = s.SeasonRewards.Count
            })
            .ToListAsync();

        return Ok(seasons);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetSeasonById(int id)
    {
        var season = await _context.Seasons
            .AsNoTracking()
            .Where(s => s.SeasonId == id)
            .Select(s => new
            {
                s.SeasonId,
                s.SeasonName,
                s.StartDate,
                s.EndDate,
                s.Status,
                s.PointsPerCorrectPrediction,
                s.CreatedAt,
                s.UpdatedAt,
                Tournaments = s.Tournaments
                    .OrderBy(t => t.StartDate)
                    .Select(t => new
                    {
                        t.TournamentId,
                        t.TournamentName,
                        t.StartDate,
                        t.EndDate,
                        t.Status
                    }),
                RewardRules = s.SeasonRewardRules
                    .OrderBy(r => r.RankPosition)
                    .Select(r => new
                    {
                        r.SeasonRewardRuleId,
                        r.RankPosition,
                        r.RewardName,
                        r.RewardDescription,
                        r.BonusPoints,
                        r.RewardItemId,
                        RewardItemName = r.RewardItem == null ? null : r.RewardItem.Name,
                        RewardItemSku = r.RewardItem == null ? null : r.RewardItem.Sku,
                        RewardItemIsActive = r.RewardItem == null ? (bool?)null : r.RewardItem.IsActive,
                        RewardItemAvailableQuantity = r.RewardItem == null
                            ? (int?)null
                            : r.RewardItem.StockQuantity - r.RewardItem.ReservedQuantity - r.RewardItem.DeliveredQuantity,
                        r.Quantity
                    }),
                Rewards = s.SeasonRewards
                    .OrderBy(r => r.RankPosition)
                    .Select(r => new
                    {
                        r.SeasonRewardId,
                        r.SpectatorId,
                        SpectatorName = r.Spectator.FullName,
                        r.RankPosition,
                        r.FinalPoints,
                        r.RewardName,
                        r.RewardDescription,
                        r.BonusPoints,
                        r.IsBonusApplied,
                        r.AppliedToSeasonId,
                        r.AppliedAt,
                        r.Status,
                        r.AwardedAt,
                        r.ClaimDeadline,
                        r.ClaimedAt,
                        r.ApprovedAt,
                        r.PreparingAt,
                        r.ShippedAt,
                        r.DeliveredAt,
                        r.RejectedAt,
                        r.ReceiverName,
                        r.ReceiverPhone,
                        r.DeliveryAddress,
                        r.AdminNote
                    })
            })
            .FirstOrDefaultAsync();

        if (season == null)
        {
            return NotFound(new AdminActionResponse
            {
                Message = "Season not found",
                Id = id
            });
        }

        return Ok(season);
    }

    [HttpPost]
    public async Task<IActionResult> CreateSeason([FromBody] AdminSeasonRequest request)
    {
        var validationError = ValidateSeasonRequest(request);
        if (validationError != null)
        {
            return validationError;
        }

        var startDate = request.StartDate.Date;
        var endDate = request.EndDate.Date;
        var normalizedName = request.SeasonName.Trim();

        var duplicateNameExists = await _context.Seasons
            .AsNoTracking()
            .AnyAsync(s =>
                s.Status != SeasonStatuses.Cancelled &&
                s.SeasonName == normalizedName);

        if (duplicateNameExists)
        {
            return Conflict(new
            {
                message = "Season name already exists"
            });
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable);

        try
        {
            var overlappingSeason = await FindOverlappingSeasonAsync(
                startDate,
                endDate);

            if (overlappingSeason != null)
            {
                return BadRequest(new
                {
                    message = "Season date range overlaps an existing season.",
                    conflictingSeasonId = overlappingSeason.SeasonId,
                    conflictingSeasonName = overlappingSeason.SeasonName,
                    conflictingStartDate = overlappingSeason.StartDate,
                    conflictingEndDate = overlappingSeason.EndDate,
                    conflictingStatus = overlappingSeason.Status
                });
            }

            var now = DateTime.UtcNow;

            var season = new Season
            {
                SeasonName = normalizedName,
                StartDate = startDate,
                EndDate = endDate,
                Status = SeasonStatuses.Draft,
                PointsPerCorrectPrediction = request.PointsPerCorrectPrediction,
                CreatedAt = now,
                UpdatedAt = now
            };

            _context.Seasons.Add(season);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new AdminActionResponse
            {
                Message = "Season created successfully",
                Id = season.SeasonId,
                Name = season.SeasonName,
                Status = season.Status
            });
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateSeason(
        int id,
        [FromBody] AdminSeasonRequest request)
    {
        var validationError = ValidateSeasonRequest(request);
        if (validationError != null)
        {
            return validationError;
        }

        var startDate = request.StartDate.Date;
        var endDate = request.EndDate.Date;
        var normalizedName = request.SeasonName.Trim();

        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable);

        try
        {
            var season = await _context.Seasons
                .FirstOrDefaultAsync(s => s.SeasonId == id);

            if (season == null)
            {
                return NotFound(new AdminActionResponse
                {
                    Message = "Season not found",
                    Id = id
                });
            }

            if (season.Status != SeasonStatuses.Draft)
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "Only draft season can be edited",
                    Id = season.SeasonId,
                    Name = season.SeasonName,
                    Status = season.Status
                });
            }

            var duplicateNameExists = await _context.Seasons
                .AsNoTracking()
                .AnyAsync(s =>
                    s.SeasonId != season.SeasonId &&
                    s.Status != SeasonStatuses.Cancelled &&
                    s.SeasonName == normalizedName);

            if (duplicateNameExists)
            {
                return Conflict(new
                {
                    message = "Season name already exists",
                    seasonId = season.SeasonId
                });
            }

            var overlappingSeason = await FindOverlappingSeasonAsync(
                startDate,
                endDate,
                season.SeasonId);

            if (overlappingSeason != null)
            {
                return BadRequest(new
                {
                    message = "Season date range overlaps an existing season.",
                    seasonId = season.SeasonId,
                    conflictingSeasonId = overlappingSeason.SeasonId,
                    conflictingSeasonName = overlappingSeason.SeasonName,
                    conflictingStartDate = overlappingSeason.StartDate,
                    conflictingEndDate = overlappingSeason.EndDate,
                    conflictingStatus = overlappingSeason.Status
                });
            }

            season.SeasonName = normalizedName;
            season.StartDate = startDate;
            season.EndDate = endDate;
            season.PointsPerCorrectPrediction = request.PointsPerCorrectPrediction;
            season.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new AdminActionResponse
            {
                Message = "Season updated successfully",
                Id = season.SeasonId,
                Name = season.SeasonName,
                Status = season.Status
            });
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteSeason(int id)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable);

        try
        {
            var season = await _context.Seasons
                .Include(s => s.SeasonRewardRules)
                .FirstOrDefaultAsync(s => s.SeasonId == id);

            if (season == null)
            {
                return NotFound(new AdminActionResponse
                {
                    Message = "Season not found",
                    Id = id
                });
            }

            if (season.Status != SeasonStatuses.Draft &&
                season.Status != SeasonStatuses.Cancelled)
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "Only draft or cancelled season can be deleted",
                    Id = season.SeasonId,
                    Name = season.SeasonName,
                    Status = season.Status
                });
            }

            var tournamentCount = await _context.Tournaments
                .AsNoTracking()
                .CountAsync(t => t.SeasonId == season.SeasonId);

            if (tournamentCount > 0)
            {
                return BadRequest(new
                {
                    message = "Cannot delete a season that already contains tournaments. Delete or move all tournaments first.",
                    seasonId = season.SeasonId,
                    seasonName = season.SeasonName,
                    status = season.Status,
                    tournamentCount
                });
            }

            var seasonRewardCount = await _context.SeasonRewards
                .AsNoTracking()
                .CountAsync(r => r.SeasonId == season.SeasonId);

            if (seasonRewardCount > 0)
            {
                return BadRequest(new
                {
                    message = "Cannot delete a season that already has awarded season rewards.",
                    seasonId = season.SeasonId,
                    seasonName = season.SeasonName,
                    status = season.Status,
                    seasonRewardCount
                });
            }

            var appliedRewardCount = await _context.SeasonRewards
                .AsNoTracking()
                .CountAsync(r => r.AppliedToSeasonId == season.SeasonId);

            if (appliedRewardCount > 0)
            {
                return BadRequest(new
                {
                    message = "Cannot delete this season because bonus rewards from another season were applied to it.",
                    seasonId = season.SeasonId,
                    seasonName = season.SeasonName,
                    status = season.Status,
                    appliedRewardCount
                });
            }

            var deletedRewardRuleCount = season.SeasonRewardRules.Count;

            if (deletedRewardRuleCount > 0)
            {
                _context.SeasonRewardRules.RemoveRange(season.SeasonRewardRules);
            }

            _context.Seasons.Remove(season);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new
            {
                message = "Season deleted successfully",
                seasonId = season.SeasonId,
                seasonName = season.SeasonName,
                previousStatus = season.Status,
                deletedRewardRuleCount
            });
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    [HttpGet("{id:int}/leaderboard")]
    public async Task<IActionResult> GetSeasonLeaderboard(
        int id,
        [FromQuery] int limit = 100)
    {
        var exists = await _context.Seasons
            .AsNoTracking()
            .AnyAsync(s => s.SeasonId == id);

        if (!exists)
        {
            return NotFound(new AdminActionResponse
            {
                Message = "Season not found",
                Id = id
            });
        }

        limit = Math.Clamp(limit, 1, 500);

        var leaderboard = await _leaderboardService
            .GetPredictorLeaderboardAsync(limit, id);

        return Ok(new
        {
            SeasonId = id,
            Items = leaderboard
        });
    }

    [HttpGet("{id:int}/reward-rules")]
    public async Task<IActionResult> GetRewardRules(int id)
    {
        var season = await _context.Seasons
            .AsNoTracking()
            .Where(s => s.SeasonId == id)
            .Select(s => new
            {
                s.SeasonId,
                s.SeasonName,
                s.Status,
                Rules = s.SeasonRewardRules
                    .OrderBy(r => r.RankPosition)
                    .Select(r => new
                    {
                        r.SeasonRewardRuleId,
                        r.RankPosition,
                        r.RewardName,
                        r.RewardDescription,
                        r.BonusPoints,
                        r.RewardItemId,
                        RewardItemName = r.RewardItem == null ? null : r.RewardItem.Name,
                        RewardItemSku = r.RewardItem == null ? null : r.RewardItem.Sku,
                        RewardItemIsActive = r.RewardItem == null ? (bool?)null : r.RewardItem.IsActive,
                        RewardItemAvailableQuantity = r.RewardItem == null
                            ? (int?)null
                            : r.RewardItem.StockQuantity - r.RewardItem.ReservedQuantity - r.RewardItem.DeliveredQuantity,
                        r.Quantity
                    })
            })
            .FirstOrDefaultAsync();

        if (season == null)
        {
            return NotFound(new AdminActionResponse
            {
                Message = "Season not found",
                Id = id
            });
        }

        return Ok(season);
    }

    [HttpPut("{id:int}/reward-rules")]
    public async Task<IActionResult> UpsertRewardRules(
        int id,
        [FromBody] UpsertSeasonRewardRulesRequest request)
    {
        var season = await _context.Seasons
            .Include(s => s.SeasonRewardRules)
            .FirstOrDefaultAsync(s => s.SeasonId == id);

        if (season == null)
        {
            return NotFound(new AdminActionResponse
            {
                Message = "Season not found",
                Id = id
            });
        }

        if (season.Status != SeasonStatuses.Draft)
        {
            return BadRequest(new AdminActionResponse
            {
                Message = "Reward rules can only be changed while the season is in Draft status",
                Id = season.SeasonId,
                Name = season.SeasonName,
                Status = season.Status
            });
        }

        if (request.Rules == null || request.Rules.Count == 0)
        {
            return BadRequest(new
            {
                message = "Reward rules are required"
            });
        }

        if (request.Rules.Count > MaxRewardRules)
        {
            return BadRequest(new
            {
                message = $"A season can have at most {MaxRewardRules} reward rules"
            });
        }

        var duplicateRank = request.Rules
            .GroupBy(r => r.RankPosition)
            .FirstOrDefault(g => g.Count() > 1);

        if (duplicateRank != null)
        {
            return BadRequest(new
            {
                message = $"Duplicate reward rule for rank {duplicateRank.Key}"
            });
        }

        var orderedRanks = request.Rules
            .Select(r => r.RankPosition)
            .OrderBy(rank => rank)
            .ToArray();

        for (var index = 0; index < orderedRanks.Length; index++)
        {
            var expectedRank = index + 1;
            if (orderedRanks[index] != expectedRank)
            {
                return BadRequest(new
                {
                    message = $"Reward ranks must be consecutive from 1. Missing or invalid rank: {expectedRank}"
                });
            }
        }

        foreach (var rule in request.Rules)
        {
            if (rule.RankPosition <= 0 || rule.RankPosition > MaxRewardRules)
            {
                return BadRequest(new
                {
                    message = $"Rank position must be between 1 and {MaxRewardRules}"
                });
            }

            if (string.IsNullOrWhiteSpace(rule.RewardName))
            {
                return BadRequest(new
                {
                    message = $"Reward name is required for rank {rule.RankPosition}"
                });
            }

            if (rule.RewardName.Trim().Length > 200)
            {
                return BadRequest(new
                {
                    message = $"Reward name for rank {rule.RankPosition} cannot exceed 200 characters"
                });
            }

            if (!string.IsNullOrWhiteSpace(rule.RewardDescription) &&
                rule.RewardDescription.Trim().Length > 1000)
            {
                return BadRequest(new
                {
                    message = $"Reward description for rank {rule.RankPosition} cannot exceed 1000 characters"
                });
            }

            if (rule.BonusPoints < 0 || rule.BonusPoints > MaxRewardBonusPoints)
            {
                return BadRequest(new
                {
                    message = $"Bonus points must be between 0 and {MaxRewardBonusPoints:N0}"
                });
            }
            if (rule.Quantity <= 0 || rule.Quantity > 100)
            {
                return BadRequest(new { message = $"Reward quantity for rank {rule.RankPosition} must be between 1 and 100." });
            }
            if (rule.RewardItemId.HasValue)
            {
                var rewardItemExists = await _context.RewardItems.AsNoTracking().AnyAsync(item =>
                    item.RewardItemId == rule.RewardItemId.Value && item.IsActive);
                if (!rewardItemExists)
                    return BadRequest(new { message = $"Reward item for rank {rule.RankPosition} was not found or is inactive." });
            }
        }

        var now = DateTime.UtcNow;
        var incomingRanks = request.Rules
            .Select(r => r.RankPosition)
            .ToHashSet();

        var rulesToRemove = season.SeasonRewardRules
            .Where(r => !incomingRanks.Contains(r.RankPosition))
            .ToList();

        _context.SeasonRewardRules.RemoveRange(rulesToRemove);

        foreach (var requestRule in request.Rules)
        {
            var existingRule = season.SeasonRewardRules
                .FirstOrDefault(r => r.RankPosition == requestRule.RankPosition);

            if (existingRule == null)
            {
                _context.SeasonRewardRules.Add(new SeasonRewardRule
                {
                    SeasonId = season.SeasonId,
                    RankPosition = requestRule.RankPosition,
                    RewardName = requestRule.RewardName.Trim(),
                    RewardDescription = string.IsNullOrWhiteSpace(requestRule.RewardDescription)
                        ? null
                        : requestRule.RewardDescription.Trim(),
                    BonusPoints = requestRule.BonusPoints,
                    RewardItemId = requestRule.RewardItemId,
                    Quantity = requestRule.Quantity,
                    CreatedAt = now
                });
            }
            else
            {
                existingRule.RewardName = requestRule.RewardName.Trim();
                existingRule.RewardDescription = string.IsNullOrWhiteSpace(requestRule.RewardDescription)
                    ? null
                    : requestRule.RewardDescription.Trim();
                existingRule.BonusPoints = requestRule.BonusPoints;
                existingRule.RewardItemId = requestRule.RewardItemId;
                existingRule.Quantity = requestRule.Quantity;
                existingRule.UpdatedAt = now;
            }
        }

        season.UpdatedAt = now;

        await _context.SaveChangesAsync();

        return Ok(new AdminActionResponse
        {
            Message = "Season reward rules updated successfully",
            Id = season.SeasonId,
            Name = season.SeasonName,
            Status = season.Status
        });
    }

    [HttpPut("{id:int}/activate")]
    public async Task<IActionResult> ActivateSeason(int id)
    {
        if (!User.TryGetUserId(out var adminId)) return Unauthorized();

        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable);

        try
        {
            var season = await _context.Seasons
                .FirstOrDefaultAsync(s => s.SeasonId == id);

            if (season == null)
            {
                return NotFound(new AdminActionResponse
                {
                    Message = "Season not found",
                    Id = id
                });
            }

            if (season.Status == SeasonStatuses.Active)
            {
                return Ok(new AdminActionResponse
                {
                    Message = "Season is already active",
                    Id = season.SeasonId,
                    Name = season.SeasonName,
                    Status = season.Status
                });
            }

            if (season.Status != SeasonStatuses.Draft)
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "Only draft season can be activated",
                    Id = season.SeasonId,
                    Name = season.SeasonName,
                    Status = season.Status
                });
            }

            var activeSeason = await _context.Seasons
                .AsNoTracking()
                .Where(s => s.SeasonId != season.SeasonId && s.Status == SeasonStatuses.Active)
                .Select(s => new
                {
                    s.SeasonId,
                    s.SeasonName,
                    s.StartDate,
                    s.EndDate
                })
                .FirstOrDefaultAsync();

            if (activeSeason != null)
            {
                return BadRequest(new
                {
                    message = "Another season is already active. Close it before activating a new season.",
                    activeSeasonId = activeSeason.SeasonId,
                    activeSeasonName = activeSeason.SeasonName,
                    activeSeasonStartDate = activeSeason.StartDate,
                    activeSeasonEndDate = activeSeason.EndDate
                });
            }

            var previousClosedSeason = await _context.Seasons
                .AsNoTracking()
                .Where(item =>
                    item.SeasonId != season.SeasonId &&
                    item.Status == SeasonStatuses.Closed)
                .OrderByDescending(item => item.EndDate)
                .ThenByDescending(item => item.SeasonId)
                .Select(item => new { item.SeasonId, item.SeasonName })
                .FirstOrDefaultAsync();

            // Normalize bonus flags before opening the new season.
            // Older databases could contain a reward marked as applied to a season that
            // was later removed manually. Such an orphan must become pending again;
            // otherwise the bonus can never be carried into a future season.
            var previousSeasonRewards = previousClosedSeason == null
                ? new List<SeasonReward>()
                : await _context.SeasonRewards
                    .Where(item => item.SeasonId == previousClosedSeason.SeasonId)
                    .OrderBy(item => item.SeasonRewardId)
                    .ToListAsync();

            var appliedTargetSeasonIds = previousSeasonRewards
                .Where(item => item.AppliedToSeasonId.HasValue)
                .Select(item => item.AppliedToSeasonId!.Value)
                .Distinct()
                .ToArray();

            var existingAppliedTargetSeasonIds = appliedTargetSeasonIds.Length == 0
                ? new HashSet<int>()
                : (await _context.Seasons
                    .AsNoTracking()
                    .Where(item => appliedTargetSeasonIds.Contains(item.SeasonId))
                    .Select(item => item.SeasonId)
                    .ToListAsync())
                    .ToHashSet();

            var repairedOrphanBonusCount = 0;
            var normalizedAppliedBonusCount = 0;

            foreach (var reward in previousSeasonRewards)
            {
                if (reward.AppliedToSeasonId.HasValue)
                {
                    if (existingAppliedTargetSeasonIds.Contains(reward.AppliedToSeasonId.Value))
                    {
                        // The target season still exists, so the reward is already consumed.
                        if (!reward.IsBonusApplied)
                        {
                            reward.IsBonusApplied = true;
                            normalizedAppliedBonusCount++;
                        }

                        continue;
                    }

                    // The recorded target season no longer exists. Restore this reward so
                    // the next activation can apply it exactly once using the idempotency key.
                    reward.IsBonusApplied = false;
                    reward.AppliedToSeasonId = null;
                    reward.AppliedAt = null;
                    repairedOrphanBonusCount++;
                    continue;
                }

                if (reward.IsBonusApplied)
                {
                    // An applied flag without a target season is also inconsistent.
                    reward.IsBonusApplied = false;
                    reward.AppliedAt = null;
                    repairedOrphanBonusCount++;
                }
            }

            var bonusRewards = previousSeasonRewards
                .Where(item =>
                    item.BonusPoints > 0 &&
                    !item.IsBonusApplied &&
                    item.AppliedToSeasonId == null)
                .OrderBy(item => item.SeasonRewardId)
                .ToList();

            var rewardsBySpectator = bonusRewards
                .GroupBy(item => item.SpectatorId)
                .ToDictionary(group => group.Key, group => group.ToList());

            var spectators = await _context.Users
                .Where(u => u.Role == UserRoles.Spectator)
                .OrderBy(u => u.UserId)
                .ToListAsync();

            var now = _dateTimeProvider.UtcNow;
            var appliedBonusRewardCount = 0;
            var totalBonusPoints = 0;

            foreach (var spectator in spectators)
            {
                var wallet = await _spectatorWalletService.OpenWalletForSeasonAsync(
                    season.SeasonId,
                    spectator,
                    SpectatorBettingRules.InitialBettingPoints,
                    now);

                if (!rewardsBySpectator.TryGetValue(spectator.UserId, out var spectatorRewards))
                {
                    continue;
                }

                foreach (var reward in spectatorRewards)
                {
                    var result = await _spectatorWalletService.ApplyAsync(
                        wallet,
                        spectator,
                        PointTransactionTypes.NextSeasonBonus,
                        reward.BonusPoints,
                        scoreDelta: 0,
                        idempotencyKey: $"NEXT_SEASON_BONUS_{reward.SeasonRewardId}_{season.SeasonId}",
                        referenceType: "SeasonReward",
                        referenceId: reward.SeasonRewardId,
                        description: $"Bonus carried from season #{reward.SeasonId}.",
                        now: now);

                    if (!result.AlreadyApplied)
                    {
                        totalBonusPoints += reward.BonusPoints;
                    }

                    reward.IsBonusApplied = true;
                    reward.AppliedToSeasonId = season.SeasonId;
                    reward.AppliedAt = now;
                    appliedBonusRewardCount++;
                }
            }

            season.Status = SeasonStatuses.Active;
            season.UpdatedAt = now;

            // Activation does not require today to fall inside the season's own
            // StartDate-EndDate range (an admin may legitimately prep/activate a
            // season a little early). We do not block on this, but the mismatch is
            // surfaced below (outOfDateRangeWarning) so it isn't silently invisible,
            // and it's captured in the audit log for later troubleshooting.
            var localNow = _dateTimeProvider.GetLocalNow(_dateTimeProvider.TimeZoneId);
            var isOutsideDateRange = localNow.Date < season.StartDate.Date || localNow.Date > season.EndDate.Date;

            await _auditService.WriteAsync(adminId, AuditActionTypes.StatusChange,
                "Season", season.SeasonId.ToString(),
                new { Status = SeasonStatuses.Draft },
                new
                {
                    Status = season.Status,
                    season.StartDate,
                    season.EndDate,
                    ActivatedOn = localNow.Date,
                    IsOutsideDateRange = isOutsideDateRange
                },
                "Season activated");

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new
            {
                message = "Season activated successfully. A separate betting wallet was opened for every spectator.",
                seasonId = season.SeasonId,
                seasonName = season.SeasonName,
                status = season.Status,
                openedWalletCount = spectators.Count,
                initialBettingPoints = SpectatorBettingRules.InitialBettingPoints,
                previousSeasonId = previousClosedSeason?.SeasonId,
                previousSeasonName = previousClosedSeason?.SeasonName,
                appliedBonusRewardCount,
                totalBonusPoints,
                repairedOrphanBonusCount,
                normalizedAppliedBonusCount,
                outOfDateRangeWarning = isOutsideDateRange
                    ? $"Activated outside the season's configured date range ({season.StartDate:yyyy-MM-dd} to {season.EndDate:yyyy-MM-dd})."
                    : null
            });
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    [HttpPut("{id:int}/close")]
    public async Task<IActionResult> CloseSeason(int id)
    {
        if (!User.TryGetUserId(out var adminId)) return Unauthorized();

        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable);

        try
        {
            var season = await _context.Seasons
                .FirstOrDefaultAsync(s => s.SeasonId == id);

            if (season == null)
            {
                return NotFound(new AdminActionResponse
                {
                    Message = "Season not found",
                    Id = id
                });
            }

            if (season.Status == SeasonStatuses.Closed)
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "Season is already closed",
                    Id = season.SeasonId,
                    Name = season.SeasonName,
                    Status = season.Status
                });
            }

            if (season.Status != SeasonStatuses.Active)
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "Only active season can be closed",
                    Id = season.SeasonId,
                    Name = season.SeasonName,
                    Status = season.Status
                });
            }

            var localNow = _dateTimeProvider.GetLocalNow(_dateTimeProvider.TimeZoneId);
            if (localNow.Date < season.EndDate.Date)
            {
                return BadRequest(new
                {
                    message = "The season cannot be closed before its configured end date.",
                    seasonId = season.SeasonId,
                    endDate = season.EndDate,
                    currentDate = localNow.Date
                });
            }

            var blockingTournament = await _context.Tournaments
                .AsNoTracking()
                .Where(t =>
                    t.SeasonId == season.SeasonId &&
                    t.Status != TournamentStatuses.Completed &&
                    t.Status != TournamentStatuses.Cancelled)
                .OrderBy(t => t.TournamentId)
                .Select(t => new
                {
                    t.TournamentId,
                    t.TournamentName,
                    t.Status
                })
                .FirstOrDefaultAsync();

            if (blockingTournament != null)
            {
                return BadRequest(new
                {
                    message = "Cannot close the season because it still contains an unfinished tournament.",
                    seasonId = season.SeasonId,
                    tournamentId = blockingTournament.TournamentId,
                    tournamentName = blockingTournament.TournamentName,
                    tournamentStatus = blockingTournament.Status
                });
            }

            var unresolvedPredictionCount = await _context.RacePredictions
                .AsNoTracking()
                .CountAsync(p =>
                    p.Race.Tournament.SeasonId == season.SeasonId &&
                    (p.Status == RacePredictionStatuses.Pending ||
                     p.Status == RacePredictionStatuses.Locked));

            if (unresolvedPredictionCount > 0)
            {
                return BadRequest(new
                {
                    message = "Cannot close the season because some predictions have not been evaluated or cancelled.",
                    seasonId = season.SeasonId,
                    unresolvedPredictionCount
                });
            }

            var rewardRules = await _context.SeasonRewardRules
                .Include(r => r.RewardItem)
                .Where(r => r.SeasonId == season.SeasonId)
                .OrderBy(r => r.RankPosition)
                .ToListAsync();

            if (rewardRules.Count == 0)
            {
                return BadRequest(new
                {
                    message = "Configure at least one reward rule before closing the season.",
                    seasonId = season.SeasonId
                });
            }

            var existingRewardCount = await _context.SeasonRewards
                .AsNoTracking()
                .CountAsync(r => r.SeasonId == season.SeasonId);

            if (existingRewardCount > 0)
            {
                return Conflict(new
                {
                    message = "Season rewards were already generated. They cannot be deleted and regenerated.",
                    seasonId = season.SeasonId,
                    existingRewardCount
                });
            }

            var leaderboard = await _leaderboardService
                .GetPredictorLeaderboardAsync(int.MaxValue, season.SeasonId);

            var now = _dateTimeProvider.UtcNow;
            var rankBySpectator = leaderboard.ToDictionary(item => item.SpectatorId, item => item.Rank);

            var wallets = await _context.SpectatorSeasonWallets
                .Where(item => item.SeasonId == season.SeasonId)
                .ToListAsync();

            foreach (var wallet in wallets)
            {
                wallet.FinalBettingPoints = wallet.CurrentBettingPoints;
                wallet.FinalSeasonScore = wallet.SeasonScore;
                wallet.FinalRank = rankBySpectator.TryGetValue(wallet.SpectatorId, out var finalRank)
                    ? finalRank
                    : null;
                wallet.Status = SeasonWalletStatuses.Settled;
                wallet.FrozenAt = now;
                wallet.SettledAt = now;
            }

            // users.betting_points is only the mirror of the currently active season wallet.
            // Once a season is closed there is no spendable wallet, so the visible balance is 0.
            // The closed season's real ending balance remains preserved in FinalBettingPoints.
            var spectators = await _context.Users
                .Where(item => item.Role == UserRoles.Spectator)
                .ToListAsync();

            foreach (var spectator in spectators)
            {
                spectator.BettingPoints = 0;
                spectator.UpdatedAt = now;
            }

            var winnersByRank = leaderboard
                .GroupBy(item => item.Rank)
                .ToDictionary(group => group.Key, group => group.ToList());

            // A real tie receives the same reward for that rank. Validate the total
            // physical inventory for every tied winner before changing season data.
            var physicalRequirements = rewardRules
                .Where(rule =>
                    rule.RewardItem != null &&
                    winnersByRank.ContainsKey(rule.RankPosition))
                .GroupBy(rule => rule.RewardItemId!.Value)
                .Select(group => new
                {
                    RewardItem = group.First().RewardItem!,
                    RequiredQuantity = group.Sum(rule =>
                        winnersByRank[rule.RankPosition].Count * Math.Max(1, rule.Quantity)),
                    Ranks = group.Select(rule => rule.RankPosition).OrderBy(rank => rank).ToArray()
                })
                .ToList();

            foreach (var requirement in physicalRequirements)
            {
                var availableQuantity = RewardInventoryService.Available(requirement.RewardItem);
                if (availableQuantity < requirement.RequiredQuantity)
                {
                    return BadRequest(new
                    {
                        message = "Cannot close the season because tied winners require more physical reward inventory than is available.",
                        seasonId = season.SeasonId,
                        rewardItemId = requirement.RewardItem.RewardItemId,
                        rewardItemName = requirement.RewardItem.Name,
                        affectedRanks = requirement.Ranks,
                        requiredQuantity = requirement.RequiredQuantity,
                        availableQuantity,
                        shortage = requirement.RequiredQuantity - availableQuantity
                    });
                }
            }

            season.Status = SeasonStatuses.Settling;
            season.UpdatedAt = now;

            var rewardCount = 0;
            var generatedRewards = new List<SeasonReward>();

            foreach (var rule in rewardRules)
            {
                if (!winnersByRank.TryGetValue(rule.RankPosition, out var tiedWinners))
                    continue;

                foreach (var item in tiedWinners)
                {
                    var seasonReward = new SeasonReward
                    {
                        SeasonId = season.SeasonId,
                        SpectatorId = item.SpectatorId,
                        RankPosition = item.Rank,
                        FinalPoints = item.Points,
                        RewardName = rule.RewardName,
                        RewardDescription = rule.RewardDescription,
                        BonusPoints = rule.BonusPoints,
                        RewardItemId = rule.RewardItemId,
                        Quantity = Math.Max(1, rule.Quantity),
                        IsBonusApplied = false,
                        AppliedToSeasonId = null,
                        AppliedAt = null,
                        Status = SeasonRewardStatuses.Eligible,
                        AwardedAt = now,
                        ClaimDeadline = now.AddDays(30)
                    };
                    _context.SeasonRewards.Add(seasonReward);
                    generatedRewards.Add(seasonReward);

                    if (rule.RewardItem != null)
                    {
                        await _rewardInventoryService.ReserveAsync(
                            rule.RewardItem,
                            seasonReward,
                            seasonReward.Quantity,
                            adminId,
                            now);
                    }

                    _context.Notifications.Add(new Notification
                    {
                        UserId = item.SpectatorId,
                        Title = "Season Reward Available",
                        Message = $"You finished season {season.SeasonName} at rank #{item.Rank} and received {rule.RewardName}. Please claim it before the deadline.",
                        IsRead = false,
                        CreatedAt = now,
                        ActionType = "SpectatorRewards",
                        ActionUrl = "/spectator/results",
                        RelatedType = "Season",
                        RelatedId = season.SeasonId
                    });

                    rewardCount++;
                }
            }

            season.Status = SeasonStatuses.Closed;
            season.UpdatedAt = now;

            await _auditService.WriteAsync(adminId, AuditActionTypes.StatusChange,
                "Season", season.SeasonId.ToString(),
                new { Status = SeasonStatuses.Active },
                new { Status = season.Status, RewardCount = rewardCount },
                "Season closed and rewards generated");
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            var rewardEmailSentCount = 0;
            foreach (var generatedReward in generatedRewards)
            {
                if (await _rewardEmailService.TrySendAwardedAsync(generatedReward.SeasonRewardId))
                {
                    rewardEmailSentCount++;
                }
            }

            return Ok(new
            {
                message = "Season closed successfully. Season wallets were settled, the current spendable balance was reset to 0, rewards were generated, and congratulation emails were attempted.",
                seasonId = season.SeasonId,
                seasonName = season.SeasonName,
                status = season.Status,
                participantCount = leaderboard.Count,
                settledWalletCount = wallets.Count,
                resetSpectatorBalanceCount = spectators.Count,
                configuredRewardRuleCount = rewardRules.Count,
                rewardCount,
                rewardEmailSentCount,
                rewardEmailFailedCount = rewardCount - rewardEmailSentCount,
                claimDeadlineDays = 30
            });
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    [HttpPut("{id:int}/cancel")]
    public async Task<IActionResult> CancelSeason(int id)
    {
        if (!User.TryGetUserId(out var adminId)) return Unauthorized();

        var season = await _context.Seasons
            .FirstOrDefaultAsync(s => s.SeasonId == id);

        if (season == null)
        {
            return NotFound(new AdminActionResponse
            {
                Message = "Season not found",
                Id = id
            });
        }

        if (season.Status == SeasonStatuses.Cancelled)
        {
            return Ok(new AdminActionResponse
            {
                Message = "Season is already cancelled",
                Id = season.SeasonId,
                Name = season.SeasonName,
                Status = season.Status
            });
        }

        if (season.Status != SeasonStatuses.Draft)
        {
            return BadRequest(new AdminActionResponse
            {
                Message = "Only draft season can be cancelled",
                Id = season.SeasonId,
                Name = season.SeasonName,
                Status = season.Status
            });
        }

        var hasTournaments = await _context.Tournaments
            .AsNoTracking()
            .AnyAsync(t => t.SeasonId == season.SeasonId);

        if (hasTournaments)
        {
            return BadRequest(new AdminActionResponse
            {
                Message = "Cannot cancel a season that already contains tournaments",
                Id = season.SeasonId,
                Name = season.SeasonName,
                Status = season.Status
            });
        }

        var previousStatus = season.Status;
        season.Status = SeasonStatuses.Cancelled;
        season.UpdatedAt = DateTime.UtcNow;

        await _auditService.WriteAsync(adminId, AuditActionTypes.StatusChange,
            "Season", season.SeasonId.ToString(),
            new { Status = previousStatus },
            new { Status = season.Status },
            "Season cancelled");

        await _context.SaveChangesAsync();

        return Ok(new AdminActionResponse
        {
            Message = "Season cancelled successfully",
            Id = season.SeasonId,
            Name = season.SeasonName,
            Status = season.Status
        });
    }

    [HttpGet("{id:int}/rewards")]
    public async Task<IActionResult> GetSeasonRewards(int id)
    {
        var exists = await _context.Seasons
            .AsNoTracking()
            .AnyAsync(s => s.SeasonId == id);

        if (!exists)
        {
            return NotFound(new AdminActionResponse
            {
                Message = "Season not found",
                Id = id
            });
        }

        var rewards = await _context.SeasonRewards
            .AsNoTracking()
            .Where(r => r.SeasonId == id)
            .OrderBy(r => r.RankPosition)
            .Select(r => new
            {
                r.SeasonRewardId,
                r.SeasonId,
                r.SpectatorId,
                SpectatorName = r.Spectator.FullName,
                SpectatorEmail = r.Spectator.Email,
                r.RankPosition,
                r.FinalPoints,
                r.RewardName,
                r.RewardDescription,
                r.BonusPoints,
                r.RewardItemId,
                r.Quantity,
                r.InventoryReserved,
                RewardItemName = r.RewardItem == null ? null : r.RewardItem.Name,
                RewardItemSku = r.RewardItem == null ? null : r.RewardItem.Sku,
                r.IsBonusApplied,
                r.AppliedToSeasonId,
                r.AppliedAt,
                r.Status,
                r.AwardedAt,
                r.ClaimDeadline,
                r.ClaimedAt,
                r.ApprovedAt,
                r.PreparingAt,
                r.ShippedAt,
                r.DeliveredAt,
                r.RejectedAt,
                r.ReceiverName,
                r.ReceiverPhone,
                r.DeliveryAddress,
                r.AdminNote
            })
            .ToListAsync();

        return Ok(new
        {
            SeasonId = id,
            Rewards = rewards
        });
    }

    [HttpPut("rewards/{rewardId:int}/status")]
    public async Task<IActionResult> UpdateSeasonRewardStatus(
        int rewardId,
        [FromBody] UpdateSeasonRewardStatusRequest request)
    {
        if (!User.TryGetUserId(out var adminId)) return Unauthorized();
        if (!SeasonRewardStatuses.IsValid(request.Status) ||
            request.Status is SeasonRewardStatuses.Eligible or
                SeasonRewardStatuses.Claimed or
                SeasonRewardStatuses.Shipped or
                SeasonRewardStatuses.Delivered or
                SeasonRewardStatuses.Expired)
        {
            return BadRequest(new
            {
                message = "Admin status must be Approved, Preparing, or Rejected. Use the ship endpoint to send a prepared reward; the spectator confirms final delivery."
            });
        }

        var reward = await _context.SeasonRewards
            .Include(item => item.Season)
            .Include(item => item.Spectator)
            .Include(item => item.RewardItem)
            .FirstOrDefaultAsync(item => item.SeasonRewardId == rewardId);

        if (reward == null)
        {
            return NotFound(new { message = "Season reward not found.", rewardId });
        }

        var allowed = reward.Status switch
        {
            SeasonRewardStatuses.Claimed => request.Status is SeasonRewardStatuses.Approved or SeasonRewardStatuses.Rejected,
            SeasonRewardStatuses.Approved => request.Status is SeasonRewardStatuses.Preparing or SeasonRewardStatuses.Rejected,
            SeasonRewardStatuses.Preparing => request.Status == SeasonRewardStatuses.Rejected,
            _ => false
        };

        if (!allowed)
        {
            return BadRequest(new
            {
                message = $"Cannot move reward from {reward.Status} to {request.Status}.",
                rewardId,
                currentStatus = reward.Status,
                requestedStatus = request.Status
            });
        }

        var now = _dateTimeProvider.UtcNow;
        reward.Status = request.Status;
        reward.AdminNote = string.IsNullOrWhiteSpace(request.AdminNote)
            ? null
            : request.AdminNote.Trim();

        switch (request.Status)
        {
            case SeasonRewardStatuses.Approved:
                reward.ApprovedAt = now;
                break;
            case SeasonRewardStatuses.Preparing:
                reward.PreparingAt = now;
                break;
            case SeasonRewardStatuses.Rejected:
                reward.RejectedAt = now;
                if (reward.RewardItem != null)
                    await _rewardInventoryService.ReleaseAsync(reward.RewardItem, reward, adminId, now,
                        "Reward rejected by admin.");
                break;
        }

        _context.Notifications.Add(new Notification
        {
            UserId = reward.SpectatorId,
            Title = "Season Reward Updated",
            Message = $"Your reward {reward.RewardName} from season {reward.Season.SeasonName} is now {reward.Status}.",
            IsRead = false,
            CreatedAt = now,
            ActionType = "SpectatorRewards",
            ActionUrl = "/spectator/results",
            RelatedType = "SeasonReward",
            RelatedId = reward.SeasonRewardId
        });

        await _auditService.WriteAsync(adminId, AuditActionTypes.StatusChange,
            "SeasonReward", reward.SeasonRewardId.ToString(), null,
            new { reward.Status, reward.InventoryReserved }, reward.AdminNote);
        await _context.SaveChangesAsync();
        var emailSent = await _rewardEmailService.TrySendStatusUpdatedAsync(reward.SeasonRewardId);

        return Ok(new
        {
            message = "Season reward status updated successfully.",
            rewardId = reward.SeasonRewardId,
            reward.Status,
            reward.AdminNote,
            reward.ApprovedAt,
            reward.PreparingAt,
            reward.ShippedAt,
            reward.DeliveredAt,
            reward.RejectedAt,
            emailSent
        });
    }


    [HttpPut("rewards/{rewardId:int}/ship")]
    public async Task<IActionResult> ShipSeasonReward(
        int rewardId,
        [FromBody] ShipSeasonRewardRequest? request)
    {
        if (!User.TryGetUserId(out var adminId)) return Unauthorized();

        var reward = await _context.SeasonRewards
            .Include(item => item.Season)
            .Include(item => item.Spectator)
            .Include(item => item.RewardItem)
            .FirstOrDefaultAsync(item => item.SeasonRewardId == rewardId);

        if (reward == null)
            return NotFound(new { message = "Season reward not found.", rewardId });

        if (reward.Status != SeasonRewardStatuses.Preparing)
        {
            return BadRequest(new
            {
                message = "Only a prepared reward can be marked as shipped.",
                rewardId,
                status = reward.Status
            });
        }

        if (string.IsNullOrWhiteSpace(reward.DeliveryAddress) ||
            string.IsNullOrWhiteSpace(reward.ReceiverName) ||
            string.IsNullOrWhiteSpace(reward.ReceiverPhone))
        {
            return BadRequest(new
            {
                message = "Receiver name, phone, and delivery address are required before shipping."
            });
        }

        var now = _dateTimeProvider.UtcNow;
        reward.Status = SeasonRewardStatuses.Shipped;
        reward.ShippedAt = now;
        reward.AdminNote = string.IsNullOrWhiteSpace(request?.AdminNote)
            ? reward.AdminNote
            : request.AdminNote.Trim();

        // Inventory remains reserved while the parcel is in transit. It is moved
        // to Delivered only when the spectator confirms actual receipt.
        _context.Notifications.Add(new Notification
        {
            UserId = reward.SpectatorId,
            Title = "Season Reward Shipped",
            Message = $"Your reward {reward.RewardName} from season {reward.Season.SeasonName} has been shipped to {reward.DeliveryAddress}. Confirm receipt after it arrives.",
            IsRead = false,
            CreatedAt = now,
            ActionType = "SpectatorRewardShipped",
            ActionUrl = "/spectator/results",
            RelatedType = "SeasonReward",
            RelatedId = reward.SeasonRewardId
        });

        await _auditService.WriteAsync(adminId, AuditActionTypes.StatusChange,
            "SeasonReward", reward.SeasonRewardId.ToString(), null,
            new { reward.Status, reward.ShippedAt, reward.InventoryReserved }, reward.AdminNote);
        await _context.SaveChangesAsync();
        var emailSent = await _rewardEmailService.TrySendStatusUpdatedAsync(reward.SeasonRewardId);

        return Ok(new
        {
            message = "Season reward marked as shipped. Waiting for spectator delivery confirmation.",
            rewardId = reward.SeasonRewardId,
            reward.Status,
            reward.ShippedAt,
            emailSent
        });
    }

    [HttpPost("rewards/{rewardId:int}/resend-email")]
    public async Task<IActionResult> ResendSeasonRewardEmail(int rewardId)
    {
        var rewardState = await _context.SeasonRewards
            .AsNoTracking()
            .Where(item => item.SeasonRewardId == rewardId)
            .Select(item => item.Status)
            .FirstOrDefaultAsync();

        if (rewardState == null)
        {
            return NotFound(new { message = "Season reward not found.", rewardId });
        }

        var emailSent = rewardState switch
        {
            SeasonRewardStatuses.Eligible => await _rewardEmailService.TrySendAwardedAsync(rewardId),
            SeasonRewardStatuses.Claimed => await _rewardEmailService.TrySendClaimReceivedAsync(rewardId),
            _ => await _rewardEmailService.TrySendStatusUpdatedAsync(rewardId)
        };
        if (!emailSent)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                message = "Reward email could not be sent. Check SMTP configuration and application logs.",
                rewardId,
                emailSent = false
            });
        }

        return Ok(new
        {
            message = "Reward email sent successfully.",
            rewardId,
            emailSent = true
        });
    }

    private async Task<Season?> FindOverlappingSeasonAsync(
        DateTime startDate,
        DateTime endDate,
        int? excludedSeasonId = null)
    {
        var query = _context.Seasons
            .AsNoTracking()
            .Where(s => s.Status != SeasonStatuses.Cancelled);

        if (excludedSeasonId.HasValue)
        {
            query = query.Where(s => s.SeasonId != excludedSeasonId.Value);
        }

        return await query
            .Where(s => startDate <= s.EndDate && endDate >= s.StartDate)
            .OrderBy(s => s.StartDate)
            .ThenBy(s => s.SeasonId)
            .FirstOrDefaultAsync();
    }

    private IActionResult? ValidateSeasonRequest(AdminSeasonRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SeasonName))
        {
            return BadRequest(new
            {
                message = "Season name is required"
            });
        }

        var seasonNameLength = request.SeasonName.Trim().Length;

        if (seasonNameLength < 3 || seasonNameLength > 200)
        {
            return BadRequest(new
            {
                message = "Season name must be between 3 and 200 characters"
            });
        }

        if (request.StartDate == default)
        {
            return BadRequest(new
            {
                message = "Start date is required"
            });
        }

        if (request.EndDate == default)
        {
            return BadRequest(new
            {
                message = "End date is required"
            });
        }

        if (request.StartDate.Year is < 2000 or > 2100 ||
            request.EndDate.Year is < 2000 or > 2100)
        {
            return BadRequest(new
            {
                message = "Season years must be between 2000 and 2100"
            });
        }

        if (request.EndDate.Date < request.StartDate.Date)
        {
            return BadRequest(new
            {
                message = "End date must be greater than or equal to start date"
            });
        }

        var durationDays = (request.EndDate.Date - request.StartDate.Date).Days;
        if (durationDays > MaxSeasonDurationDays)
        {
            return BadRequest(new
            {
                message = $"Season duration cannot exceed {MaxSeasonDurationDays} days"
            });
        }

        if (request.PointsPerCorrectPrediction <= 0 ||
            request.PointsPerCorrectPrediction > MaxPredictionPoints)
        {
            return BadRequest(new
            {
                message = $"Points per correct prediction must be between 1 and {MaxPredictionPoints:N0}"
            });
        }

        return null;
    }
}

public class AdminSeasonRequest
{
    [Required]
    [StringLength(200, MinimumLength = 3)]
    public string SeasonName { get; set; } = string.Empty;

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    [Range(1, 1_000_000)]
    public int PointsPerCorrectPrediction { get; set; } = 100;
}

public class UpsertSeasonRewardRulesRequest
{
    [Required]
    [MinLength(1)]
    [MaxLength(100)]
    public List<SeasonRewardRuleRequest> Rules { get; set; } = new();
}

public class SeasonRewardRuleRequest
{
    [Range(1, 100)]
    public int RankPosition { get; set; }

    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string RewardName { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? RewardDescription { get; set; }

    [Range(0, 1_000_000)]
    public int BonusPoints { get; set; }

    public int? RewardItemId { get; set; }

    [Range(1, 100)]
    public int Quantity { get; set; } = 1;
}

public class ShipSeasonRewardRequest
{
    [StringLength(1000)]
    public string? AdminNote { get; set; }
}

public class UpdateSeasonRewardStatusRequest
{
    [Required]
    [StringLength(30)]
    public string Status { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? AdminNote { get; set; }
}
