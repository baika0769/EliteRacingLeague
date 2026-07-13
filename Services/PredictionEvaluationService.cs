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

    private static readonly string[] WinnerResultStatuses =
    {
        RaceResultStatuses.Published
    };

    public PredictionEvaluationService(
        EliteRacingLeagueContext context,
        IDateTimeProvider dateTimeProvider)
    {
        _context = context;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task EvaluateRacePredictionsAsync(int raceId)
    {
        var raceInfo = await _context.Races
            .AsNoTracking()
            .Where(r => r.RaceId == raceId)
            .Select(r => new
            {
                r.Status,
                TournamentStatus = r.Tournament.Status,
                PointsPerCorrectPrediction = (int?)r.Tournament.Season.PointsPerCorrectPrediction
            })
            .FirstOrDefaultAsync();

        if (raceInfo == null ||
            raceInfo.Status != RaceStatuses.Published ||
            raceInfo.TournamentStatus != TournamentStatuses.Completed)
        {
            return;
        }

        var winner = await _context.RaceResults
     .AsNoTracking()
     .Where(r =>
         r.RaceId == raceId &&
         WinnerResultStatuses.Contains(r.Status) &&
         r.FinishPosition.HasValue &&
         !_context.RaceViolations.Any(v =>
             v.RaceId == r.RaceId &&
             v.RegistrationId == r.RegistrationId &&
             v.Action == RaceViolationActions.Disqualified))
     .OrderBy(r => r.FinishPosition)
     .Select(r => new
     {
         r.RegistrationId,
         HorseName = r.Registration.Horse.HorseName
     })
     .FirstOrDefaultAsync();

        if (winner == null)
        {
            return;
        }

        var pointsPerCorrectPrediction = raceInfo.PointsPerCorrectPrediction ?? 100;

        var predictions = await _context.RacePredictions
            .Include(p => p.Spectator)
            .Where(p =>
                p.RaceId == raceId &&
                p.Status != RacePredictionStatuses.Evaluated &&
                p.Status != RacePredictionStatuses.Cancelled)
            .OrderBy(p => p.PredictionId)
            .ToListAsync();

        if (predictions.Count == 0)
        {
            return;
        }

        var now = _dateTimeProvider.UtcNow;

        foreach (var pendingPrediction in predictions.Where(p => p.Status == RacePredictionStatuses.Pending))
        {
            pendingPrediction.Status = RacePredictionStatuses.Locked;
            pendingPrediction.LockedAt = now;
            pendingPrediction.UpdatedAt = now;
        }

        var evaluablePredictions = predictions
            .Where(p => p.Status == RacePredictionStatuses.Locked)
            .ToList();

        if (evaluablePredictions.Count == 0)
        {
            await _context.SaveChangesAsync();
            return;
        }

        var correctPredictions = evaluablePredictions
            .Where(p => p.PredictedRegistrationId == winner.RegistrationId)
            .ToList();

        var payouts = CalculatePariMutuelPayouts(
            evaluablePredictions,
            correctPredictions,
            pointsPerCorrectPrediction);

        foreach (var prediction in evaluablePredictions)
        {
            var isCorrect = prediction.PredictedRegistrationId == winner.RegistrationId;
            var payoutPoints = payouts.GetValueOrDefault(prediction.PredictionId, 0);

            prediction.ActualWinnerRegistrationId = winner.RegistrationId;
            prediction.IsCorrect = isCorrect;
            prediction.PointsAwarded = payoutPoints;
            prediction.Status = RacePredictionStatuses.Evaluated;
            prediction.RewardStatus = isCorrect
                ? PredictionRewardStatuses.Paid
                : PredictionRewardStatuses.None;
            prediction.EvaluatedAt = now;
            prediction.UpdatedAt = now;

            if (isCorrect && payoutPoints > 0)
            {
                prediction.Spectator.BettingPoints += payoutPoints;
                prediction.Spectator.UpdatedAt = now;

                _context.Notifications.Add(new Notification
                {
                    UserId = prediction.SpectatorId,
                    Title = "Bet Won",
                    Message = $"Your bet was correct. {winner.HorseName} won and you received {payoutPoints} points.",
                    IsRead = false,
                    CreatedAt = now,
                    ActionType = "SpectatorRewards",
                    ActionUrl = "/spectator/results",
                    RelatedType = "RacePrediction",
                    RelatedId = prediction.PredictionId
                });
            }

            if (!isCorrect && prediction.StakePoints > 0)
            {
                _context.Notifications.Add(new Notification
                {
                    UserId = prediction.SpectatorId,
                    Title = "Bet Lost",
                    Message = $"Your bet was not correct. {winner.HorseName} won, so your {prediction.StakePoints} staked points were lost.",
                    IsRead = false,
                    CreatedAt = now,
                    ActionType = "SpectatorPredictions",
                    ActionUrl = "/spectator/predictions",
                    RelatedType = "RacePrediction",
                    RelatedId = prediction.PredictionId
                });
            }
        }

        await _context.SaveChangesAsync();
    }

    private static Dictionary<int, int> CalculatePariMutuelPayouts(
        List<RacePrediction> allPredictions,
        List<RacePrediction> correctPredictions,
        int legacyPointsPerCorrectPrediction)
    {
        var payouts = allPredictions.ToDictionary(p => p.PredictionId, _ => 0);

        if (correctPredictions.Count == 0)
        {
            return payouts;
        }

        var totalPool = allPredictions.Sum(p => Math.Max(0, p.StakePoints));
        var totalCorrectStake = correctPredictions.Sum(p => Math.Max(0, p.StakePoints));

        // Legacy safety: old predictions created before stake_points existed can still be rewarded.
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
            .Select(p =>
            {
                var exactNumerator = (long)totalPool * Math.Max(0, p.StakePoints);
                var basePayout = (int)(exactNumerator / totalCorrectStake);
                var remainder = exactNumerator % totalCorrectStake;

                remainingPoints -= basePayout;

                return new
                {
                    Prediction = p,
                    BasePayout = basePayout,
                    Remainder = remainder
                };
            })
            .OrderByDescending(x => x.Remainder)
            .ThenByDescending(x => x.Prediction.StakePoints)
            .ThenBy(x => x.Prediction.PredictionId)
            .ToList();

        foreach (var row in allocatedRows)
        {
            payouts[row.Prediction.PredictionId] = row.BasePayout;
        }

        for (var i = 0; i < remainingPoints && i < allocatedRows.Count; i++)
        {
            payouts[allocatedRows[i].Prediction.PredictionId] += 1;
        }

        return payouts;
    }
}
