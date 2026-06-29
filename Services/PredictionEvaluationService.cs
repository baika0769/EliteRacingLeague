using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Services;

public class PredictionEvaluationService
{
    private readonly EliteRacingLeagueContext _context;
    private static readonly string[] WinnerResultStatuses =
    {
        RaceResultStatuses.AdminApproved,
        RaceResultStatuses.Published
    };

    public PredictionEvaluationService(EliteRacingLeagueContext context)
    {
        _context = context;
    }

    public async Task EvaluateRacePredictionsAsync(int raceId)
    {
        var winner = await _context.RaceResults
            .AsNoTracking()
            .Where(r =>
                r.RaceId == raceId &&
                r.FinishPosition == 1 &&
                WinnerResultStatuses.Contains(r.Status))
            .Select(r => new
            {
                r.RegistrationId
            })
            .FirstOrDefaultAsync();

        if (winner == null)
        {
            return;
        }

        var pointsPerCorrectPrediction = await _context.Races
            .AsNoTracking()
            .Where(r => r.RaceId == raceId)
            .Select(r => (int?)r.Tournament.Season.PointsPerCorrectPrediction)
            .FirstOrDefaultAsync() ?? 100;

        var predictions = await _context.RacePredictions
            .Where(p =>
                p.RaceId == raceId &&
                p.Status != RacePredictionStatuses.Evaluated &&
                p.Status != RacePredictionStatuses.Cancelled)
            .ToListAsync();

        if (predictions.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;

        foreach (var prediction in predictions)
        {
            var isCorrect = prediction.PredictedRegistrationId == winner.RegistrationId;

            prediction.ActualWinnerRegistrationId = winner.RegistrationId;
            prediction.IsCorrect = isCorrect;
            prediction.PointsAwarded = isCorrect ? pointsPerCorrectPrediction : 0;
            prediction.Status = RacePredictionStatuses.Evaluated;
            prediction.RewardStatus = isCorrect
                ? PredictionRewardStatuses.Pending
                : PredictionRewardStatuses.None;
            prediction.EvaluatedAt = now;
            prediction.UpdatedAt = now;

            if (isCorrect)
            {
                _context.Notifications.Add(new Notification
                {
                    UserId = prediction.SpectatorId,
                    Title = "Prediction Correct",
                    Message = "You predicted the winner correctly.",
                    IsRead = false,
                    CreatedAt = now,
                    ActionType = "SpectatorRewards",
                    ActionUrl = "/spectator/rewards",
                    RelatedType = "RacePrediction",
                    RelatedId = prediction.PredictionId
                });
            }
        }

        await _context.SaveChangesAsync();
    }
}
