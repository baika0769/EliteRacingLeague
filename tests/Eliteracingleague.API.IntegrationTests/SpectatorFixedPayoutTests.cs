using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.Models;
using Eliteracingleague.API.Services;
using Eliteracingleague.API.Services.SystemTime;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Eliteracingleague.API.IntegrationTests;

public sealed class SpectatorFixedPayoutTests
{
    [Fact]
    public async Task Stake100_Win_EndsAtWallet1200AndScore200()
    {
        await using var context = CreateContext();
        var (wallet, spectator) = SeedWallet(context);
        var service = new SpectatorWalletService(context);
        var now = DateTime.UtcNow;

        await service.ApplyAsync(wallet, spectator, PointTransactionTypes.PredictionStake,
            -100, 0, "TEST_WIN_STAKE", "RacePrediction", 1, null, now);
        await service.ApplyAsync(wallet, spectator, PointTransactionTypes.PredictionWinSettlement,
            SpectatorBettingRules.CalculateWinGrossPayout(100),
            SpectatorBettingRules.CalculateWinScoreDelta(100),
            "PREDICTION_WIN_1_V1", "RacePrediction", 1, null, now);
        await context.SaveChangesAsync();

        Assert.Equal(1200, wallet.CurrentBettingPoints);
        Assert.Equal(200, wallet.SeasonScore);
        Assert.Equal(300, SpectatorBettingRules.CalculateWinGrossPayout(100));
    }

    [Fact]
    public async Task Stake100_Loss_EndsAtWallet900AndNegativeScore100()
    {
        await using var context = CreateContext();
        var (wallet, spectator) = SeedWallet(context);
        var service = new SpectatorWalletService(context);
        var now = DateTime.UtcNow;

        await service.ApplyAsync(wallet, spectator, PointTransactionTypes.PredictionStake,
            -100, 0, "TEST_LOSS_STAKE", "RacePrediction", 2, null, now);
        await service.ApplyAsync(wallet, spectator, PointTransactionTypes.PredictionLossSettlement,
            0, SpectatorBettingRules.CalculateLossScoreDelta(100),
            "PREDICTION_LOSS_2_V1", "RacePrediction", 2, null, now);
        await context.SaveChangesAsync();

        Assert.Equal(900, wallet.CurrentBettingPoints);
        Assert.Equal(-100, wallet.SeasonScore);
    }

    [Fact]
    public async Task CancelEvaluatedWin_ReversalAndRefundAreNeutral()
    {
        await using var context = CreateContext();
        var (wallet, spectator) = SeedWallet(context);
        var service = new SpectatorWalletService(context);
        var now = DateTime.UtcNow;

        await service.ApplyAsync(wallet, spectator, PointTransactionTypes.PredictionStake,
            -100, 0, "TEST_CANCEL_STAKE", "RacePrediction", 3, null, now);
        await service.ApplyAsync(wallet, spectator, PointTransactionTypes.PredictionWinSettlement,
            300, 200, "PREDICTION_WIN_3_V1", "RacePrediction", 3, null, now);
        await context.SaveChangesAsync();

        var settlement = await context.PointTransactions.SingleAsync(item =>
            item.IdempotencyKey == "PREDICTION_WIN_3_V1");
        await service.ReverseTransactionAsync(wallet, spectator, settlement,
            "PREDICTION_REVERSAL_3_V1", null, now);
        await service.ApplyAsync(wallet, spectator, PointTransactionTypes.PredictionRefund,
            100, 0, "PREDICTION_REFUND_3", "RacePrediction", 3, null, now);
        await context.SaveChangesAsync();

        Assert.Equal(1000, wallet.CurrentBettingPoints);
        Assert.Equal(0, wallet.SeasonScore);
        Assert.Equal(0, wallet.PendingRecoveryPoints);
    }

