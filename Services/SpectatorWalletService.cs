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

        if (wallet != null)
        {
            return wallet;
        }

        if (openingPoints < 0)
        {
            throw new InvalidOperationException("Opening points cannot be negative.");
        }

        wallet = new SpectatorSeasonWallet
        {
            SeasonId = seasonId,
            SpectatorId = spectator.UserId,
            OpeningBettingPoints = openingPoints,
            CurrentBettingPoints = openingPoints,
            SeasonScore = 0,
            Status = SeasonWalletStatuses.Active,
            OpenedAt = now
        };

        wallet.PointTransactions.Add(new PointTransaction
        {
            TransactionType = PointTransactionTypes.SeasonOpening,
            Amount = openingPoints,
            ScoreDelta = 0,
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
        if (wallet.Status != SeasonWalletStatuses.Active)
        {
            throw new InvalidOperationException("The season wallet is not active.");
        }

        var alreadyApplied = _context.PointTransactions.Local
            .Any(item => item.IdempotencyKey == idempotencyKey)
            || await _context.PointTransactions
                .AsNoTracking()
                .AnyAsync(item => item.IdempotencyKey == idempotencyKey, cancellationToken);

        if (alreadyApplied)
        {
            return new WalletMutationResult(
                wallet.CurrentBettingPoints,
                wallet.SeasonScore,
                true);
        }

        var balanceBefore = wallet.CurrentBettingPoints;
        var balanceAfter = checked(balanceBefore + amount);

        if (balanceAfter < 0)
        {
            throw new InvalidOperationException("Insufficient betting points.");
        }

        var scoreAfter = checked(wallet.SeasonScore + scoreDelta);
        if (scoreAfter < 0)
        {
            throw new InvalidOperationException("Season score cannot be negative.");
        }

        wallet.CurrentBettingPoints = balanceAfter;
        wallet.SeasonScore = scoreAfter;

        spectator.BettingPoints = balanceAfter;
        spectator.UpdatedAt = now;

        _context.PointTransactions.Add(new PointTransaction
        {
            SpectatorSeasonWallet = wallet,
            TransactionType = transactionType,
            Amount = amount,
            ScoreDelta = scoreDelta,
            BalanceBefore = balanceBefore,
            BalanceAfter = balanceAfter,
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            IdempotencyKey = idempotencyKey,
            Description = description,
            CreatedAt = now
        });

        return new WalletMutationResult(balanceAfter, scoreAfter, false);
    }
}

public sealed record WalletMutationResult(
    int BettingPoints,
    int SeasonScore,
    bool AlreadyApplied);
