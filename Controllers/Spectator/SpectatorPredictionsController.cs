using System.Security.Claims;
using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Spectator;
using Eliteracingleague.API.Models;
using Eliteracingleague.API.Services.SystemTime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Controllers.Spectator;

[Authorize(Roles = UserRoles.Spectator)]
[ApiController]
[Route("api/spectator/predictions")]
public class SpectatorPredictionsController : ControllerBase
{
    private readonly EliteRacingLeagueContext _context;
    private readonly IDateTimeProvider _dateTimeProvider;

    private static readonly string[] PredictableRegistrationStatuses =
    {
        RaceRegistrationStatuses.Approved,
        RaceRegistrationStatuses.JockeyInvited,
        RaceRegistrationStatuses.ReadyToRace
    };

    public SpectatorPredictionsController(
        EliteRacingLeagueContext context,
        IDateTimeProvider dateTimeProvider)
    {
        _context = context;
        _dateTimeProvider = dateTimeProvider;
    }

    private int GetUserId()
        => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    [HttpGet("wallet")]
    public async Task<IActionResult> GetWallet()
    {
        var spectatorId = GetUserId();

        var wallet = await _context.Users
            .AsNoTracking()
            .Where(u => u.UserId == spectatorId && u.Role == UserRoles.Spectator)
            .Select(u => new
            {
                bettingPoints = u.BettingPoints,
                initialBettingPoints = SpectatorBettingRules.InitialBettingPoints,
                minimumStakePoints = SpectatorBettingRules.MinimumStakePoints,
                totalStakePoints = _context.RacePredictions
                    .Where(p => p.SpectatorId == spectatorId && p.Status != RacePredictionStatuses.Cancelled)
                    .Sum(p => (int?)p.StakePoints) ?? 0,
                totalPayoutPoints = _context.RacePredictions
                    .Where(p => p.SpectatorId == spectatorId && p.Status != RacePredictionStatuses.Cancelled)
                    .Sum(p => (int?)p.PointsAwarded) ?? 0,
                pendingStakePoints = _context.RacePredictions
                    .Where(p =>
                        p.SpectatorId == spectatorId &&
                        p.Status != RacePredictionStatuses.Cancelled &&
                        p.Status != RacePredictionStatuses.Evaluated)
                    .Sum(p => (int?)p.StakePoints) ?? 0
            })
            .FirstOrDefaultAsync();

        if (wallet == null)
        {
            return NotFound(new { message = "Spectator wallet not found." });
        }

        return Ok(new
        {
            wallet.bettingPoints,
            wallet.initialBettingPoints,
            wallet.minimumStakePoints,
            wallet.totalStakePoints,
            wallet.totalPayoutPoints,
            wallet.pendingStakePoints,
            netPoints = wallet.totalPayoutPoints - wallet.totalStakePoints
        });
    }

    [HttpPost]
    public async Task<IActionResult> CreatePrediction(CreatePredictionRequest request)
    {
        var spectatorId = GetUserId();
        var now = _dateTimeProvider.UtcNow;

        if (request.StakePoints < SpectatorBettingRules.MinimumStakePoints)
        {
            return BadRequest(new
            {
                error = "Invalid stake points.",
                message = $"Stake must be at least {SpectatorBettingRules.MinimumStakePoints} points.",
                minimumStakePoints = SpectatorBettingRules.MinimumStakePoints
            });
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();

        var spectator = await _context.Users
            .FirstOrDefaultAsync(u => u.UserId == spectatorId && u.Role == UserRoles.Spectator);

        if (spectator == null)
        {
            return Unauthorized(new { message = "Spectator account not found." });
        }

        if (spectator.BettingPoints < request.StakePoints)
        {
            return BadRequest(new
            {
                error = "Insufficient betting points.",
                message = "You do not have enough points to place this bet.",
                bettingPoints = spectator.BettingPoints,
                requestedStakePoints = request.StakePoints
            });
        }

        var tournament = await _context.Tournaments
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TournamentId == request.TournamentId);

        if (tournament == null)
        {
            return NotFound("Tournament not found.");
        }

        if (tournament.Status is TournamentStatuses.Cancelled or TournamentStatuses.Completed)
        {
            return BadRequest("Prediction is not allowed for this tournament status.");
        }

        var race = await _context.Races
            .AsNoTracking()
            .Where(r =>
                r.TournamentId == request.TournamentId &&
                r.Status != RaceStatuses.Cancelled)
            .OrderBy(r => r.RaceDate)
            .FirstOrDefaultAsync();

        if (race == null)
        {
            return NotFound("Race not found.");
        }

        if (RaceStatuses.IsClosedForPrediction(race.Status))
        {
            return BadRequest("Prediction is not allowed for this race status.");
        }

        if (race.PredictionDeadline.HasValue && now > race.PredictionDeadline.Value)
        {
            return BadRequest("Prediction deadline has passed.");
        }

        var existing = await _context.RacePredictions
            .AsNoTracking()
            .AnyAsync(p =>
                p.SpectatorId == spectatorId &&
                p.Status != RacePredictionStatuses.Cancelled &&
                p.Race.TournamentId == request.TournamentId);

        if (existing)
        {
            return Conflict(new
            {
                error = "You have already predicted for this tournament.",
                message = "You have already predicted for this tournament."
            });
        }

        var registration = await _context.RaceRegistrations
            .AsNoTracking()
            .FirstOrDefaultAsync(r =>
                r.RaceId == race.RaceId &&
                r.HorseId == request.PredictedHorseId &&
                PredictableRegistrationStatuses.Contains(r.Status));

        if (registration == null)
        {
            return BadRequest(new
            {
                error = "Horse is not registered in this tournament.",
                message = "Horse is not registered in this tournament."
            });
        }

        spectator.BettingPoints -= request.StakePoints;
        spectator.UpdatedAt = now;

        var prediction = new RacePrediction
        {
            RaceId = race.RaceId,
            SpectatorId = spectatorId,
            PredictedRegistrationId = registration.RegistrationId,
            Status = RacePredictionStatuses.Pending,
            IsCorrect = null,
            PointsAwarded = 0,
            StakePoints = request.StakePoints,
            RewardStatus = PredictionRewardStatuses.None,
            PredictedAt = now,
            CreatedAt = now
        };

        _context.RacePredictions.Add(prediction);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return Ok(new
        {
            message = "Prediction submitted successfully. Stake points have been deducted from your wallet.",
            predictionId = prediction.PredictionId,
            status = prediction.Status,
            stakePoints = prediction.StakePoints,
            payoutPoints = prediction.PointsAwarded,
            bettingPoints = spectator.BettingPoints
        });
    }

    [HttpGet("my")]
    public async Task<IActionResult> GetMyPredictions()
    {
        var spectatorId = GetUserId();

        var predictions = await _context.RacePredictions
            .AsNoTracking()
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
                stakePoints = p.StakePoints,
                payoutPoints = p.PointsAwarded,
                pointsAwarded = p.PointsAwarded,
                netPoints = p.Status == RacePredictionStatuses.Evaluated
                    ? p.PointsAwarded - p.StakePoints
                    : -p.StakePoints,
                rewardAmount = p.RewardAmount,
                rewardStatus = p.RewardStatus,
                predictedAt = p.PredictedAt,
                lockedAt = p.LockedAt,
                evaluatedAt = p.EvaluatedAt
            })
            .ToListAsync();

        return Ok(predictions);
    }
}