    [Fact]
    public async Task RepeatingSameSettlementVersion_IsIdempotent()
    {
        await using var context = CreateContext();
        var (wallet, spectator) = SeedWallet(context);
        var service = new SpectatorWalletService(context);
        var now = DateTime.UtcNow;

        await service.ApplyAsync(wallet, spectator, PointTransactionTypes.PredictionStake,
            -100, 0, "TEST_RETRY_STAKE", "RacePrediction", 4, null, now);
        await service.ApplyAsync(wallet, spectator, PointTransactionTypes.PredictionWinSettlement,
            300, 200, "PREDICTION_WIN_4_V7", "RacePrediction", 4, null, now);
        var retry = await service.ApplyAsync(wallet, spectator, PointTransactionTypes.PredictionWinSettlement,
            300, 200, "PREDICTION_WIN_4_V7", "RacePrediction", 4, null, now);

        Assert.True(retry.AlreadyApplied);
        Assert.Equal(1200, wallet.CurrentBettingPoints);
        Assert.Equal(200, wallet.SeasonScore);
    }

    [Fact]
    public async Task CancelPendingOrLockedPrediction_RefundIsNeutralAndIdempotent()
    {
        await using var context = CreateContext();
        var (wallet, spectator) = SeedWallet(context);
        var service = new SpectatorWalletService(context);
        var now = DateTime.UtcNow;

        await service.ApplyAsync(wallet, spectator, PointTransactionTypes.PredictionStake,
            -100, 0, "PENDING_CANCEL_STAKE", "RacePrediction", 41, null, now);
        var firstRefund = await service.ApplyAsync(wallet, spectator, PointTransactionTypes.PredictionRefund,
            100, 0, "PREDICTION_REFUND_41", "RacePrediction", 41, null, now);
        var retryRefund = await service.ApplyAsync(wallet, spectator, PointTransactionTypes.PredictionRefund,
            100, 0, "PREDICTION_REFUND_41", "RacePrediction", 41, null, now);

        Assert.False(firstRefund.AlreadyApplied);
        Assert.True(retryRefund.AlreadyApplied);
        Assert.Equal(1000, wallet.CurrentBettingPoints);
        Assert.Equal(0, wallet.SeasonScore);
    }

    [Fact]
    public async Task WinToWinCorrection_NewVersionSettlesOnceAndReversalRetryIsIdempotent()
    {
        await using var context = CreateContext();
        var (wallet, spectator) = SeedWallet(context);
        var service = new SpectatorWalletService(context);
        var now = DateTime.UtcNow;

        await service.ApplyAsync(wallet, spectator, PointTransactionTypes.PredictionStake,
            -100, 0, "WIN_WIN_STAKE", "RacePrediction", 42, null, now);
        await service.ApplyAsync(wallet, spectator, PointTransactionTypes.PredictionWinSettlement,
            300, 200, "PREDICTION_WIN_42_V1", "RacePrediction", 42, null, now);
        await context.SaveChangesAsync();

        var firstVersion = await context.PointTransactions.SingleAsync(item =>
            item.IdempotencyKey == "PREDICTION_WIN_42_V1");
        await service.ReverseTransactionAsync(wallet, spectator, firstVersion,
            "PREDICTION_REVERSAL_42_V1", null, now);
        var reversalRetry = await service.ReverseTransactionAsync(wallet, spectator, firstVersion,
            "PREDICTION_REVERSAL_42_V1", null, now);
        await service.ApplyAsync(wallet, spectator, PointTransactionTypes.PredictionWinSettlement,
            300, 200, "PREDICTION_WIN_42_V3", "RacePrediction", 42, null, now);
        var settlementRetry = await service.ApplyAsync(wallet, spectator,
            PointTransactionTypes.PredictionWinSettlement,
            300, 200, "PREDICTION_WIN_42_V3", "RacePrediction", 42, null, now);

        Assert.True(reversalRetry.AlreadyApplied);
        Assert.True(settlementRetry.AlreadyApplied);
        Assert.Equal(1200, wallet.CurrentBettingPoints);
        Assert.Equal(200, wallet.SeasonScore);
    }

