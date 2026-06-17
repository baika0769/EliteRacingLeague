using System.Security.Claims;
using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Spectator;
using Eliteracingleague.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Controllers.Spectator;

[Authorize]
[ApiController]
[Route("api/spectator/predictions")]
public class SpectatorPredictionsController : ControllerBase
{
    private readonly EliteRacingLeagueContext _context;

    public SpectatorPredictionsController(EliteRacingLeagueContext context)
    {
        _context = context;
    }

    private int GetUserId()
        => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    [HttpPost]
    public async Task<IActionResult> CreatePrediction(CreatePredictionRequest request)
    {
        var spectatorId = GetUserId();

        var race = await _context.Races.FirstOrDefaultAsync(r => r.RaceId == request.RaceId);

        if (race == null)
            return NotFound("Race not found.");

        if (race.Status == RaceStatuses.Cancelled ||
            race.Status == RaceStatuses.Ongoing ||
            race.Status == RaceStatuses.Completed ||
            race.Status == RaceStatuses.ResultPending ||
            race.Status == RaceStatuses.Published)
            return BadRequest("Prediction is not allowed for this race status.");

        var registration = await _context.RaceRegistrations
            .FirstOrDefaultAsync(r =>
                r.RegistrationId == request.PredictedRegistrationId &&
                r.RaceId == request.RaceId &&
                r.Status == RaceRegistrationStatuses.Approved);

        if (registration == null)
            return BadRequest("Invalid predicted registration.");

        var existing = await _context.RacePredictions
            .FirstOrDefaultAsync(p =>
                p.RaceId == request.RaceId &&
                p.SpectatorId == spectatorId);

        if (existing != null)
            return BadRequest("You already predicted this race.");

        var prediction = new RacePrediction
        {
            RaceId = request.RaceId,
            SpectatorId = spectatorId,
            PredictedRegistrationId = request.PredictedRegistrationId,
            Status = RacePredictionStatuses.Pending,
            IsCorrect = null,
            PointsAwarded = 0,
            RewardStatus = PredictionRewardStatuses.None,
            PredictedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        _context.RacePredictions.Add(prediction);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Prediction submitted successfully",
            predictionId = prediction.PredictionId,
            status = prediction.Status
        });
    }

    [HttpGet("my")]
    public async Task<IActionResult> GetMyPredictions()
    {
        var spectatorId = GetUserId();

        var predictions = await _context.RacePredictions
            .Where(p => p.SpectatorId == spectatorId)
            .OrderByDescending(p => p.PredictedAt)
            .Select(p => new
            {
                predictionId = p.PredictionId,
                raceId = p.RaceId,
                raceName = p.Race.RaceName,
                tournamentName = p.Race.Tournament.TournamentName,
                predictedRegistrationId = p.PredictedRegistrationId,
                predictedHorseName = p.PredictedRegistration.Horse.HorseName,
                actualWinnerRegistrationId = p.ActualWinnerRegistrationId,
                actualWinnerHorseName = p.ActualWinnerRegistration != null
                    ? p.ActualWinnerRegistration.Horse.HorseName
                    : null,
                status = p.Status,
                isCorrect = p.IsCorrect,
                pointsAwarded = p.PointsAwarded,
                rewardAmount = p.RewardAmount,
                rewardStatus = p.RewardStatus,
                predictedAt = p.PredictedAt,
                evaluatedAt = p.EvaluatedAt
            })
            .ToListAsync();

        return Ok(predictions);
    }
}