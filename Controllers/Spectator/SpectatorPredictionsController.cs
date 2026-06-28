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
    private static readonly string[] PredictableRegistrationStatuses =
    {
        RaceRegistrationStatuses.Approved,
        RaceRegistrationStatuses.JockeyInvited,
        RaceRegistrationStatuses.ReadyToRace
    };

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
        var now = DateTime.UtcNow;

        var tournament = await _context.Tournaments
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TournamentId == request.TournamentId);

        if (tournament == null)
            return NotFound("Tournament not found.");

        if (tournament.Status is TournamentStatuses.Cancelled or TournamentStatuses.Completed)
            return BadRequest("Prediction is not allowed for this tournament status.");

        var race = await _context.Races
            .AsNoTracking()
            .Where(r =>
                r.TournamentId == request.TournamentId &&
                r.Status != RaceStatuses.Cancelled)
            .OrderBy(r => r.RaceDate)
            .FirstOrDefaultAsync();

        if (race == null)
            return NotFound("Race not found.");

        if (RaceStatuses.IsClosedForPrediction(race.Status))
            return BadRequest("Prediction is not allowed for this race status.");

        if (race.PredictionDeadline.HasValue && now > race.PredictionDeadline.Value)
            return BadRequest("Prediction deadline has passed.");

        var existing = await _context.RacePredictions
            .AsNoTracking()
            .AnyAsync(p =>
                p.SpectatorId == spectatorId &&
                p.Status != RacePredictionStatuses.Cancelled &&
                p.Race.TournamentId == request.TournamentId);

        if (existing)
            return Conflict(new
            {
                error = "You have already predicted for this tournament.",
                message = "You have already predicted for this tournament."
            });

        var registration = await _context.RaceRegistrations
            .AsNoTracking()
            .FirstOrDefaultAsync(r =>
                r.RaceId == race.RaceId &&
                r.HorseId == request.PredictedHorseId &&
                PredictableRegistrationStatuses.Contains(r.Status));

        if (registration == null)
            return BadRequest(new
            {
                error = "Horse is not registered in this tournament.",
                message = "Horse is not registered in this tournament."
            });

        var prediction = new RacePrediction
        {
            RaceId = race.RaceId,
            SpectatorId = spectatorId,
            PredictedRegistrationId = registration.RegistrationId,
            Status = RacePredictionStatuses.Pending,
            IsCorrect = null,
            PointsAwarded = 0,
            RewardStatus = PredictionRewardStatuses.None,
            PredictedAt = now,
            CreatedAt = now
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
                tournamentId = p.Race.TournamentId,
                tournamentName = p.Race.Tournament.TournamentName,
                tournamentStatus = p.Race.Tournament.Status,
                raceId = p.RaceId,
                raceName = p.Race.RaceName,
                predictedHorseId = p.PredictedRegistration.HorseId,
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
