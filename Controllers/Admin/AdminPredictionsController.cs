using System.Data;
using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Controllers.Admin;

[Authorize(Roles = UserRoles.Admin)]
[ApiController]
[Route("api/admin/predictions")]
public class AdminPredictionsController : ControllerBase
{
    private readonly EliteRacingLeagueContext _context;
    private readonly PredictionEvaluationService _predictionEvaluationService;
    private readonly SpectatorWalletService _spectatorWalletService;

    public AdminPredictionsController(
        EliteRacingLeagueContext context,
        PredictionEvaluationService predictionEvaluationService,
        SpectatorWalletService spectatorWalletService)
    {
        _context = context;
        _predictionEvaluationService = predictionEvaluationService;
        _spectatorWalletService = spectatorWalletService;
    }

    [HttpGet]
    public async Task<IActionResult> GetPredictions(
        [FromQuery] int? raceId,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(status) &&
            !RacePredictionStatuses.IsValid(status))
        {
            return BadRequest(new
            {
                message = "Invalid prediction status."
            });
        }

        var query = _context.RacePredictions
            .AsNoTracking()
            .AsQueryable();

        if (raceId.HasValue)
        {
            query = query.Where(prediction =>
                prediction.RaceId == raceId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(prediction =>
                prediction.Status == status);
        }

        var predictions = await query
            .OrderByDescending(prediction => prediction.PredictedAt)
            .Select(prediction => new
            {
                id = prediction.PredictionId,
                raceId = prediction.RaceId,
                tournamentId = prediction.Race.TournamentId,
                tournament = prediction.Race.Tournament.TournamentName,
                spectatorId = prediction.SpectatorId,
                spectator = prediction.Spectator.FullName,
                horseId = prediction.PredictedRegistration.HorseId,
                horse = prediction.PredictedRegistration.Horse.HorseName,
                status = prediction.Status,
                isCorrect = prediction.IsCorrect,
                accuracy = prediction.IsCorrect == true
                    ? "Correct"
                    : prediction.IsCorrect == false
                        ? "Incorrect"
                        : "Pending",
                stakePoints = prediction.StakePoints,
                payoutPoints = prediction.PointsAwarded,
                netPoints = prediction.Status == RacePredictionStatuses.Evaluated
                    ? prediction.PointsAwarded - prediction.StakePoints
                    : -prediction.StakePoints,
                rewardAmount = prediction.RewardAmount,
                rewardStatus = prediction.RewardStatus,
                predictedAt = prediction.PredictedAt,
                lockedAt = prediction.LockedAt,
                evaluatedAt = prediction.EvaluatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(predictions);
    }

    [HttpPost("races/{raceId:int}/evaluate")]
    public async Task<IActionResult> EvaluateRacePredictions(
        int raceId,
        CancellationToken cancellationToken)
    {
        var evaluation = await _predictionEvaluationService
            .EvaluateRacePredictionsAsync(raceId, cancellationToken);

        if (!evaluation.Success)
        {
            return BadRequest(evaluation);
        }

        return Ok(evaluation);
    }

    [HttpPut("{id:int}/status")]
    public async Task<IActionResult> UpdatePredictionStatus(
        int id,
        [FromBody] UpdatePredictionStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Status) ||
            !RacePredictionStatuses.IsValid(request.Status))
        {
            return BadRequest(new
            {
                message = "Invalid prediction status."
            });
        }

        if (request.Status == RacePredictionStatuses.Evaluated)
        {
            return BadRequest(new
            {
                message = "Predictions must be evaluated by the official result flow or the race evaluation endpoint."
            });
        }

        if (request.Status == RacePredictionStatuses.Pending)
        {
            return BadRequest(new
            {
                message = "A locked or cancelled prediction cannot be moved back to Pending."
            });
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        try
        {
            var prediction = await _context.RacePredictions
                .Include(item => item.Race)
                    .ThenInclude(race => race.Tournament)
                .Include(item => item.Spectator)
                .FirstOrDefaultAsync(
                    item => item.PredictionId == id,
                    cancellationToken);

            if (prediction == null)
            {
                return NotFound(new
                {
                    message = "Prediction not found.",
                    id
                });
            }

            if (prediction.Status == RacePredictionStatuses.Evaluated)
            {
                return BadRequest(new
                {
                    message = "Evaluated predictions cannot be changed manually.",
                    id,
                    status = prediction.Status
                });
            }

            if (prediction.Status == RacePredictionStatuses.Cancelled)
            {
                return BadRequest(new
                {
                    message = "Cancelled predictions cannot be changed manually.",
                    id,
                    status = prediction.Status
                });
            }

            if (request.Status == RacePredictionStatuses.Locked &&
                prediction.Status != RacePredictionStatuses.Pending)
            {
                return BadRequest(new
                {
                    message = "Only a Pending prediction can be locked.",
                    id,
                    currentStatus = prediction.Status
                });
            }

            if (request.Status == RacePredictionStatuses.Cancelled &&
                prediction.Race.Status == RaceStatuses.Published)
            {
                return BadRequest(new
                {
                    message = "A prediction cannot be cancelled after the race has been published.",
                    id,
                    raceStatus = prediction.Race.Status
                });
            }

            var now = DateTime.UtcNow;

            if (request.Status == RacePredictionStatuses.Cancelled)
            {
                if (prediction.StakePoints > 0)
                {
                    var wallet = await _spectatorWalletService.GetOrCreateWalletAsync(
                        prediction.Race.Tournament.SeasonId,
                        prediction.Spectator,
                        prediction.Spectator.BettingPoints,
                        now,
                        cancellationToken);

                    await _spectatorWalletService.ApplyAsync(
                        wallet,
                        prediction.Spectator,
                        PointTransactionTypes.PredictionRefund,
                        prediction.StakePoints,
                        scoreDelta: 0,
                        idempotencyKey: $"PREDICTION_REFUND_{prediction.PredictionId}",
                        referenceType: "RacePrediction",
                        referenceId: prediction.PredictionId,
                        description: $"Refund for cancelled prediction #{prediction.PredictionId}.",
                        now: now,
                        cancellationToken: cancellationToken);
                }

                prediction.Status = RacePredictionStatuses.Cancelled;
                prediction.ActualWinnerRegistrationId = null;
                prediction.IsCorrect = null;
                prediction.PointsAwarded = 0;
                prediction.RewardAmount = null;
                prediction.RewardStatus = PredictionRewardStatuses.None;
                prediction.EvaluatedAt = null;
                prediction.UpdatedAt = now;
            }
            else
            {
                prediction.Status = RacePredictionStatuses.Locked;
                prediction.LockedAt = now;
                prediction.UpdatedAt = now;
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Ok(new
            {
                message = "Prediction status updated successfully.",
                id = prediction.PredictionId,
                status = prediction.Status,
                bettingPoints = prediction.Spectator.BettingPoints
            });
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

public class UpdatePredictionStatusRequest
{
    public string Status { get; set; } = string.Empty;
}