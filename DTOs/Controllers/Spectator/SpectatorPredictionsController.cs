using System.Data;
using System.Security.Claims;
using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Spectator;
using Eliteracingleague.API.Models;
using Eliteracingleague.API.Services.SystemTime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Eliteracingleague.API.Services;

namespace Eliteracingleague.API.Controllers.Spectator;

[Authorize(Roles = UserRoles.Spectator)]
[ApiController]
[Route("api/spectator/predictions")]
public class SpectatorPredictionsController : ControllerBase
{
    private readonly EliteRacingLeagueContext _context;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly SpectatorWalletService _spectatorWalletService;

    private static readonly string[] PredictableRegistrationStatuses =
    {
        RaceRegistrationStatuses.ReadyToRace
    };

    public SpectatorPredictionsController(
        EliteRacingLeagueContext context,
        IDateTimeProvider dateTimeProvider,
        SpectatorWalletService spectatorWalletService)
    {
        _context = context;
        _dateTimeProvider = dateTimeProvider;
        _spectatorWalletService = spectatorWalletService;
    }

    private int GetUserId()
        => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    [HttpGet("wallet")]
    public async Task<IActionResult> GetWallet()
    {
        var spectatorId = GetUserId();

        var spectator = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == spectatorId && u.Role == UserRoles.Spectator);

        if (spectator == null)
        {
            return NotFound(new { message = "Spectator wallet not found." });
        }

        var activeSeason = await _context.Seasons
            .AsNoTracking()
            .Where(item => item.Status == SeasonStatuses.Active)
            .OrderByDescending(item => item.StartDate)
            .Select(item => new { item.SeasonId, item.SeasonName })
            .FirstOrDefaultAsync();

        if (activeSeason == null)
        {
            return Ok(new
            {
                seasonId = (int?)null,
                seasonName = (string?)null,
                hasActiveSeason = false,
                bettingPoints = 0,
                seasonScore = 0,
                initialBettingPoints = 0,
                baseOpeningPoints = 0,
                carriedBonusPoints = 0,
                openingTotalPoints = 0,
                walletStatus = (string?)null,
                minimumStakePoints = SpectatorBettingRules.MinimumStakePoints,
                totalStakePoints = 0,
                totalPayoutPoints = 0,
                pendingStakePoints = 0,
                netPoints = 0,
                message = "There is no active season. The spendable wallet balance is 0 until a new season is activated."
            });
        }

        var wallet = await _context.SpectatorSeasonWallets
            .AsNoTracking()
            .FirstOrDefaultAsync(item =>
                item.SeasonId == activeSeason.SeasonId &&
                item.SpectatorId == spectatorId);

        var carriedBonusPoints = wallet == null
            ? 0
            : await _context.PointTransactions
                .AsNoTracking()
                .Where(item =>
                    item.SpectatorSeasonWalletId == wallet.SpectatorSeasonWalletId &&
                    item.TransactionType == PointTransactionTypes.NextSeasonBonus)
                .SumAsync(item => (int?)item.Amount) ?? 0;

        // Never fall back to users.betting_points here. The season wallet is the
        // source of truth; a missing wallet must display 0 instead of a stale old balance.
        var baseOpeningPoints = wallet?.OpeningBettingPoints ?? 0;

        var predictionQuery = _context.RacePredictions
            .AsNoTracking()
            .Where(p =>
                p.SpectatorId == spectatorId &&
                p.Race.Tournament.SeasonId == activeSeason.SeasonId &&
                p.Status != RacePredictionStatuses.Cancelled);

        var totalStakePoints = await predictionQuery.SumAsync(p => (int?)p.StakePoints) ?? 0;
        var totalPayoutPoints = await predictionQuery.SumAsync(p => (int?)p.PointsAwarded) ?? 0;
        var pendingStakePoints = await predictionQuery
            .Where(p => p.Status != RacePredictionStatuses.Evaluated)
            .SumAsync(p => (int?)p.StakePoints) ?? 0;

