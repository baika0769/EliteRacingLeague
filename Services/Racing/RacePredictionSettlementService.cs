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

        try
        {
            var predictions = await _context.RacePredictions
                .Include(prediction => prediction.Spectator)
                .Where(prediction =>
                    prediction.RaceId == raceId &&
                    prediction.Status != RacePredictionStatuses.Cancelled)
                .OrderBy(prediction => prediction.PredictionId)
                .ToListAsync(cancellationToken);

            if (predictions.Count == 0)
            {
                if (transaction != null)
                    await transaction.CommitAsync(cancellationToken);
                return new PredictionSettlementSummary(0, 0, 0);
            }

            var race = await GetRaceContextAsync(raceId, cancellationToken);

            var now = DateTime.UtcNow;
            var payoutReversed = 0;
            var stakesRefunded = 0;

            foreach (var prediction in predictions)
            {
                var result = await CancelPredictionCoreAsync(
                    prediction,
                    race.SeasonId,
                    race.LifecycleVersion,
                    $"Race #{raceId} was cancelled. {reason}",
                    now,
                    cancellationToken);

                payoutReversed = checked(payoutReversed + result.PayoutPointsReversed);
                stakesRefunded = checked(stakesRefunded + result.StakePointsRefunded);
            }

            await _context.SaveChangesAsync(cancellationToken);
            if (transaction != null)
                await transaction.CommitAsync(cancellationToken);

            return new PredictionSettlementSummary(
                predictions.Count,
                stakesRefunded,
                payoutReversed);
        }
        catch
        {
            if (transaction != null)
                await transaction.RollbackAsync(cancellationToken);
            throw;
        }
        finally
        {
            if (transaction != null)
                await transaction.DisposeAsync();
        }
    }

    public async Task<PredictionSettlementSummary> CancelPredictionAsync(
        int predictionId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        IDbContextTransaction? transaction = null;
        if (_context.Database.CurrentTransaction == null)
        {
            transaction = await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
        }

        try
        {
            var prediction = await _context.RacePredictions
                .Include(item => item.Spectator)
                .Include(item => item.Race)
                    .ThenInclude(race => race.Tournament)
                        .ThenInclude(tournament => tournament.Season)
                .FirstOrDefaultAsync(item => item.PredictionId == predictionId, cancellationToken)
                ?? throw new InvalidOperationException("Prediction not found.");

            if (prediction.Status == RacePredictionStatuses.Cancelled)
                return new PredictionSettlementSummary(0, 0, 0);

            if (prediction.Race.Tournament.Season.Status != SeasonStatuses.Active)
                throw new InvalidOperationException("Predictions can only be cancelled while the season is active.");

            var result = await CancelPredictionCoreAsync(
                prediction,
                prediction.Race.Tournament.SeasonId,
                prediction.Race.LifecycleVersion,
                reason,
                DateTime.UtcNow,
                cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
            if (transaction != null)
                await transaction.CommitAsync(cancellationToken);

            return new PredictionSettlementSummary(
                1,
                result.StakePointsRefunded,
                result.PayoutPointsReversed);
        }
        catch
        {
            if (transaction != null)
                await transaction.RollbackAsync(cancellationToken);
            throw;
        }
        finally
        {
            if (transaction != null)
                await transaction.DisposeAsync();
        }
    }

    public async Task<PredictionSettlementSummary> CancelForFailedPreRaceInspectionAsync(
        int raceId,
        int registrationId,
        string horseName,
        CancellationToken cancellationToken = default)
    {
        IDbContextTransaction? transaction = null;
        if (_context.Database.CurrentTransaction == null)
        {
            transaction = await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
        }

        try
        {
            var race = await GetRaceContextAsync(raceId, cancellationToken);
            var predictions = await _context.RacePredictions
                .Include(prediction => prediction.Spectator)
                .Where(prediction =>
                    prediction.RaceId == raceId &&
                    prediction.PredictedRegistrationId == registrationId &&
                    prediction.Status != RacePredictionStatuses.Cancelled)
                .OrderBy(prediction => prediction.PredictionId)
                .ToListAsync(cancellationToken);

            if (predictions.Count == 0)
            {
                if (transaction != null)
                    await transaction.CommitAsync(cancellationToken);
                return new PredictionSettlementSummary(0, 0, 0);
            }

            var now = DateTime.UtcNow;
            var payoutReversed = 0;
            var stakesRefunded = 0;
            const string notificationMessage =
                "Ngựa không vượt qua kiểm tra pre-race, tiền cược đã được hoàn.";

            foreach (var prediction in predictions)
            {
                var result = await CancelPredictionCoreAsync(
                    prediction,
                    race.SeasonId,
                    race.LifecycleVersion,
                    $"Horse {horseName} failed pre-race inspection.",
                    now,
                    cancellationToken,
                    refundBypassesRecoveryDebt: true,
                    notificationMessage: notificationMessage);

                payoutReversed = checked(payoutReversed + result.PayoutPointsReversed);
                stakesRefunded = checked(stakesRefunded + result.StakePointsRefunded);
            }

            await _context.SaveChangesAsync(cancellationToken);
            if (transaction != null)
                await transaction.CommitAsync(cancellationToken);

            return new PredictionSettlementSummary(
                predictions.Count,
                stakesRefunded,
                payoutReversed);
        }
        catch
        {
            if (transaction != null)
                await transaction.RollbackAsync(cancellationToken);
            throw;
        }
        finally
        {
            if (transaction != null)
                await transaction.DisposeAsync();
        }
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

        try
        {
            var race = await GetRaceContextAsync(raceId, cancellationToken);
            var predictions = await _context.RacePredictions
                .Include(prediction => prediction.Spectator)
                .Where(prediction =>
                    prediction.RaceId == raceId &&
                    prediction.Status == RacePredictionStatuses.Evaluated)
                .OrderBy(prediction => prediction.PredictionId)
                .ToListAsync(cancellationToken);

            var now = DateTime.UtcNow;
            var payoutReversed = 0;

            foreach (var prediction in predictions)
            {
                var wallet = await _walletService.GetOrCreateWalletAsync(
                    race.SeasonId,
                    prediction.Spectator,
                    prediction.Spectator.BettingPoints,
                    now,
                    cancellationToken);

                var settlement = await GetSettlementTransactionAsync(
                    prediction,
                    race.LifecycleVersion,
                    cancellationToken);

                if (settlement != null)
                {
                    var reversal = await _walletService.ReverseTransactionAsync(
                        wallet,
                        prediction.Spectator,
                        settlement,
                        $"PREDICTION_REVERSAL_{prediction.PredictionId}_V{race.LifecycleVersion}",
                        $"Reverse prediction settlement while race #{raceId} result is corrected. {reason}",
                        now,
                        cancellationToken);

                    if (!reversal.AlreadyApplied)
                        payoutReversed = checked(payoutReversed + Math.Max(0, settlement.RequestedAmount));
                }
                else if (prediction.IsCorrect == true && prediction.PointsAwarded > 0)
                {
                    throw new InvalidOperationException(
                        $"Winning prediction #{prediction.PredictionId} has no settlement ledger transaction to reverse.");
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
                await transaction.CommitAsync(cancellationToken);

            return new PredictionSettlementSummary(predictions.Count, 0, payoutReversed);
        }
        catch
        {
            if (transaction != null)
                await transaction.RollbackAsync(cancellationToken);
            throw;
        }
        finally
        {
            if (transaction != null)
                await transaction.DisposeAsync();
        }
    }

    private async Task<PredictionCancellationResult> CancelPredictionCoreAsync(
        RacePrediction prediction,
        int seasonId,
        int settlementVersion,
        string reason,
        DateTime now,
        CancellationToken cancellationToken,
        bool refundBypassesRecoveryDebt = false,
        string? notificationMessage = null)
    {
        var wallet = await _walletService.GetOrCreateWalletAsync(
            seasonId,
            prediction.Spectator,
            prediction.Spectator.BettingPoints,
            now,
            cancellationToken);

        var payoutReversed = 0;
        if (prediction.Status == RacePredictionStatuses.Evaluated)
        {
            var settlement = await GetSettlementTransactionAsync(
                prediction,
                settlementVersion,
                cancellationToken);

            if (settlement != null)
            {
                var reversal = await _walletService.ReverseTransactionAsync(
                    wallet,
                    prediction.Spectator,
                    settlement,
                    $"PREDICTION_REVERSAL_{prediction.PredictionId}_V{settlementVersion}",
                    $"Reverse prediction settlement because it was cancelled. {reason}",
                    now,
                    cancellationToken);

                if (!reversal.AlreadyApplied)
                    payoutReversed = Math.Max(0, settlement.RequestedAmount);
            }
            else if (prediction.IsCorrect == true && prediction.PointsAwarded > 0)
            {
                throw new InvalidOperationException(
                    $"Winning prediction #{prediction.PredictionId} has no settlement ledger transaction to reverse.");
            }
        }

        var stakeRefunded = 0;
        if (prediction.StakePoints > 0)
        {
            var refund = await _walletService.ApplyAsync(
                wallet,
                prediction.Spectator,
                PointTransactionTypes.PredictionRefund,
                prediction.StakePoints,
                scoreDelta: 0,
                idempotencyKey: $"PREDICTION_REFUND_{prediction.PredictionId}",
                referenceType: "RacePrediction",
                referenceId: prediction.PredictionId,
                description: $"Refund stake for cancelled prediction #{prediction.PredictionId}. {reason}",
                now: now,
                cancellationToken: cancellationToken,
                settleRecoveryDebt: !refundBypassesRecoveryDebt);

            if (!refund.AlreadyApplied)
                stakeRefunded = prediction.StakePoints;
        }

        prediction.Status = RacePredictionStatuses.Cancelled;
        prediction.IsCorrect = null;
        prediction.ActualWinnerRegistrationId = null;
        prediction.PointsAwarded = 0;
        prediction.RewardAmount = null;
        prediction.RewardStatus = PredictionRewardStatuses.None;
        prediction.EvaluatedAt = null;
        prediction.UpdatedAt = now;

        _context.Notifications.Add(new Notification
        {
            UserId = prediction.SpectatorId,
            Title = "Prediction Cancelled - Stake Refunded",
            Message = notificationMessage ??
                $"Prediction #{prediction.PredictionId} was cancelled. Its settlement was reversed and the {prediction.StakePoints}-point stake was refunded.",
            IsRead = false,
            CreatedAt = now,
            ActionType = "SpectatorPredictions",
            ActionUrl = "/spectator/predictions",
            RelatedType = "RacePrediction",
            RelatedId = prediction.PredictionId
        });

        return new PredictionCancellationResult(stakeRefunded, payoutReversed);
    }

    private async Task<RaceSettlementContext> GetRaceContextAsync(
        int raceId,
        CancellationToken cancellationToken)
    {
        var race = await _context.Races
            .AsNoTracking()
            .Where(item => item.RaceId == raceId)
            .Select(item => new RaceSettlementContext(
                item.RaceId,
                item.Tournament.SeasonId,
                item.Tournament.Season.Status,
                item.LifecycleVersion))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Race not found.");

        if (race.SeasonStatus != SeasonStatuses.Active)
            throw new InvalidOperationException("Predictions can only be settled while the season is active.");

        return race;
    }

    private async Task<PointTransaction?> GetSettlementTransactionAsync(
        RacePrediction prediction,
        int settlementVersion,
        CancellationToken cancellationToken)
    {
        var expectedKey = prediction.IsCorrect == true
            ? $"PREDICTION_WIN_{prediction.PredictionId}_V{settlementVersion}"
            : $"PREDICTION_LOSS_{prediction.PredictionId}_V{settlementVersion}";

        var exact = await _context.PointTransactions
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.IdempotencyKey == expectedKey, cancellationToken);

        if (exact != null)
            return exact;

        // Compatibility for active predictions settled before the fixed-payout
        // cutover. The audited cutover adjustment and any legacy reversal are
        // aggregated so a later cancellation/correction reverses the current net
        // settlement, not only the original pari-mutuel payout row.
        var legacyRows = await _context.PointTransactions
            .AsNoTracking()
            .Where(item =>
                item.ReferenceType == "RacePrediction" &&
                item.ReferenceId == prediction.PredictionId &&
                (item.TransactionType == PointTransactionTypes.PredictionWinSettlement ||
                 item.TransactionType == PointTransactionTypes.PredictionLossSettlement ||
                 item.TransactionType == PointTransactionTypes.PredictionSettlementReversal ||
                 item.TransactionType == PointTransactionTypes.PredictionPayout ||
                 item.TransactionType == PointTransactionTypes.PredictionPayoutReversal ||
                 item.TransactionType == PointTransactionTypes.ResultCorrectionAdjustment ||
                 (item.TransactionType == PointTransactionTypes.AdminAdjustment &&
                  item.IdempotencyKey.StartsWith("FIXED_PAYOUT_CUTOVER_"))))
            .OrderBy(item => item.PointTransactionId)
            .ToListAsync(cancellationToken);

        if (legacyRows.Count == 0)
            return null;

        var walletIds = legacyRows
            .Select(item => item.SpectatorSeasonWalletId)
            .Distinct()
            .ToList();
        if (walletIds.Count != 1)
            throw new InvalidOperationException(
                $"Prediction #{prediction.PredictionId} has settlement rows in multiple wallets.");

        return new PointTransaction
        {
            SpectatorSeasonWalletId = walletIds[0],
            RequestedAmount = legacyRows.Sum(item =>
                item.RequestedAmount == 0 && item.Amount != 0
                    ? item.Amount
                    : item.RequestedAmount),
            Amount = legacyRows.Sum(item => item.Amount),
            ScoreDelta = legacyRows.Sum(item => item.ScoreDelta),
            RecoveryDebtDelta = legacyRows.Sum(item => item.RecoveryDebtDelta),
            ReferenceType = "RacePrediction",
            ReferenceId = prediction.PredictionId
        };
    }

    private sealed record RaceSettlementContext(
        int RaceId,
        int SeasonId,
        string SeasonStatus,
        int LifecycleVersion);

    private sealed record PredictionCancellationResult(
        int StakePointsRefunded,
        int PayoutPointsReversed);
}

public sealed record PredictionSettlementSummary(
    int PredictionsAffected,
    int StakePointsRefunded,
    int PayoutPointsReversed);