    [Fact]
    public async Task LossToLossCorrection_NewVersionKeepsSingleLossImpact()
    {
        await using var context = CreateContext();
        var (wallet, spectator) = SeedWallet(context);
        var service = new SpectatorWalletService(context);
        var now = DateTime.UtcNow;

        await service.ApplyAsync(wallet, spectator, PointTransactionTypes.PredictionStake,
            -100, 0, "LOSS_LOSS_STAKE", "RacePrediction", 43, null, now);
        await service.ApplyAsync(wallet, spectator, PointTransactionTypes.PredictionLossSettlement,
            0, -100, "PREDICTION_LOSS_43_V1", "RacePrediction", 43, null, now);
        await context.SaveChangesAsync();

        var firstVersion = await context.PointTransactions.SingleAsync(item =>
            item.IdempotencyKey == "PREDICTION_LOSS_43_V1");
        await service.ReverseTransactionAsync(wallet, spectator, firstVersion,
            "PREDICTION_REVERSAL_43_V1", null, now);
        await service.ApplyAsync(wallet, spectator, PointTransactionTypes.PredictionLossSettlement,
            0, -100, "PREDICTION_LOSS_43_V3", "RacePrediction", 43, null, now);

        Assert.Equal(900, wallet.CurrentBettingPoints);
        Assert.Equal(-100, wallet.SeasonScore);
    }

    [Fact]
    public async Task StakeAboveWalletBalance_IsRejectedWithoutMutation()
    {
        await using var context = CreateContext();
        var (wallet, spectator) = SeedWallet(context);
        var service = new SpectatorWalletService(context);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApplyAsync(
            wallet,
            spectator,
            PointTransactionTypes.PredictionStake,
            -1001,
            0,
            "INSUFFICIENT_STAKE",
            "RacePrediction",
            44,
            null,
            DateTime.UtcNow));

