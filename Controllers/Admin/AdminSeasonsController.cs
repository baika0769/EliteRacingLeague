using System.Data;
using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Admin;
using Eliteracingleague.API.Models;
using Eliteracingleague.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Controllers.Admin;

[Authorize(Roles = UserRoles.Admin)]
[ApiController]
[Route("api/admin/seasons")]
public class AdminSeasonsController : ControllerBase
{
    private readonly EliteRacingLeagueContext _context;
    private readonly SpectatorLeaderboardService _leaderboardService;

    public AdminSeasonsController(
        EliteRacingLeagueContext context,
        SpectatorLeaderboardService leaderboardService)
    {
        _context = context;
        _leaderboardService = leaderboardService;
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
                        r.BonusPoints
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
                        r.AwardedAt
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
                SeasonName = request.SeasonName.Trim(),
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

            season.SeasonName = request.SeasonName.Trim();
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
                        r.BonusPoints
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

        foreach (var rule in request.Rules)
        {
            if (rule.RankPosition <= 0)
            {
                return BadRequest(new
                {
                    message = "Rank position must be greater than 0"
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

            if (rule.BonusPoints < 0)
            {
                return BadRequest(new
                {
                    message = "Bonus points cannot be negative"
                });
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
                .Where(s =>
                    s.SeasonId != season.SeasonId &&
                    s.Status == SeasonStatuses.Active)
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

            var now = DateTime.UtcNow;

            var spectators = await _context.Users
                .Where(u => u.Role == UserRoles.Spectator)
                .ToListAsync();

            foreach (var spectator in spectators)
            {
                spectator.BettingPoints = SpectatorBettingRules.InitialBettingPoints;
                spectator.UpdatedAt = now;
            }

            var unappliedRewards = await _context.SeasonRewards
                .Where(r =>
                    r.BonusPoints > 0 &&
                    !r.IsBonusApplied &&
                    r.AppliedToSeasonId == null &&
                    r.SeasonId != season.SeasonId)
                .ToListAsync();

            var spectatorById = spectators.ToDictionary(s => s.UserId);
            var appliedBonusRewardCount = 0;

            foreach (var reward in unappliedRewards)
            {
                if (!spectatorById.TryGetValue(reward.SpectatorId, out var spectator))
                {
                    continue;
                }

                spectator.BettingPoints += reward.BonusPoints;
                spectator.UpdatedAt = now;

                reward.IsBonusApplied = true;
                reward.AppliedToSeasonId = season.SeasonId;
                reward.AppliedAt = now;
                appliedBonusRewardCount++;
            }

            season.Status = SeasonStatuses.Active;
            season.UpdatedAt = now;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new
            {
                message = "Season activated successfully. Spectator betting points have been reset.",
                seasonId = season.SeasonId,
                seasonName = season.SeasonName,
                status = season.Status,
                resetSpectatorCount = spectators.Count,
                appliedBonusRewardCount
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
                .AsNoTracking()
                .Where(r => r.SeasonId == season.SeasonId)
                .OrderBy(r => r.RankPosition)
                .ToListAsync();

            IReadOnlyList<Eliteracingleague.API.DTOs.Spectator.PredictorLeaderboardItem> leaderboard =
                Array.Empty<Eliteracingleague.API.DTOs.Spectator.PredictorLeaderboardItem>();

            if (rewardRules.Count > 0)
            {
                var highestRewardRank = rewardRules.Max(r => r.RankPosition);
                leaderboard = await _leaderboardService
                    .GetPredictorLeaderboardAsync(highestRewardRank, season.SeasonId);
            }

            var existingRewards = await _context.SeasonRewards
                .Where(r => r.SeasonId == season.SeasonId)
                .ToListAsync();

            if (existingRewards.Count > 0)
            {
                _context.SeasonRewards.RemoveRange(existingRewards);
                await _context.SaveChangesAsync();
            }

            var now = DateTime.UtcNow;
            var leaderboardByRank = leaderboard.ToDictionary(x => x.Rank);
            var rewardCount = 0;

            foreach (var rule in rewardRules)
            {
                if (!leaderboardByRank.TryGetValue(rule.RankPosition, out var item))
                {
                    continue;
                }

                _context.SeasonRewards.Add(new SeasonReward
                {
                    SeasonId = season.SeasonId,
                    SpectatorId = item.SpectatorId,
                    RankPosition = item.Rank,
                    FinalPoints = item.Points,
                    RewardName = rule.RewardName,
                    RewardDescription = rule.RewardDescription,
                    BonusPoints = rule.BonusPoints,
                    IsBonusApplied = false,
                    AppliedToSeasonId = null,
                    AppliedAt = null,
                    Status = "Awarded",
                    AwardedAt = now
                });

                rewardCount++;
            }

            season.Status = SeasonStatuses.Closed;
            season.UpdatedAt = now;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new
            {
                message = "Season closed successfully.",
                seasonId = season.SeasonId,
                seasonName = season.SeasonName,
                status = season.Status,
                configuredRewardRuleCount = rewardRules.Count,
                rewardCount
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

        season.Status = SeasonStatuses.Cancelled;
        season.UpdatedAt = DateTime.UtcNow;

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
                r.IsBonusApplied,
                r.AppliedToSeasonId,
                r.AppliedAt,
                r.Status,
                r.AwardedAt
            })
            .ToListAsync();

        return Ok(new
        {
            SeasonId = id,
            Rewards = rewards
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

        if (request.SeasonName.Trim().Length > 200)
        {
            return BadRequest(new
            {
                message = "Season name cannot exceed 200 characters"
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

        if (request.EndDate.Date < request.StartDate.Date)
        {
            return BadRequest(new
            {
                message = "End date must be greater than or equal to start date"
            });
        }

        if (request.PointsPerCorrectPrediction <= 0)
        {
            return BadRequest(new
            {
                message = "Points per correct prediction must be greater than 0"
            });
        }

        return null;
    }
}

public class AdminSeasonRequest
{
    public string SeasonName { get; set; } = string.Empty;

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public int PointsPerCorrectPrediction { get; set; } = 100;
}

public class UpsertSeasonRewardRulesRequest
{
    public List<SeasonRewardRuleRequest> Rules { get; set; } = new();
}

public class SeasonRewardRuleRequest
{
    public int RankPosition { get; set; }

    public string RewardName { get; set; } = string.Empty;

    public string? RewardDescription { get; set; }

    public int BonusPoints { get; set; }
}