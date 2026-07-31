using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.Models;
using Eliteracingleague.API.Services;
using Eliteracingleague.API.Services.Racing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace Eliteracingleague.API.IntegrationTests;

public sealed class PreRacePredictionCancellationTests
{
    [Fact]
    public async Task FailedPreRace_CancelsAllMatchingPredictionsAndRefundsFullStakeOnce()
    {
        await using var context = CreateContext();
        var now = DateTime.UtcNow;
        var admin = User(1, "Admin", UserRoles.Admin, 0);
        var firstSpectator = User(2, "First Spectator", UserRoles.Spectator, 900);
        var secondSpectator = User(3, "Second Spectator", UserRoles.Spectator, 850);
        var season = new Season
        {
            SeasonId = 1,
            SeasonName = "Active season",
            StartDate = now.AddDays(-1),
            EndDate = now.AddDays(30),
            Status = SeasonStatuses.Active,
            CreatedAt = now
        };
        var tournament = new Tournament
        {
            TournamentId = 1,
            TournamentName = "Pre-race tournament",
            StartDate = DateOnly.FromDateTime(now),
            EndDate = DateOnly.FromDateTime(now.AddDays(1)),
            Location = "Test",
            MaxHorses = 4,
            Status = TournamentStatuses.ClosedRegistration,
            SeasonId = season.SeasonId,
            CreatedBy = admin.UserId,
            CreatedAt = now
        };
        var race = new Race
        {
            RaceId = 1,
            TournamentId = tournament.TournamentId,
            RaceName = "Pre-race",
            RaceDate = now.AddHours(2),
            PredictionDeadline = now.AddHours(1),
            DistanceMeters = 1000,
            MaxHorses = 4,
            Status = RaceStatuses.AssignedReferee,
            LifecycleVersion = 1,
            CreatedAt = now
        };
        var firstWallet = Wallet(1, season.SeasonId, firstSpectator, 900, 37, 50);
        var secondWallet = Wallet(2, season.SeasonId, secondSpectator, 850, -20, 0);

        context.AddRange(admin, firstSpectator, secondSpectator, season, tournament, race);
        context.SpectatorSeasonWallets.AddRange(firstWallet, secondWallet);
        context.RacePredictions.AddRange(
            Prediction(1, race.RaceId, firstSpectator.UserId, 10, 100, RacePredictionStatuses.Pending, now),
            Prediction(2, race.RaceId, secondSpectator.UserId, 10, 150, RacePredictionStatuses.Locked, now),
            Prediction(3, race.RaceId, firstSpectator.UserId, 11, 25, RacePredictionStatuses.Cancelled, now));
        await context.SaveChangesAsync();

        var service = new RacePredictionSettlementService(
            context,
            new SpectatorWalletService(context));

        var result = await service.CancelForFailedPreRaceInspectionAsync(
            race.RaceId,
            registrationId: 10,
            horseName: "Storm");

        Assert.Equal(2, result.PredictionsAffected);
        Assert.Equal(250, result.StakePointsRefunded);
        Assert.Equal(1000, firstWallet.CurrentBettingPoints);
        Assert.Equal(1000, secondWallet.CurrentBettingPoints);
        Assert.Equal(37, firstWallet.SeasonScore);
        Assert.Equal(-20, secondWallet.SeasonScore);
        Assert.Equal(50, firstWallet.PendingRecoveryPoints);

        var refunds = await context.PointTransactions
            .Where(item => item.TransactionType == PointTransactionTypes.PredictionRefund)
            .OrderBy(item => item.ReferenceId)
            .ToListAsync();
        Assert.Equal(new[] { 100, 150 }, refunds.Select(item => item.Amount));
        Assert.All(refunds, refund =>
        {
            Assert.Equal(0, refund.ScoreDelta);
            Assert.Equal(0, refund.RecoveryDebtDelta);
        });

        Assert.All(
            await context.RacePredictions.Where(item => item.PredictedRegistrationId == 10).ToListAsync(),
            prediction => Assert.Equal(RacePredictionStatuses.Cancelled, prediction.Status));

        var notifications = await context.Notifications
            .OrderBy(item => item.UserId)
            .ToListAsync();
        Assert.Equal(2, notifications.Count);
        Assert.All(
            notifications,
            notification => Assert.Equal(
                "Ngựa không vượt qua kiểm tra pre-race, tiền cược đã được hoàn.",
                notification.Message));

        var retry = await service.CancelForFailedPreRaceInspectionAsync(
            race.RaceId,
            registrationId: 10,
            horseName: "Storm");

        Assert.Equal(0, retry.PredictionsAffected);
        Assert.Equal(1000, firstWallet.CurrentBettingPoints);
        Assert.Equal(1000, secondWallet.CurrentBettingPoints);
        Assert.Equal(2, await context.Notifications.CountAsync());
    }

    private static EliteRacingLeagueContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<EliteRacingLeagueContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings =>
                warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new EliteRacingLeagueContext(options);
    }

    private static User User(int id, string name, string role, int bettingPoints) => new()
    {
        UserId = id,
        FullName = name,
        Email = $"{id}@example.test",
        PasswordHash = "test",
        Role = role,
        Status = UserStatuses.Active,
        BettingPoints = bettingPoints,
        CreatedAt = DateTime.UtcNow
    };

    private static SpectatorSeasonWallet Wallet(
        int id,
        int seasonId,
        User spectator,
        int bettingPoints,
        int seasonScore,
        int recoveryPoints) => new()
    {
        SpectatorSeasonWalletId = id,
        SeasonId = seasonId,
        SpectatorId = spectator.UserId,
        Spectator = spectator,
        OpeningBettingPoints = 1000,
        CurrentBettingPoints = bettingPoints,
        SeasonScore = seasonScore,
        PendingRecoveryPoints = recoveryPoints,
        Status = SeasonWalletStatuses.Active,
        OpenedAt = DateTime.UtcNow
    };

    private static RacePrediction Prediction(
        int id,
        int raceId,
        int spectatorId,
        int registrationId,
        int stakePoints,
        string status,
        DateTime now) => new()
    {
        PredictionId = id,
        RaceId = raceId,
        SpectatorId = spectatorId,
        PredictedRegistrationId = registrationId,
        Status = status,
        StakePoints = stakePoints,
        PointsAwarded = 0,
        PredictedAt = now,
        CreatedAt = now
    };
}
