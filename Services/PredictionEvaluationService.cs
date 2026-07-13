using System.Data;
using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.Models;
using Eliteracingleague.API.Services.SystemTime;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Services;

public class PredictionEvaluationService
{
    private readonly EliteRacingLeagueContext _context;
    private readonly IDateTimeProvider _dateTimeProvider;

    public PredictionEvaluationService(
        EliteRacingLeagueContext context,
        IDateTimeProvider dateTimeProvider)
    {
        _context = context;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<PredictionEvaluationResult> EvaluateRacePredictionsAsync(
        int raceId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

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
                    SeasonStatus = r.Tournament.Season.Status,
                    PointsPerCorrectPrediction =
                        (int?)r.Tournament.Season.PointsPerCorrectPrediction
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (raceInfo == null)
            {
                return PredictionEvaluationResult.Fail(
                    raceId,
                    "Race not found.");
            }

            if (raceInfo.Status != RaceStatuses.Published ||
                raceInfo.TournamentStatus != TournamentStatuses.Completed)
            {
                return PredictionEvaluationResult.Fail(
                    raceId,
                    "Predictions can only be evaluated after the race is published and the tournament is completed.");
            }

            var winner = await _context.RaceResults
                .AsNoTracking()
                .Where(result =>
                    result.RaceId == raceId &&
                    result.Status == RaceResultStatuses.Published &&
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

            var pointsPerCorrectPrediction =
                raceInfo.PointsPerCorrectPrediction.GetValueOrDefault(100);

            if (pointsPerCorrectPrediction <= 0)
            {
                pointsPerCorrectPrediction = 100;
            }

            var allCorrectPredictions = predictions
                .Where(prediction =>
                    prediction.PredictedRegistrationId == winner.RegistrationId)
                .ToList();

            var payouts = CalculatePariMutuelPayouts(
                predictions,
                allCorrectPredictions,
                pointsPerCorrectPrediction);

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

                var payoutPoints = payouts.GetValueOrDefault(
                    prediction.PredictionId,
                    0);

                prediction.ActualWinnerRegistrationId = winner.RegistrationId;
                prediction.IsCorrect = isCorrect;
                prediction.PointsAwarded = payoutPoints;
                prediction.Status = RacePredictionStatuses.Evaluated;
                prediction.RewardStatus = isCorrect
                    ? PredictionRewardStatuses.Paid
                    : PredictionRewardStatuses.None;
                prediction.EvaluatedAt = now;
                prediction.UpdatedAt = now;

                if (isCorrect)
                {
                    newlyCorrectPredictions++;

                    if (payoutPoints > 0)
                    {
                        prediction.Spectator.BettingPoints += payoutPoints;
                        prediction.Spectator.UpdatedAt = now;
                        newlyPaidPoints += payoutPoints;
                    }

                    _context.Notifications.Add(new Notification
                    {
                        UserId = prediction.SpectatorId,
                        Title = "Bet Won",
                        Message = payoutPoints > 0
                            ? $"Your prediction was correct. {winner.HorseName} won and you received {payoutPoints} points."
                            : $"Your prediction was correct. {winner.HorseName} won.",
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
                        Message = $"Your prediction was not correct. {winner.HorseName} won, so your {prediction.StakePoints} staked points were lost.",
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
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static Dictionary<int, int> CalculatePariMutuelPayouts(
        IReadOnlyCollection<RacePrediction> allPredictions,
        IReadOnlyCollection<RacePrediction> correctPredictions,
        int legacyPointsPerCorrectPrediction)
    {
        var payouts = allPredictions.ToDictionary(
            prediction => prediction.PredictionId,
            _ => 0);

        if (correctPredictions.Count == 0)
        {
            return payouts;
        }

        var totalPool = allPredictions.Sum(
            prediction => Math.Max(0, prediction.StakePoints));

        var totalCorrectStake = correctPredictions.Sum(
            prediction => Math.Max(0, prediction.StakePoints));

        // Compatibility for predictions created before stake points were added.
        if (totalPool <= 0 || totalCorrectStake <= 0)
        {
            foreach (var prediction in correctPredictions)
            {
                payouts[prediction.PredictionId] = legacyPointsPerCorrectPrediction;
            }

            return payouts;
        }

        var remainingPoints = totalPool;

        var allocatedRows = correctPredictions
            .Select(prediction =>
            {
                var exactNumerator =
                    (long)totalPool * Math.Max(0, prediction.StakePoints);

                var basePayout = (int)(exactNumerator / totalCorrectStake);
                var remainder = exactNumerator % totalCorrectStake;

                remainingPoints -= basePayout;

                return new
                {
                    Prediction = prediction,
                    BasePayout = basePayout,
                    Remainder = remainder
                };
            })
            .OrderByDescending(row => row.Remainder)
            .ThenByDescending(row => row.Prediction.StakePoints)
            .ThenBy(row => row.Prediction.PredictionId)
            .ToList();

        foreach (var row in allocatedRows)
        {
            payouts[row.Prediction.PredictionId] = row.BasePayout;
        }

        for (var index = 0;
             index < remainingPoints && index < allocatedRows.Count;
             index++)
        {
            payouts[allocatedRows[index].Prediction.PredictionId] += 1;
        }

        return payouts;
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