        Assert.Equal(1000, wallet.CurrentBettingPoints);
        Assert.Equal(0, wallet.SeasonScore);
    }

    [Fact]
    public async Task NewSeasonOpening_IncludesCarryBonusButResetsScoreToZero()
    {
        await using var context = CreateContext();
        var (wallet, spectator) = SeedWallet(context);
        var service = new SpectatorWalletService(context);
        wallet.SeasonScore = 500;

        await service.OpenWalletForSeasonAsync(
            wallet.SeasonId,
            spectator,
            openingPoints: 1200,
            now: DateTime.UtcNow);

        Assert.Equal(1200, wallet.OpeningBettingPoints);
        Assert.Equal(1200, wallet.CurrentBettingPoints);
        Assert.Equal(0, wallet.SeasonScore);
    }

    [Fact]
    public async Task CancelEvaluatedLoss_ReversesNegativeScoreAndRefundsStake()
    {
        await using var context = CreateContext();
        var (wallet, spectator) = SeedWallet(context);
        var service = new SpectatorWalletService(context);
        var now = DateTime.UtcNow;

        await service.ApplyAsync(wallet, spectator, PointTransactionTypes.PredictionStake,
            -100, 0, "TEST_CANCEL_LOSS_STAKE", "RacePrediction", 5, null, now);
        await service.ApplyAsync(wallet, spectator, PointTransactionTypes.PredictionLossSettlement,
            0, -100, "PREDICTION_LOSS_5_V1", "RacePrediction", 5, null, now);
        await context.SaveChangesAsync();

        var settlement = await context.PointTransactions.SingleAsync(item =>
            item.IdempotencyKey == "PREDICTION_LOSS_5_V1");
        await service.ReverseTransactionAsync(wallet, spectator, settlement,
            "PREDICTION_REVERSAL_5_V1", null, now);
        await service.ApplyAsync(wallet, spectator, PointTransactionTypes.PredictionRefund,
            100, 0, "PREDICTION_REFUND_5", "RacePrediction", 5, null, now);

        Assert.Equal(1000, wallet.CurrentBettingPoints);
        Assert.Equal(0, wallet.SeasonScore);
    }

    [Fact]
    public async Task WinLossWinSequence_UsesFixedWalletAndScoreFormula()
    {
        await using var context = CreateContext();
        var (wallet, spectator) = SeedWallet(context);
        var service = new SpectatorWalletService(context);
        var now = DateTime.UtcNow;

        await service.ApplyAsync(wallet, spectator, PointTransactionTypes.PredictionStake,
            -100, 0, "SEQ_STAKE_1", "RacePrediction", 11, null, now);
        await service.ApplyAsync(wallet, spectator, PointTransactionTypes.PredictionWinSettlement,
            300, 200, "PREDICTION_WIN_11_V1", "RacePrediction", 11, null, now);
        await service.ApplyAsync(wallet, spectator, PointTransactionTypes.PredictionStake,
            -300, 0, "SEQ_STAKE_2", "RacePrediction", 12, null, now);
        await service.ApplyAsync(wallet, spectator, PointTransactionTypes.PredictionLossSettlement,
            0, -300, "PREDICTION_LOSS_12_V1", "RacePrediction", 12, null, now);
        await service.ApplyAsync(wallet, spectator, PointTransactionTypes.PredictionStake,
            -200, 0, "SEQ_STAKE_3", "RacePrediction", 13, null, now);
        await service.ApplyAsync(wallet, spectator, PointTransactionTypes.PredictionWinSettlement,
            600, 400, "PREDICTION_WIN_13_V1", "RacePrediction", 13, null, now);

        Assert.Equal(1300, wallet.CurrentBettingPoints);
        Assert.Equal(300, wallet.SeasonScore);
    }

    [Fact]
    public async Task ReversalOfSpentWin_CreatesAuditedRecoveryDebt()
    {
        await using var context = CreateContext();
        var (wallet, spectator) = SeedWallet(context);
        var service = new SpectatorWalletService(context);
        var now = DateTime.UtcNow;

        await service.ApplyAsync(wallet, spectator, PointTransactionTypes.PredictionStake,
            -100, 0, "TEST_DEBT_STAKE", "RacePrediction", 6, null, now);
        await service.ApplyAsync(wallet, spectator, PointTransactionTypes.PredictionWinSettlement,
            300, 200, "PREDICTION_WIN_6_V1", "RacePrediction", 6, null, now);
        await context.SaveChangesAsync();

        wallet.CurrentBettingPoints = 0;
        spectator.BettingPoints = 0;
        var settlement = await context.PointTransactions.SingleAsync(item =>
            item.IdempotencyKey == "PREDICTION_WIN_6_V1");
        await service.ReverseTransactionAsync(wallet, spectator, settlement,
            "PREDICTION_REVERSAL_6_V1", null, now);
        await service.ApplyAsync(wallet, spectator, PointTransactionTypes.PredictionRefund,
            100, 0, "PREDICTION_REFUND_6", "RacePrediction", 6, null, now);
        await context.SaveChangesAsync();

        Assert.Equal(0, wallet.CurrentBettingPoints);
        Assert.Equal(0, wallet.SeasonScore);
        Assert.Equal(200, wallet.PendingRecoveryPoints);

        var refund = await context.PointTransactions.SingleAsync(item =>
            item.IdempotencyKey == "PREDICTION_REFUND_6");
        Assert.Equal(100, refund.RequestedAmount);
        Assert.Equal(0, refund.Amount);
        Assert.Equal(-100, refund.RecoveryDebtDelta);
    }

    [Fact]
    public async Task ResultCorrection_WinToLoss_ReversesOldVersionWithoutRefundingStake()
    {
        await using var context = CreateContext();
        var (wallet, spectator) = SeedWallet(context);
        var service = new SpectatorWalletService(context);
        var now = DateTime.UtcNow;

        await service.ApplyAsync(wallet, spectator, PointTransactionTypes.PredictionStake,
            -100, 0, "CORRECTION_STAKE_1", "RacePrediction", 21, null, now);
        await service.ApplyAsync(wallet, spectator, PointTransactionTypes.PredictionWinSettlement,
            300, 200, "PREDICTION_WIN_21_V1", "RacePrediction", 21, null, now);
        await context.SaveChangesAsync();

        var oldSettlement = await context.PointTransactions.SingleAsync(item =>
            item.IdempotencyKey == "PREDICTION_WIN_21_V1");
        await service.ReverseTransactionAsync(wallet, spectator, oldSettlement,
            "PREDICTION_REVERSAL_21_V1", null, now);
        await service.ApplyAsync(wallet, spectator, PointTransactionTypes.PredictionLossSettlement,
            0, -100, "PREDICTION_LOSS_21_V3", "RacePrediction", 21, null, now);

        Assert.Equal(900, wallet.CurrentBettingPoints);
        Assert.Equal(-100, wallet.SeasonScore);
    }

    [Fact]
    public async Task Reversal_RestoresDebtPaidByOriginalWinCredit()
    {
        await using var context = CreateContext();
        var (wallet, spectator) = SeedWallet(context);
        var service = new SpectatorWalletService(context);
        var now = DateTime.UtcNow;
        wallet.PendingRecoveryPoints = 100;

        await service.ApplyAsync(wallet, spectator, PointTransactionTypes.PredictionWinSettlement,
            300, 200, "PREDICTION_WIN_23_V1", "RacePrediction", 23, null, now);
        await context.SaveChangesAsync();
        var settlement = await context.PointTransactions.SingleAsync(item =>
            item.IdempotencyKey == "PREDICTION_WIN_23_V1");

        await service.ReverseTransactionAsync(wallet, spectator, settlement,
            "PREDICTION_REVERSAL_23_V1", null, now);

        Assert.Equal(1000, wallet.CurrentBettingPoints);
        Assert.Equal(0, wallet.SeasonScore);
        Assert.Equal(100, wallet.PendingRecoveryPoints);
    }

    [Fact]
    public async Task Reversal_OfLegacyCutoverAggregate_UsesNetFixedPayout()
    {
        await using var context = CreateContext();
        var (wallet, spectator) = SeedWallet(context);
        var service = new SpectatorWalletService(context);
        var now = DateTime.UtcNow;
        wallet.CurrentBettingPoints = 0;
        wallet.SeasonScore = 100;
        wallet.PendingRecoveryPoints = 200;
        spectator.BettingPoints = 0;
        var reconciledSettlement = new PointTransaction
        {
            SpectatorSeasonWalletId = wallet.SpectatorSeasonWalletId,
            RequestedAmount = 150,
            Amount = 350,
            ScoreDelta = 100,
            RecoveryDebtDelta = 200,
            ReferenceType = "RacePrediction",
            ReferenceId = 24
        };

        await service.ReverseTransactionAsync(wallet, spectator, reconciledSettlement,
            "PREDICTION_REVERSAL_24_V1", null, now);
        await service.ApplyAsync(wallet, spectator, PointTransactionTypes.PredictionRefund,
            50, 0, "PREDICTION_REFUND_24", "RacePrediction", 24, null, now);

        Assert.Equal(0, wallet.CurrentBettingPoints);
        Assert.Equal(0, wallet.SeasonScore);
        Assert.Equal(300, wallet.PendingRecoveryPoints);
    }

    [Fact]
    public async Task ResultCorrection_LossToWin_ReversesOldVersionWithoutRefundingStake()
    {
        await using var context = CreateContext();
        var (wallet, spectator) = SeedWallet(context);
        var service = new SpectatorWalletService(context);
        var now = DateTime.UtcNow;

        await service.ApplyAsync(wallet, spectator, PointTransactionTypes.PredictionStake,
            -100, 0, "CORRECTION_STAKE_2", "RacePrediction", 22, null, now);
        await service.ApplyAsync(wallet, spectator, PointTransactionTypes.PredictionLossSettlement,
            0, -100, "PREDICTION_LOSS_22_V1", "RacePrediction", 22, null, now);
        await context.SaveChangesAsync();

        var oldSettlement = await context.PointTransactions.SingleAsync(item =>
            item.IdempotencyKey == "PREDICTION_LOSS_22_V1");
        await service.ReverseTransactionAsync(wallet, spectator, oldSettlement,
            "PREDICTION_REVERSAL_22_V1", null, now);
        await service.ApplyAsync(wallet, spectator, PointTransactionTypes.PredictionWinSettlement,
            300, 200, "PREDICTION_WIN_22_V3", "RacePrediction", 22, null, now);

        Assert.Equal(1200, wallet.CurrentBettingPoints);
        Assert.Equal(200, wallet.SeasonScore);
    }

    [Fact]
    public async Task Leaderboard_UsesStoredSeasonScoreAndSupportsNegativeValues()
    {
        await using var context = CreateContext();
        var now = DateTime.UtcNow;
        var admin = TestUser(100, "Admin", UserRoles.Admin);
        var first = TestUser(101, "First", UserRoles.Spectator);
        var second = TestUser(102, "Second", UserRoles.Spectator);
        var season = new Season
        {
            SeasonId = 7,
            SeasonName = "Fixed payout season",
            StartDate = now.AddDays(-1),
            EndDate = now.AddDays(30),
            Status = SeasonStatuses.Active,
            PointsPerCorrectPrediction = 999,
            CreatedAt = now
        };
        var tournament = new Tournament
        {
            TournamentId = 8,
            TournamentName = "Completed tournament",
            StartDate = DateOnly.FromDateTime(now.AddDays(-1)),
            EndDate = DateOnly.FromDateTime(now),
            Location = "Test",
            MaxHorses = 2,
            Status = TournamentStatuses.Completed,
            SeasonId = season.SeasonId,
            CreatedBy = admin.UserId,
            CreatedAt = now
        };
        var race = new Race
        {
            RaceId = 9,
            TournamentId = tournament.TournamentId,
            RaceName = "Published race",
            RaceDate = now,
            DistanceMeters = 1000,
            MaxHorses = 2,
            Status = RaceStatuses.Published,
            CreatedAt = now
        };

        context.AddRange(admin, first, second, season, tournament, race);
        context.SpectatorSeasonWallets.AddRange(
            TestWallet(71, season.SeasonId, first, -100),
            TestWallet(72, season.SeasonId, second, -50));
        context.RacePredictions.AddRange(
            new RacePrediction
            {
                PredictionId = 81,
                RaceId = race.RaceId,
                SpectatorId = first.UserId,
                PredictedRegistrationId = 1,
                Status = RacePredictionStatuses.Evaluated,
                IsCorrect = true,
                StakePoints = 100,
                PointsAwarded = 300,
                PredictedAt = now,
                CreatedAt = now
            },
            new RacePrediction
            {
                PredictionId = 82,
                RaceId = race.RaceId,
                SpectatorId = second.UserId,
                PredictedRegistrationId = 2,
                Status = RacePredictionStatuses.Evaluated,
                IsCorrect = false,
                StakePoints = 50,
                PointsAwarded = 0,
                PredictedAt = now,
                CreatedAt = now
            });
        await context.SaveChangesAsync();

        var service = new SpectatorLeaderboardService(context, new SystemDateTimeProvider());
        var leaderboard = await service.GetPredictorLeaderboardAsync(50, season.SeasonId);

        Assert.Equal(second.UserId, leaderboard[0].SpectatorId);
        Assert.Equal(-50, leaderboard[0].Points);
        Assert.Equal(first.UserId, leaderboard[1].SpectatorId);
        Assert.Equal(-100, leaderboard[1].Points);
    }

    private static EliteRacingLeagueContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<EliteRacingLeagueContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new EliteRacingLeagueContext(options);
    }

    private static (SpectatorSeasonWallet Wallet, User Spectator) SeedWallet(
        EliteRacingLeagueContext context)
    {
        var spectator = new User
        {
            UserId = 1,
            FullName = "Test Spectator",
            Email = "spectator@example.test",
            PasswordHash = "test",
            Role = UserRoles.Spectator,
            Status = UserStatuses.Active,
            BettingPoints = 1000,
            CreatedAt = DateTime.UtcNow
        };
        var wallet = new SpectatorSeasonWallet
        {
            SpectatorSeasonWalletId = 1,
            SeasonId = 1,
            SpectatorId = spectator.UserId,
            Spectator = spectator,
            OpeningBettingPoints = 1000,
            CurrentBettingPoints = 1000,
            SeasonScore = 0,
            PendingRecoveryPoints = 0,
            Status = SeasonWalletStatuses.Active,
            OpenedAt = DateTime.UtcNow
        };

        context.Users.Add(spectator);
        context.SpectatorSeasonWallets.Add(wallet);
        context.SaveChanges();
        return (wallet, spectator);
    }

    private static User TestUser(int id, string name, string role) => new()
    {
        UserId = id,
        FullName = name,
        Email = $"{id}@example.test",
        PasswordHash = "test",
        Role = role,
        Status = UserStatuses.Active,
        CreatedAt = DateTime.UtcNow
    };

    private static SpectatorSeasonWallet TestWallet(
        int id,
        int seasonId,
        User spectator,
        int score) => new()
    {
        SpectatorSeasonWalletId = id,
        SeasonId = seasonId,
        SpectatorId = spectator.UserId,
        Spectator = spectator,
        OpeningBettingPoints = 1000,
        CurrentBettingPoints = 1000,
        SeasonScore = score,
        PendingRecoveryPoints = 0,
        Status = SeasonWalletStatuses.Active,
        OpenedAt = DateTime.UtcNow
    };
}
