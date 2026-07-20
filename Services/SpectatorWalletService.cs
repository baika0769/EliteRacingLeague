using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Services;

public class SpectatorWalletService
{
    private readonly EliteRacingLeagueContext _context;

    public SpectatorWalletService(EliteRacingLeagueContext context)
    {
        _context = context;
    }



    public async Task<SpectatorSeasonWallet> OpenWalletForSeasonAsync(
        int seasonId,
        User spectator,
        int openingPoints,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        if (openingPoints < 0)
            throw new InvalidOperationException("Opening points cannot be negative.");

        var wallet = await _context.SpectatorSeasonWallets
            .Include(item => item.PointTransactions)
            .FirstOrDefaultAsync(
                item => item.SeasonId == seasonId && item.SpectatorId == spectator.UserId,
                cancellationToken);

        if (wallet == null)
        {
            return await GetOrCreateWalletAsync(
                seasonId,
                spectator,
                openingPoints,
                now,
                cancellationToken);
        }

        var hasPredictions = await _context.RacePredictions
            .AsNoTracking()
            .AnyAsync(item =>
                item.SpectatorId == spectator.UserId &&
                item.Race.Tournament.SeasonId == seasonId,
                cancellationToken);

        if (hasPredictions)
        {
            throw new InvalidOperationException(
                "The season wallet cannot be reset because prediction activity already exists.");
        }

        var openingKey = $"SEASON_OPENING_{seasonId}_{spectator.UserId}";
        var openingTransaction = wallet.PointTransactions
            .FirstOrDefault(item =>
                item.TransactionType == PointTransactionTypes.SeasonOpening &&
                item.IdempotencyKey == openingKey);

        var unexpectedTransactions = wallet.PointTransactions
            .Where(item => item != openingTransaction)
            .ToList();

        if (unexpectedTransactions.Count > 0)
        {
            throw new InvalidOperationException(
                "The draft season wallet already contains point transactions and cannot be safely reset.");
        }

        if (openingTransaction == null)
        {
            openingTransaction = new PointTransaction
            {
                SpectatorSeasonWallet = wallet,
                TransactionType = PointTransactionTypes.SeasonOpening,
                IdempotencyKey = openingKey
            };
            _context.PointTransactions.Add(openingTransaction);
        }

        openingTransaction.RequestedAmount = openingPoints;
        openingTransaction.Amount = openingPoints;
        openingTransaction.ScoreDelta = 0;
        openingTransaction.RecoveryDebtDelta = 0;
        openingTransaction.BalanceBefore = 0;
        openingTransaction.BalanceAfter = openingPoints;
        openingTransaction.ReferenceType = "Season";
        openingTransaction.ReferenceId = seasonId;
        openingTransaction.Description = "Opening betting balance for the season.";
        openingTransaction.CreatedAt = now;

        wallet.OpeningBettingPoints = openingPoints;
        wallet.CurrentBettingPoints = openingPoints;
        wallet.SeasonScore = 0;
        wallet.PendingRecoveryPoints = 0;
        wallet.FinalBettingPoints = null;
        wallet.FinalSeasonScore = null;
        wallet.FinalRank = null;
        wallet.Status = SeasonWalletStatuses.Active;
        wallet.OpenedAt = now;
        wallet.FrozenAt = null;
        wallet.SettledAt = null;

        spectator.BettingPoints = openingPoints;
        spectator.UpdatedAt = now;

        return wallet;
    }

    public async Task<SpectatorSeasonWallet> GetOrCreateWalletAsync(
        int seasonId,
        User spectator,
        int openingPoints,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        var wallet = await _context.SpectatorSeasonWallets
            .FirstOrDefaultAsync(
                item => item.SeasonId == seasonId && item.SpectatorId == spectator.UserId,
                cancellationToken);

        if (wallet != null) return wallet;
        if (openingPoints < 0) throw new InvalidOperationException("Opening points cannot be negative.");

        wallet = new SpectatorSeasonWallet
        {
            SeasonId = seasonId,
            SpectatorId = spectator.UserId,
            OpeningBettingPoints = openingPoints,
            CurrentBettingPoints = openingPoints,
            SeasonScore = 0,
            PendingRecoveryPoints = 0,
            Status = SeasonWalletStatuses.Active,
            OpenedAt = now
        };

        wallet.PointTransactions.Add(new PointTransaction
        {
            TransactionType = PointTransactionTypes.SeasonOpening,
            RequestedAmount = openingPoints,
            Amount = openingPoints,
            ScoreDelta = 0,
            RecoveryDebtDelta = 0,
            BalanceBefore = 0,
            BalanceAfter = openingPoints,
            ReferenceType = "Season",
            ReferenceId = seasonId,
            IdempotencyKey = $"SEASON_OPENING_{seasonId}_{spectator.UserId}",
            Description = "Opening betting balance for the season.",
            CreatedAt = now
        });

        _context.SpectatorSeasonWallets.Add(wallet);
        spectator.BettingPoints = openingPoints;
        spectator.UpdatedAt = now;
        return wallet;
    }

