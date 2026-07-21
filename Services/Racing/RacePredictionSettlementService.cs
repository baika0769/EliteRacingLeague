using System.Data;
using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Eliteracingleague.API.Services.Racing;

public class RacePredictionSettlementService
{
    private readonly EliteRacingLeagueContext _context;
    private readonly SpectatorWalletService _walletService;

    public RacePredictionSettlementService(
        EliteRacingLeagueContext context,
        SpectatorWalletService walletService)
    {
        _context = context;
        _walletService = walletService;
    }

    public async Task<PredictionSettlementSummary> RefundForCancelledRaceAsync(
        int raceId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        IDbContextTransaction? transaction = null;
        if (_context.Database.CurrentTransaction == null)
        {
            transaction = await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
        }

        var race = await _context.Races.AsNoTracking()
            .Where(r => r.RaceId == raceId)
            .Select(r => new
            {
                r.RaceId,
                SeasonId = r.Tournament.SeasonId,
                SeasonStatus = r.Tournament.Season.Status,
                ScorePerCorrectPrediction = r.Tournament.Season.PointsPerCorrectPrediction
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Race not found.");

        if (race.SeasonStatus != SeasonStatuses.Active)
            throw new InvalidOperationException("Predictions can only be refunded while the season is active.");

        var predictions = await _context.RacePredictions
            .Include(p => p.Spectator)
            .Where(p => p.RaceId == raceId && p.Status != RacePredictionStatuses.Cancelled)
            .OrderBy(p => p.PredictionId)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var payoutReversed = 0;
        var stakesRefunded = 0;

        foreach (var prediction in predictions)
        {
            var wallet = await _walletService.GetOrCreateWalletAsync(
                race.SeasonId, prediction.Spectator, prediction.Spectator.BettingPoints,
                now, cancellationToken);

            if (prediction.Status == RacePredictionStatuses.Evaluated && prediction.PointsAwarded > 0)
            {
                var reversal = await _walletService.ApplyReversalAsync(
                    wallet,
                    prediction.Spectator,
                    PointTransactionTypes.PredictionPayoutReversal,
                    prediction.PointsAwarded,
                    await GetRecordedScoreDeltaAsync(
                        prediction.PredictionId,
                        prediction.IsCorrect == true ? race.ScorePerCorrectPrediction : 0,
                        cancellationToken),
                    $"CANCEL_RACE_PAYOUT_REVERSAL_{raceId}_{prediction.PredictionId}",
                    "RacePrediction",
                    prediction.PredictionId,
                    $"Reverse payout because race #{raceId} was cancelled. {reason}",
                    now,
                    cancellationToken);

                if (!reversal.AlreadyApplied) payoutReversed += prediction.PointsAwarded;
            }

            if (prediction.StakePoints > 0)
            {
                var refund = await _walletService.ApplyAsync(
                    wallet,
                    prediction.Spectator,
                    PointTransactionTypes.PredictionRefund,
                    prediction.StakePoints,
                    0,
                    $"CANCEL_RACE_STAKE_REFUND_{raceId}_{prediction.PredictionId}",
                    "RacePrediction",
                    prediction.PredictionId,
                    $"Refund stake because race #{raceId} was cancelled. {reason}",
                    now,
                    cancellationToken);

                if (!refund.AlreadyApplied) stakesRefunded += prediction.StakePoints;
            }

            prediction.Status = RacePredictionStatuses.Cancelled;
            prediction.IsCorrect = null;
            prediction.ActualWinnerRegistrationId = null;
            prediction.PointsAwarded = 0;
            prediction.RewardStatus = PredictionRewardStatuses.None;
            prediction.EvaluatedAt = null;
            prediction.UpdatedAt = now;

            _context.Notifications.Add(new Notification
            {
                UserId = prediction.SpectatorId,
                Title = "Race Cancelled - Points Refunded",
                Message = $"Race #{raceId} was cancelled. Your {prediction.StakePoints} stake points were refunded.",
                IsRead = false,
                CreatedAt = now,
                ActionType = "RaceCancelled",
                ActionUrl = "/spectator/predictions",
                RelatedType = "Race",
                RelatedId = raceId
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
        if (transaction != null)
        {
            await transaction.CommitAsync(cancellationToken);
            await transaction.DisposeAsync();
        }
        return new PredictionSettlementSummary(predictions.Count, stakesRefunded, payoutReversed);
    }

    public async Task<PredictionSettlementSummary> ReverseForResultCorrectionAsync(
        int raceId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        IDbContextTransaction? transaction = null;
        if (_context.Database.CurrentTransaction == null)
        {
            transaction = await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
        }

        var race = await _context.Races.AsNoTracking()
            .Where(r => r.RaceId == raceId)
            .Select(r => new
            {
                SeasonId = r.Tournament.SeasonId,
                SeasonStatus = r.Tournament.Season.Status,
                ScorePerCorrectPrediction = r.Tournament.Season.PointsPerCorrectPrediction
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Race not found.");

        if (race.SeasonStatus != SeasonStatuses.Active)
            throw new InvalidOperationException("A result cannot be reopened after the season enters settling or closes.");

        var predictions = await _context.RacePredictions
            .Include(p => p.Spectator)
            .Where(p => p.RaceId == raceId && p.Status == RacePredictionStatuses.Evaluated)
            .OrderBy(p => p.PredictionId)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var payoutReversed = 0;

        foreach (var prediction in predictions)
        {
            var wallet = await _walletService.GetOrCreateWalletAsync(
                race.SeasonId, prediction.Spectator, prediction.Spectator.BettingPoints,
                now, cancellationToken);

            if (prediction.PointsAwarded > 0)
            {
                var reversal = await _walletService.ApplyReversalAsync(
                    wallet,
                    prediction.Spectator,
                    PointTransactionTypes.ResultCorrectionAdjustment,
                    prediction.PointsAwarded,
                    await GetRecordedScoreDeltaAsync(
                        prediction.PredictionId,
                        prediction.IsCorrect == true ? race.ScorePerCorrectPrediction : 0,
                        cancellationToken),
                    $"RESULT_CORRECTION_REVERSAL_{raceId}_{prediction.PredictionId}_{prediction.EvaluatedAt:yyyyMMddHHmmss}",
                    "RacePrediction",
                    prediction.PredictionId,
                    $"Payout reversed while race result is corrected. {reason}",
                    now,
                    cancellationToken);

                if (!reversal.AlreadyApplied) payoutReversed += prediction.PointsAwarded;
            }

            prediction.Status = RacePredictionStatuses.Locked;
            prediction.IsCorrect = null;
            prediction.ActualWinnerRegistrationId = null;
            prediction.PointsAwarded = 0;
            prediction.RewardStatus = PredictionRewardStatuses.Pending;
            prediction.EvaluatedAt = null;
            prediction.LockedAt ??= now;
            prediction.UpdatedAt = now;
        }

        await _context.SaveChangesAsync(cancellationToken);
        if (transaction != null)
        {
            await transaction.CommitAsync(cancellationToken);
            await transaction.DisposeAsync();
        }
        return new PredictionSettlementSummary(predictions.Count, 0, payoutReversed);
    }

    private async Task<int> GetRecordedScoreDeltaAsync(
        int predictionId,
        int fallback,
        CancellationToken cancellationToken)
    {
        var recorded = await _context.PointTransactions
            .AsNoTracking()
            .Where(item =>
                item.ReferenceType == "RacePrediction" &&
                item.ReferenceId == predictionId &&
                item.TransactionType == PointTransactionTypes.PredictionPayout)
            .OrderByDescending(item => item.PointTransactionId)
            .Select(item => (int?)item.ScoreDelta)
            .FirstOrDefaultAsync(cancellationToken);

        return Math.Max(0, recorded ?? Math.Max(0, fallback));
    }
}

public sealed record PredictionSettlementSummary(
    int PredictionsAffected,
    int StakePointsRefunded,
    int PayoutPointsReversed);
