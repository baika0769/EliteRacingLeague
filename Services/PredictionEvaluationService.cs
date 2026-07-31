using System.Data;
using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.Models;
using Eliteracingleague.API.Services.SystemTime;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Eliteracingleague.API.Services;

public class PredictionEvaluationService
{
    private readonly EliteRacingLeagueContext _context;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly SpectatorWalletService _spectatorWalletService;

    public PredictionEvaluationService(
        EliteRacingLeagueContext context,
        IDateTimeProvider dateTimeProvider,
        SpectatorWalletService spectatorWalletService)
    {
        _context = context;
        _dateTimeProvider = dateTimeProvider;
        _spectatorWalletService = spectatorWalletService;
    }

    public async Task<PredictionEvaluationResult> EvaluateRacePredictionsAsync(
        int raceId,
        CancellationToken cancellationToken = default)
    {
        IDbContextTransaction? transaction = null;
        if (_context.Database.CurrentTransaction == null)
        {
            transaction = await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
        }

        try
        {
            var raceInfo = await _context.Races
                .AsNoTracking()
                .Where(r => r.RaceId == raceId)
                .Select(r => new
                {
                    r.RaceId,
                    r.Status,
                    TournamentStatus = r.Tournament.Status,
                    SeasonId = r.Tournament.SeasonId,
                    SeasonStatus = r.Tournament.Season.Status,
                    r.LifecycleVersion
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (raceInfo == null)
            {
                return PredictionEvaluationResult.Fail(
                    raceId,
                    "Race not found.");
            }

            if (raceInfo.Status != RaceStatuses.Published ||
                raceInfo.TournamentStatus == TournamentStatuses.Cancelled ||
                raceInfo.SeasonStatus is SeasonStatuses.Settling or SeasonStatuses.Closed or SeasonStatuses.Cancelled)
            {
                return PredictionEvaluationResult.Fail(
                    raceId,
                    "Predictions can only be evaluated for a published race in an active season.");
            }

            var winner = await _context.RaceResults
                .AsNoTracking()
                .Where(result =>
                    result.RaceId == raceId &&
                    result.Status == RaceResultStatuses.Published &&
                    result.OutcomeStatus == RaceOutcomeStatuses.Finished &&
                    result.FinishPosition.HasValue &&
                    !_context.RaceViolations.Any(violation =>
                        violation.RaceId == result.RaceId &&
                        violation.RegistrationId == result.RegistrationId &&
                        violation.Action == RaceViolationActions.Disqualified))
                .OrderBy(result => result.FinishPosition)
                .ThenBy(result => result.FinishTimeSeconds)
                .ThenBy(result => result.ResultId)
                .Select(result => new
                {
                    result.RegistrationId,
                    HorseName = result.Registration.Horse.HorseName
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (winner == null)
            {
                return PredictionEvaluationResult.Fail(
                    raceId,
                    "No valid published winner was found for this race.");
            }

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

                return PredictionEvaluationResult.Successful(
                    raceId,
                    winner.RegistrationId,
                    winner.HorseName,
                    totalPredictions: 0,
                    newlyEvaluated: 0,
                    correctPredictions: 0,
                    totalPayoutPoints: 0,
                    alreadyEvaluated: true,
                    message: "The race has no predictions to evaluate.");
            }

            var unsupportedPrediction = predictions.FirstOrDefault(prediction =>
                prediction.Status != RacePredictionStatuses.Pending &&
                prediction.Status != RacePredictionStatuses.Locked &&
                prediction.Status != RacePredictionStatuses.Evaluated);

            if (unsupportedPrediction != null)
            {
                return PredictionEvaluationResult.Fail(
                    raceId,
                    $"Prediction #{unsupportedPrediction.PredictionId} has unsupported status '{unsupportedPrediction.Status}'.");
            }

            var predictionsToEvaluate = predictions
                .Where(prediction =>
                    prediction.Status == RacePredictionStatuses.Pending ||
                    prediction.Status == RacePredictionStatuses.Locked)
                .ToList();

            if (predictionsToEvaluate.Count == 0)
            {
                if (transaction != null)
                    await transaction.CommitAsync(cancellationToken);

                return PredictionEvaluationResult.Successful(
                    raceId,
                    winner.RegistrationId,
                    winner.HorseName,
                    totalPredictions: predictions.Count,
                    newlyEvaluated: 0,
                    correctPredictions: predictions.Count(prediction => prediction.IsCorrect == true),
                    totalPayoutPoints: predictions.Sum(prediction => prediction.PointsAwarded),
                    alreadyEvaluated: true,
                    message: "All predictions for this race were already evaluated.");
            }

            var now = _dateTimeProvider.UtcNow;
            var newlyCorrectPredictions = 0;
            var newlyPaidPoints = 0;

            foreach (var prediction in predictionsToEvaluate)
            {
                if (prediction.Status == RacePredictionStatuses.Pending)
                {
                    prediction.Status = RacePredictionStatuses.Locked;
                    prediction.LockedAt = now;
                    prediction.UpdatedAt = now;
                }

                var isCorrect =
                    prediction.PredictedRegistrationId == winner.RegistrationId;

                var payoutPoints = isCorrect
                    ? SpectatorBettingRules.CalculateWinGrossPayout(prediction.StakePoints)
                    : 0;
                var scoreDelta = isCorrect
                    ? SpectatorBettingRules.CalculateWinScoreDelta(prediction.StakePoints)
                    : SpectatorBettingRules.CalculateLossScoreDelta(prediction.StakePoints);

                prediction.ActualWinnerRegistrationId = winner.RegistrationId;
                prediction.IsCorrect = isCorrect;
                prediction.PointsAwarded = payoutPoints;
                prediction.Status = RacePredictionStatuses.Evaluated;
                prediction.RewardStatus = isCorrect
                    ? PredictionRewardStatuses.Paid
                    : PredictionRewardStatuses.None;
                prediction.EvaluatedAt = now;
                prediction.UpdatedAt = now;

                var wallet = await _spectatorWalletService.GetOrCreateWalletAsync(
                    raceInfo.SeasonId,
                    prediction.Spectator,
                    prediction.Spectator.BettingPoints,
                    now,
                    cancellationToken);

                var settlement = await _spectatorWalletService.ApplyAsync(
                    wallet,
                    prediction.Spectator,
                    isCorrect
                        ? PointTransactionTypes.PredictionWinSettlement
                        : PointTransactionTypes.PredictionLossSettlement,
                    payoutPoints,
                    scoreDelta,
                    isCorrect
                        ? $"PREDICTION_WIN_{prediction.PredictionId}_V{raceInfo.LifecycleVersion}"
                        : $"PREDICTION_LOSS_{prediction.PredictionId}_V{raceInfo.LifecycleVersion}",
                    "RacePrediction",
                    prediction.PredictionId,
                    isCorrect
                        ? $"Fixed gross payout of {payoutPoints} and season score +{scoreDelta} for winning prediction #{prediction.PredictionId}."
                        : $"Loss settlement and season score {scoreDelta} for prediction #{prediction.PredictionId}.",
                    now,
                    cancellationToken);

                if (isCorrect)
                {
                    newlyCorrectPredictions++;

                    if (!settlement.AlreadyApplied)
                    {
                        newlyPaidPoints += payoutPoints;
                    }

                    _context.Notifications.Add(new Notification
                    {
                        UserId = prediction.SpectatorId,
                        Title = "Bet Won",
                        Message = $"Correct prediction: {winner.HorseName} won. Gross wallet payout: {payoutPoints} points; net profit and season score: +{scoreDelta}.",
                        IsRead = false,
                        CreatedAt = now,
                        ActionType = "SpectatorRewards",
                        ActionUrl = "/spectator/results",
                        RelatedType = "RacePrediction",
                        RelatedId = prediction.PredictionId
                    });
                }
                else
                {
                    _context.Notifications.Add(new Notification
                    {
                        UserId = prediction.SpectatorId,
                        Title = "Bet Lost",
                        Message = $"Your prediction was not correct. {winner.HorseName} won. Wallet payout: 0; season score: {scoreDelta}.",
                        IsRead = false,
                        CreatedAt = now,
                        ActionType = "SpectatorPredictions",
                        ActionUrl = "/spectator/predictions",
                        RelatedType = "RacePrediction",
                        RelatedId = prediction.PredictionId
                    });
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
            if (transaction != null)
                await transaction.CommitAsync(cancellationToken);

            return PredictionEvaluationResult.Successful(
                raceId,
                winner.RegistrationId,
                winner.HorseName,
                totalPredictions: predictions.Count,
                newlyEvaluated: predictionsToEvaluate.Count,
                correctPredictions: newlyCorrectPredictions,
                totalPayoutPoints: newlyPaidPoints,
                alreadyEvaluated: false,
                message: "Race predictions were evaluated successfully.");
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
}

public sealed class PredictionEvaluationResult
{
    public bool Success { get; init; }
    public int RaceId { get; init; }
    public string Message { get; init; } = string.Empty;
    public int? WinnerRegistrationId { get; init; }
    public string? WinnerHorseName { get; init; }
    public int TotalPredictions { get; init; }
    public int NewlyEvaluated { get; init; }
    public int CorrectPredictions { get; init; }
    public int TotalPayoutPoints { get; init; }
    public bool AlreadyEvaluated { get; init; }

    public static PredictionEvaluationResult Fail(
        int raceId,
        string message)
    {
        return new PredictionEvaluationResult
        {
            Success = false,
            RaceId = raceId,
            Message = message
        };
    }

    public static PredictionEvaluationResult Successful(
        int raceId,
        int winnerRegistrationId,
        string winnerHorseName,
        int totalPredictions,
        int newlyEvaluated,
        int correctPredictions,
        int totalPayoutPoints,
        bool alreadyEvaluated,
        string message)
    {
        return new PredictionEvaluationResult
        {
            Success = true,
            RaceId = raceId,
            Message = message,
            WinnerRegistrationId = winnerRegistrationId,
            WinnerHorseName = winnerHorseName,
            TotalPredictions = totalPredictions,
            NewlyEvaluated = newlyEvaluated,
            CorrectPredictions = correctPredictions,
            TotalPayoutPoints = totalPayoutPoints,
            AlreadyEvaluated = alreadyEvaluated
        };
    }
}