    public async Task<WalletMutationResult> ApplyAsync(
        SpectatorSeasonWallet wallet,
        User spectator,
        string transactionType,
        int amount,
        int scoreDelta,
        string idempotencyKey,
        string? referenceType,
        int? referenceId,
        string? description,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        EnsureMutable(wallet);
        if (await ExistsAsync(idempotencyKey, cancellationToken))
            return Existing(wallet);

        var requestedAmount = amount;
        var recoveryDebtDelta = 0;

        // Every positive credit first pays any debt caused by a prior result correction.
        if (amount > 0 && wallet.PendingRecoveryPoints > 0)
        {
            var recovered = Math.Min(amount, wallet.PendingRecoveryPoints);
            wallet.PendingRecoveryPoints -= recovered;
            recoveryDebtDelta = -recovered;
            amount -= recovered;
        }

        var before = wallet.CurrentBettingPoints;
        var after = checked(before + amount);
        if (after < 0) throw new InvalidOperationException("Insufficient betting points.");

        var scoreAfter = checked(wallet.SeasonScore + scoreDelta);
        if (scoreAfter < 0) throw new InvalidOperationException("Season score cannot be negative.");

        wallet.CurrentBettingPoints = after;
        wallet.SeasonScore = scoreAfter;
        spectator.BettingPoints = after;
        spectator.UpdatedAt = now;

        AddTransaction(wallet, transactionType, requestedAmount, amount, scoreDelta,
            recoveryDebtDelta, before, after, idempotencyKey, referenceType,
            referenceId, description, now);

        return new WalletMutationResult(after, scoreAfter, wallet.PendingRecoveryPoints, false);
    }

    public async Task<WalletMutationResult> ApplyReversalAsync(
        SpectatorSeasonWallet wallet,
        User spectator,
        string transactionType,
        int pointsToRecover,
        int scoreToReverse,
        string idempotencyKey,
        string? referenceType,
        int? referenceId,
        string? description,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        EnsureMutable(wallet);
        if (pointsToRecover < 0 || scoreToReverse < 0)
            throw new ArgumentOutOfRangeException(nameof(pointsToRecover));

        if (await ExistsAsync(idempotencyKey, cancellationToken))
            return Existing(wallet);

        var before = wallet.CurrentBettingPoints;
        var recoveredNow = Math.Min(before, pointsToRecover);
        var debt = pointsToRecover - recoveredNow;
        var after = before - recoveredNow;
        var scoreAfter = wallet.SeasonScore - scoreToReverse;

        if (scoreAfter < 0)
            throw new InvalidOperationException("Cannot reverse more season score than the wallet contains.");

        wallet.CurrentBettingPoints = after;
        wallet.SeasonScore = scoreAfter;
        wallet.PendingRecoveryPoints = checked(wallet.PendingRecoveryPoints + debt);
        spectator.BettingPoints = after;
        spectator.UpdatedAt = now;

        AddTransaction(wallet, transactionType, -pointsToRecover, -recoveredNow,
            -scoreToReverse, debt, before, after, idempotencyKey, referenceType,
            referenceId, description, now);

        return new WalletMutationResult(after, scoreAfter, wallet.PendingRecoveryPoints, false);
    }

    private void EnsureMutable(SpectatorSeasonWallet wallet)
    {
        if (wallet.Status != SeasonWalletStatuses.Active)
            throw new InvalidOperationException("The season wallet is not active.");
    }

    private async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken)
    {
        var localTransaction = _context.PointTransactions.Local
            .FirstOrDefault(item => item.IdempotencyKey == key);

        if (localTransaction != null)
        {
            return _context.Entry(localTransaction).State != EntityState.Deleted;
        }

        return await _context.PointTransactions
            .AsNoTracking()
            .AnyAsync(item => item.IdempotencyKey == key, cancellationToken);
    }

    private static WalletMutationResult Existing(SpectatorSeasonWallet wallet) =>
        new(wallet.CurrentBettingPoints, wallet.SeasonScore, wallet.PendingRecoveryPoints, true);

    private void AddTransaction(
        SpectatorSeasonWallet wallet,
        string transactionType,
        int requestedAmount,
        int amount,
        int scoreDelta,
        int recoveryDebtDelta,
        int before,
        int after,
        string key,
        string? referenceType,
        int? referenceId,
        string? description,
        DateTime now)
    {
        _context.PointTransactions.Add(new PointTransaction
        {
            SpectatorSeasonWallet = wallet,
            TransactionType = transactionType,
            RequestedAmount = requestedAmount,
            Amount = amount,
            ScoreDelta = scoreDelta,
            RecoveryDebtDelta = recoveryDebtDelta,
            BalanceBefore = before,
            BalanceAfter = after,
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            IdempotencyKey = key,
            Description = description,
            CreatedAt = now
        });
    }
}

public sealed record WalletMutationResult(
    int BettingPoints,
    int SeasonScore,
    int PendingRecoveryPoints,
    bool AlreadyApplied);