        return Ok(new
        {
            activeSeason.SeasonId,
            activeSeason.SeasonName,
            hasActiveSeason = true,
            bettingPoints = wallet?.CurrentBettingPoints ?? 0,
            seasonScore = wallet?.SeasonScore ?? 0,
            initialBettingPoints = baseOpeningPoints,
            baseOpeningPoints,
            carriedBonusPoints,
            openingTotalPoints = checked(baseOpeningPoints + carriedBonusPoints),
            walletStatus = wallet?.Status,
            minimumStakePoints = SpectatorBettingRules.MinimumStakePoints,
            totalStakePoints,
            totalPayoutPoints,
            pendingStakePoints,
            netPoints = totalPayoutPoints - totalStakePoints
        });
    }

    [HttpPost]
    public async Task<IActionResult> CreatePrediction(CreatePredictionRequest request)
    {
        var spectatorId = GetUserId();
        var utcNow = _dateTimeProvider.UtcNow;
        var localNow = _dateTimeProvider.GetLocalNow(_dateTimeProvider.TimeZoneId);

        if (request.StakePoints < SpectatorBettingRules.MinimumStakePoints)
        {
            return BadRequest(new
            {
                error = "Invalid stake points.",
                message = $"Stake must be at least {SpectatorBettingRules.MinimumStakePoints} points.",
                minimumStakePoints = SpectatorBettingRules.MinimumStakePoints
            });
        }

        await using var transaction = await _context.Database
            .BeginTransactionAsync(IsolationLevel.Serializable);

        var spectator = await _context.Users
            .FirstOrDefaultAsync(u => u.UserId == spectatorId && u.Role == UserRoles.Spectator);

        if (spectator == null)
        {
            return Unauthorized(new { message = "Spectator account not found." });
        }

        var tournament = await _context.Tournaments
    .AsNoTracking()
    .Include(t => t.Season)
    .FirstOrDefaultAsync(t => t.TournamentId == request.TournamentId);

        if (tournament == null)
        {
            return NotFound("Tournament not found.");
        }

        if (tournament.Season == null)
        {
            return BadRequest(new
            {
                code = "SEASON_NOT_ASSIGNED",
                message = "Tournament has not been assigned to a season.",
                tournamentId = tournament.TournamentId
            });
        }

        if (tournament.Season.Status != SeasonStatuses.Active)
        {
            return BadRequest(new
            {
                code = "SEASON_NOT_ACTIVE",
                message = "Prediction is only allowed in an active season.",
                tournamentId = tournament.TournamentId,
                seasonId = tournament.Season.SeasonId,
                seasonName = tournament.Season.SeasonName,
                seasonStatus = tournament.Season.Status
            });
        }

        var wallet = await _spectatorWalletService.GetOrCreateWalletAsync(
            tournament.SeasonId,
            spectator,
            spectator.BettingPoints,
            utcNow);

        if (wallet.CurrentBettingPoints < request.StakePoints)
        {
            return BadRequest(new
            {
                error = "Insufficient betting points.",
                message = "You do not have enough points to place this bet.",
                bettingPoints = wallet.CurrentBettingPoints,
                requestedStakePoints = request.StakePoints
            });
        }

        if (tournament.Status is not TournamentStatuses.OpenRegistration
            and not TournamentStatuses.ClosedRegistration)
        {
            return BadRequest(new
            {
                code = "TOURNAMENT_NOT_OPEN_FOR_PREDICTION",
                message = "Prediction is only allowed while the tournament is open or closed for registration and before the race starts.",
                tournamentId = tournament.TournamentId,
                tournamentStatus = tournament.Status
            });
        }

        var race = await _context.Races
            .AsNoTracking()
            .Where(r =>
                r.TournamentId == request.TournamentId &&
                r.Status != RaceStatuses.Cancelled)
            .OrderBy(r => r.RaceDate)
            .FirstOrDefaultAsync();

        if (race == null)
        {
            return NotFound("Race not found.");
        }

        if (RaceStatuses.IsClosedForPrediction(race.Status))
        {
            return BadRequest("Prediction is not allowed for this race status.");
        }

        if (!race.PredictionDeadline.HasValue)
        {
            return BadRequest(new
            {
                code = "PREDICTION_DEADLINE_NOT_CONFIGURED",
                message = "Prediction deadline has not been configured for this race.",
                raceId = race.RaceId
            });
        }

        if (localNow >= race.RaceDate)
        {
            return BadRequest(new
            {
                code = "RACE_ALREADY_STARTED",
                message = "Prediction is not allowed after the race start time.",
                raceId = race.RaceId,
                raceDate = race.RaceDate
            });
        }

        if (localNow >= race.PredictionDeadline.Value)
        {
            return BadRequest(new
            {
                code = "PREDICTION_DEADLINE_PASSED",
                message = "Prediction deadline has passed.",
                raceId = race.RaceId,
                predictionDeadline = race.PredictionDeadline.Value
            });
        }

        var existing = await _context.RacePredictions
            .AsNoTracking()
            .AnyAsync(p =>
                p.SpectatorId == spectatorId &&
                p.Status != RacePredictionStatuses.Cancelled &&
                p.Race.TournamentId == request.TournamentId);

        if (existing)
        {
            return Conflict(new
            {
                error = "You have already predicted for this tournament.",
                message = "You have already predicted for this tournament."
            });
        }

        var registration = await _context.RaceRegistrations
            .AsNoTracking()
            .Where(r =>
                r.RaceId == race.RaceId &&
                r.HorseId == request.PredictedHorseId &&
                PredictableRegistrationStatuses.Contains(r.Status))
            .Select(r => new
            {
                r.RegistrationId,
                r.HorseId,
                HorseName = r.Horse.HorseName,
                HorseImageUrl = r.Horse.ImageUrl,
                OwnerName = r.Owner.Owner.FullName,
                JockeyName = r.Jockey == null
                    ? null
                    : r.Jockey.JockeyNavigation.FullName
            })
            .FirstOrDefaultAsync();

        if (registration == null)
        {
            return BadRequest(new
            {
                error = "Horse is not registered in this tournament.",
                message = "Horse is not registered in this tournament."
            });
        }

        var prediction = new RacePrediction
        {
            RaceId = race.RaceId,
            SpectatorId = spectatorId,
            PredictedRegistrationId = registration.RegistrationId,
            Status = RacePredictionStatuses.Pending,
            IsCorrect = null,
            PointsAwarded = 0,
            StakePoints = request.StakePoints,
            RewardStatus = PredictionRewardStatuses.None,
            PredictedAt = utcNow,
            CreatedAt = utcNow
        };

        _context.RacePredictions.Add(prediction);
        await _context.SaveChangesAsync();

        var walletResult = await _spectatorWalletService.ApplyAsync(
            wallet,
            spectator,
            PointTransactionTypes.PredictionStake,
            -request.StakePoints,
            scoreDelta: 0,
            idempotencyKey: $"PREDICTION_STAKE_{prediction.PredictionId}",
            referenceType: "RacePrediction",
            referenceId: prediction.PredictionId,
            description: $"Stake for prediction #{prediction.PredictionId}.",
            now: utcNow);

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return Ok(new
        {
            message = "Prediction submitted successfully. Stake points have been deducted from your season wallet.",
            predictionId = prediction.PredictionId,
            tournamentId = request.TournamentId,
            raceId = prediction.RaceId,
            predictedHorseId = registration.HorseId,
            predictedHorseName = registration.HorseName,
            predictedHorseImageUrl = registration.HorseImageUrl,
            predictedOwnerName = registration.OwnerName,
            predictedJockeyName = registration.JockeyName,
            status = prediction.Status,
            stakePoints = prediction.StakePoints,
            payoutPoints = prediction.PointsAwarded,
            bettingPoints = walletResult.BettingPoints,
            seasonScore = walletResult.SeasonScore
        });
    }

    [HttpPut("{predictionId:int}")]
    public async Task<IActionResult> UpdatePrediction(
        int predictionId,
        UpdatePredictionRequest request,
        CancellationToken cancellationToken)
    {
        var spectatorId = GetUserId();
        var utcNow = _dateTimeProvider.UtcNow;
        var localNow = _dateTimeProvider.GetLocalNow(_dateTimeProvider.TimeZoneId);

        if (request.StakePoints < SpectatorBettingRules.MinimumStakePoints)
        {
            return BadRequest(new
            {
                code = "INVALID_STAKE_POINTS",
                message = $"Stake must be at least {SpectatorBettingRules.MinimumStakePoints} points.",
                minimumStakePoints = SpectatorBettingRules.MinimumStakePoints
            });
        }

        await using var transaction = await _context.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var spectator = await _context.Users
            .FirstOrDefaultAsync(
                u => u.UserId == spectatorId && u.Role == UserRoles.Spectator,
                cancellationToken);

        if (spectator == null)
        {
            return Unauthorized(new { message = "Spectator account not found." });
        }

        var prediction = await _context.RacePredictions
            .Include(p => p.Race)
                .ThenInclude(r => r.Tournament)
                    .ThenInclude(t => t.Season)
            .FirstOrDefaultAsync(
                p => p.PredictionId == predictionId && p.SpectatorId == spectatorId,
                cancellationToken);

        if (prediction == null)
        {
            return NotFound(new { message = "Prediction not found." });
        }

        if (prediction.Status != RacePredictionStatuses.Pending)
        {
            return Conflict(new
            {
                code = "PREDICTION_NOT_EDITABLE",
                message = "Only pending predictions can be edited.",
                predictionStatus = prediction.Status
            });
        }

        var race = prediction.Race;
        var tournament = race.Tournament;

        if (tournament.Season == null || tournament.Season.Status != SeasonStatuses.Active)
        {
            return BadRequest(new
            {
                code = "SEASON_NOT_ACTIVE",
                message = "Prediction can only be edited during an active season."
            });
        }

        if (tournament.Status is not TournamentStatuses.OpenRegistration
            and not TournamentStatuses.ClosedRegistration)
        {
            return BadRequest(new
            {
                code = "TOURNAMENT_NOT_OPEN_FOR_PREDICTION",
                message = "Prediction can no longer be edited for the current tournament status.",
                tournamentStatus = tournament.Status
            });
        }

        if (RaceStatuses.IsClosedForPrediction(race.Status))
        {
            return BadRequest(new
            {
                code = "RACE_CLOSED_FOR_PREDICTION",
                message = "Prediction can no longer be edited for the current race status.",
                raceStatus = race.Status
            });
        }

        if (!race.PredictionDeadline.HasValue)
        {
            return BadRequest(new
            {
                code = "PREDICTION_DEADLINE_NOT_CONFIGURED",
                message = "Prediction deadline has not been configured for this race."
            });
        }

        if (localNow >= race.RaceDate)
        {
            return BadRequest(new
            {
                code = "RACE_ALREADY_STARTED",
                message = "Prediction cannot be edited after the race start time.",
                raceDate = race.RaceDate
            });
        }

        if (localNow >= race.PredictionDeadline.Value)
        {
            return BadRequest(new
            {
                code = "PREDICTION_DEADLINE_PASSED",
                message = "Prediction deadline has passed.",
                predictionDeadline = race.PredictionDeadline.Value
            });
        }

        var registration = await _context.RaceRegistrations
            .AsNoTracking()
            .Where(r =>
                r.RaceId == race.RaceId &&
                r.HorseId == request.PredictedHorseId &&
                PredictableRegistrationStatuses.Contains(r.Status))
            .Select(r => new
            {
                r.RegistrationId,
                r.HorseId,
                HorseName = r.Horse.HorseName,
                HorseImageUrl = r.Horse.ImageUrl,
                OwnerName = r.Owner.Owner.FullName,
                JockeyName = r.Jockey == null
                    ? null
                    : r.Jockey.JockeyNavigation.FullName
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (registration == null)
        {
            return BadRequest(new
            {
                code = "HORSE_NOT_PREDICTABLE",
                message = "The selected horse is not eligible for prediction in this race."
            });
        }

        var wallet = await _spectatorWalletService.GetOrCreateWalletAsync(
            tournament.SeasonId,
            spectator,
            spectator.BettingPoints,
            utcNow,
            cancellationToken);

        var oldStakePoints = prediction.StakePoints;
        var walletDelta = oldStakePoints - request.StakePoints;

        if (walletDelta < 0 && wallet.CurrentBettingPoints < -walletDelta)
        {
            return BadRequest(new
            {
                code = "INSUFFICIENT_BETTING_POINTS",
                message = "You do not have enough points to increase this stake.",
                bettingPoints = wallet.CurrentBettingPoints,
                additionalPointsRequired = -walletDelta
            });
        }

        prediction.PredictedRegistrationId = registration.RegistrationId;
        prediction.StakePoints = request.StakePoints;
        prediction.UpdatedAt = utcNow;

        var walletResult = new WalletMutationResult(
            wallet.CurrentBettingPoints,
            wallet.SeasonScore,
            wallet.PendingRecoveryPoints,
            false);

        if (walletDelta != 0)
        {
            walletResult = await _spectatorWalletService.ApplyAsync(
                wallet,
                spectator,
                walletDelta > 0
                    ? PointTransactionTypes.PredictionRefund
                    : PointTransactionTypes.PredictionStake,
                walletDelta,
                scoreDelta: 0,
                idempotencyKey: $"PREDICTION_EDIT_{prediction.PredictionId}_{utcNow.Ticks}",
                referenceType: "RacePrediction",
                referenceId: prediction.PredictionId,
                description: walletDelta > 0
                    ? $"Stake reduction refund for prediction #{prediction.PredictionId}."
                    : $"Additional stake for prediction #{prediction.PredictionId}.",
                now: utcNow,
                cancellationToken: cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Ok(new
        {
            message = "Prediction updated successfully.",
            predictionId = prediction.PredictionId,
            tournamentId = tournament.TournamentId,
            raceId = race.RaceId,
            predictedHorseId = registration.HorseId,
            predictedHorseName = registration.HorseName,
            predictedHorseImageUrl = registration.HorseImageUrl,
            predictedOwnerName = registration.OwnerName,
            predictedJockeyName = registration.JockeyName,
            status = prediction.Status,
            oldStakePoints,
            stakePoints = prediction.StakePoints,
            bettingPoints = walletResult.BettingPoints,
            seasonScore = walletResult.SeasonScore,
            updatedAt = prediction.UpdatedAt
        });
    }

    [HttpGet("my")]
    public async Task<IActionResult> GetMyPredictions()
    {
        var spectatorId = GetUserId();

        var predictions = await _context.RacePredictions
            .AsNoTracking()
            .Where(p => p.SpectatorId == spectatorId)
            .OrderByDescending(p => p.PredictedAt)
            .Select(p => new
            {
                predictionId = p.PredictionId,
                seasonId = p.Race.Tournament.SeasonId,
                seasonName = p.Race.Tournament.Season.SeasonName,
                tournamentId = p.Race.TournamentId,
                tournamentName = p.Race.Tournament.TournamentName,
                tournamentStatus = p.Race.Tournament.Status,
                raceId = p.RaceId,
                raceName = p.Race.RaceName,
                predictedHorseId = p.PredictedRegistration.HorseId,
                predictedRegistrationId = p.PredictedRegistrationId,
                predictedHorseName = p.PredictedRegistration.Horse.HorseName,
                predictedHorseImageUrl = p.PredictedRegistration.Horse.ImageUrl,
                predictedOwnerName = p.PredictedRegistration.Owner.Owner.FullName,
                predictedJockeyName = p.PredictedRegistration.Jockey == null
                    ? null
                    : p.PredictedRegistration.Jockey.JockeyNavigation.FullName,
                actualWinnerRegistrationId = p.ActualWinnerRegistrationId,
                actualWinnerHorseName = p.ActualWinnerRegistration != null
                    ? p.ActualWinnerRegistration.Horse.HorseName
                    : null,
                status = p.Status,
                isCorrect = p.IsCorrect,
                stakePoints = p.StakePoints,
                payoutPoints = p.PointsAwarded,
                pointsAwarded = p.PointsAwarded,
                netPoints = p.Status == RacePredictionStatuses.Cancelled
                    ? 0
                    : p.Status == RacePredictionStatuses.Evaluated
                        ? p.PointsAwarded - p.StakePoints
                        : -p.StakePoints,
                rewardAmount = p.RewardAmount,
                rewardStatus = p.RewardStatus,
                predictedAt = p.PredictedAt,
                lockedAt = p.LockedAt,
                evaluatedAt = p.EvaluatedAt
            })
            .ToListAsync();

        return Ok(predictions);
    }
}