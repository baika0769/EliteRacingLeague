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
            var prediction = await _context.RacePredictions.FindAsync(id);

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

            prediction.Status = request.Status;
            prediction.UpdatedAt = DateTime.UtcNow;

            if (request.Status == RacePredictionStatuses.Locked)
            {
                prediction.LockedAt = DateTime.UtcNow;
            }

            if (request.Status == RacePredictionStatuses.Evaluated)
            {
                prediction.EvaluatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Prediction status updated successfully",
                id = prediction.PredictionId,
                status = prediction.Status
            });
        }
    }

    public class UpdatePredictionStatusRequest
    {
        public string Status { get; set; } = string.Empty;
    }
}
