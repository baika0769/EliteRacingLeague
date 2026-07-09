using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;

namespace Eliteracingleague.API.Controllers.Admin
{
    [Authorize(Roles = UserRoles.Admin)]
    [ApiController]
    [Route("api/admin/predictions")]
    public class AdminPredictionsController : ControllerBase
    {
        private readonly EliteRacingLeagueContext _context;

        public AdminPredictionsController(EliteRacingLeagueContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetPredictions()
        {
            var predictions = await _context.RacePredictions
                .Include(p => p.Race)
                    .ThenInclude(r => r.Tournament)
                .Include(p => p.Spectator)
                .Include(p => p.PredictedRegistration)
                    .ThenInclude(r => r.Horse)
                .OrderByDescending(p => p.PredictedAt)
                .Select(p => new
                {
                    id = p.PredictionId,
                    tournament = p.Race.Tournament.TournamentName,
                    spectator = p.Spectator.FullName,
                    horse = p.PredictedRegistration.Horse.HorseName,
                    count = 1,
                    status = p.Status,
                    accuracy = p.IsCorrect == true
                        ? "High Accuracy"
                        : p.IsCorrect == false
                            ? "Low Accuracy"
                            : RacePredictionStatuses.Pending,
                    stakePoints = p.StakePoints,
                    payoutPoints = p.PointsAwarded,
                    pointsAwarded = p.PointsAwarded,
                    netPoints = p.Status == RacePredictionStatuses.Evaluated
                        ? p.PointsAwarded - p.StakePoints
                        : -p.StakePoints,
                    rewardAmount = p.RewardAmount,
                    rewardStatus = p.RewardStatus,
                    predictedAt = p.PredictedAt
                })
                .ToListAsync();

            return Ok(predictions);
        }

        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdatePredictionStatus(
            int id,
            [FromBody] UpdatePredictionStatusRequest request)
        {
            var prediction = await _context.RacePredictions
                .Include(p => p.Race)
                    .ThenInclude(r => r.Tournament)
                .Include(p => p.Spectator)
                .FirstOrDefaultAsync(p => p.PredictionId == id);

            if (prediction == null)
            {
                return NotFound(new
                {
                    message = "Prediction not found",
                    id
                });
            }

            if (!RacePredictionStatuses.All.Contains(request.Status))
            {
                return BadRequest(new
                {
                    message = "Invalid prediction status"
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

            if (request.Status == RacePredictionStatuses.Evaluated)
            {
                return BadRequest(new
                {
                    message = "Predictions must be evaluated by the official result publish flow, not manually from admin status update.",
                    id,
                    raceStatus = prediction.Race.Status,
                    tournamentStatus = prediction.Race.Tournament.Status
                });
            }

            var now = DateTime.UtcNow;

            if (request.Status == RacePredictionStatuses.Cancelled &&
                prediction.Status != RacePredictionStatuses.Cancelled &&
                prediction.Status != RacePredictionStatuses.Evaluated)
            {
                if (prediction.StakePoints > 0)
                {
                    prediction.Spectator.BettingPoints += prediction.StakePoints;
                    prediction.Spectator.UpdatedAt = now;
                }

                prediction.RewardStatus = PredictionRewardStatuses.None;
                prediction.PointsAwarded = 0;
                prediction.IsCorrect = null;
            }

            prediction.Status = request.Status;
            prediction.UpdatedAt = now;

            if (request.Status == RacePredictionStatuses.Locked)
            {
                prediction.LockedAt = now;
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Prediction status updated successfully",
                id = prediction.PredictionId,
                status = prediction.Status,
                bettingPoints = prediction.Spectator.BettingPoints
            });
        }
    }

    public class UpdatePredictionStatusRequest
    {
        public string Status { get; set; } = string.Empty;
    }
}
