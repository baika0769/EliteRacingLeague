using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Admin;
using Microsoft.AspNetCore.Authorization;
using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Extensions;
using Eliteracingleague.API.Models;
using Eliteracingleague.API.Services;
namespace Eliteracingleague.API.Controllers.Admin
{
    [Authorize(Roles = UserRoles.Admin)]
    [ApiController]
    [Route("api/admin/results")]
    public class AdminRaceResultsController : ControllerBase
    {
        private readonly EliteRacingLeagueContext _context;
        private readonly PredictionEvaluationService _predictionEvaluationService;

        public AdminRaceResultsController(
            EliteRacingLeagueContext context,
            PredictionEvaluationService predictionEvaluationService)
        {
            _context = context;
            _predictionEvaluationService = predictionEvaluationService;
        }

        [HttpGet]
        public async Task<IActionResult> GetResults()
        {
            var results = await _context.RaceResults
                .AsNoTracking()
                .Select(r => new AdminRaceResultResponse
                {
                    ResultId = r.ResultId,
                    RaceId = r.RaceId,
                    RegistrationId = r.RegistrationId,
                    FinishTimeSeconds = r.FinishTimeSeconds,
                    FinishPosition = r.FinishPosition,
                    Score = r.Score,
                    Status = r.Status,
                    EnteredByRefereeId = r.EnteredByRefereeId,
                    AdminConfirmedBy = r.AdminConfirmedBy,
                    PublishedAt = r.PublishedAt,
                    Note = r.Note,
                    CreatedAt = r.CreatedAt,
                    UpdatedAt = r.UpdatedAt
                })
                .ToListAsync();

            return Ok(results);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetResultById(int id)
        {
            var result = await _context.RaceResults
                .AsNoTracking()
                .Where(r => r.ResultId == id)
                .Select(r => new AdminRaceResultResponse
                {
                    ResultId = r.ResultId,
                    RaceId = r.RaceId,
                    RegistrationId = r.RegistrationId,
                    FinishTimeSeconds = r.FinishTimeSeconds,
                    FinishPosition = r.FinishPosition,
                    Score = r.Score,
                    Status = r.Status,
                    EnteredByRefereeId = r.EnteredByRefereeId,
                    AdminConfirmedBy = r.AdminConfirmedBy,
                    PublishedAt = r.PublishedAt,
                    Note = r.Note,
                    CreatedAt = r.CreatedAt,
                    UpdatedAt = r.UpdatedAt
                })
                .FirstOrDefaultAsync();

            if (result == null)
            {
                return NotFound(new AdminActionResponse
                {
                    Message = "Race result not found",
                    Id = id
                });
            }

            return Ok(result);
        }

        [HttpGet("pending")]
        public async Task<IActionResult> GetPendingResults()
        {
            var results = await _context.RaceResults
                .AsNoTracking()
                .Where(r => r.Status == RaceResultStatuses.RefereeConfirmed)
                .Select(r => new AdminRaceResultResponse
                {
                    ResultId = r.ResultId,
                    RaceId = r.RaceId,
                    RegistrationId = r.RegistrationId,
                    FinishTimeSeconds = r.FinishTimeSeconds,
                    FinishPosition = r.FinishPosition,
                    Score = r.Score,
                    Status = r.Status,
                    EnteredByRefereeId = r.EnteredByRefereeId,
                    AdminConfirmedBy = r.AdminConfirmedBy,
                    PublishedAt = r.PublishedAt,
                    Note = r.Note,
                    CreatedAt = r.CreatedAt,
                    UpdatedAt = r.UpdatedAt
                })
                .ToListAsync();

            return Ok(results);
        }

        [HttpPut("{id}/approve")]
        public async Task<IActionResult> ApproveResult(int id)
        {
            var result = await _context.RaceResults
                .FirstOrDefaultAsync(r => r.ResultId == id);

            if (result == null)
            {
                return NotFound(new AdminActionResponse
                {
                    Message = "Race result not found",
                    Id = id
                });
            }

            if (result.Status != RaceResultStatuses.RefereeConfirmed)
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "Only referee-confirmed race results can be approved",
                    Id = id,
                    Status = result.Status
                });
            }

            if (!User.TryGetUserId(out var adminId))
            {
                return Unauthorized(new AdminActionResponse
                {
                    Message = "Invalid admin token",
                    Id = id
                });
            }

            var now = DateTime.UtcNow;

            var registration = await _context.RaceRegistrations
                .FirstOrDefaultAsync(r => r.RegistrationId == result.RegistrationId);

            result.Status = RaceResultStatuses.AdminApproved;
            result.AdminConfirmedBy = adminId;
            result.PublishedAt = now;
            result.UpdatedAt = now;

            if (registration != null)
            {
                registration.Status = RaceRegistrationStatuses.Completed;
            }

            if (result.FinishPosition.HasValue)
            {
                var prizeRule = await _context.PrizeRules
                    .FirstOrDefaultAsync(r =>
                        r.RaceId == result.RaceId &&
                        r.RankPosition == result.FinishPosition.Value);

                if (registration != null && prizeRule != null)
                {
                    var prizeAward = await _context.PrizeAwards
                        .FirstOrDefaultAsync(a =>
                            a.RaceId == result.RaceId &&
                            a.RankPosition == result.FinishPosition.Value);

                    prizeAward ??= await _context.PrizeAwards
                        .FirstOrDefaultAsync(a =>
                            a.RaceId == result.RaceId &&
                            a.RegistrationId == result.RegistrationId);

                    if (prizeAward == null)
                    {
                        prizeAward = new PrizeAward
                        {
                            RaceId = result.RaceId,
                            RegistrationId = result.RegistrationId,
                            OwnerId = registration.OwnerId,
                            JockeyId = registration.JockeyId,
                            RankPosition = result.FinishPosition.Value,
                            PrizeAmount = prizeRule.PrizeAmount,
                            Status = PrizeAwardStatuses.ReadyToClaim,
                            CreatedAt = now
                        };

                        _context.PrizeAwards.Add(prizeAward);
                    }
                    else
                    {
                        prizeAward.RegistrationId = result.RegistrationId;
                        prizeAward.OwnerId = registration.OwnerId;
                        prizeAward.JockeyId = registration.JockeyId;
                        prizeAward.RankPosition = result.FinishPosition.Value;
                        prizeAward.PrizeAmount = prizeRule.PrizeAmount;

                        if (prizeAward.Status != PrizeAwardStatuses.Paid)
                        {
                            prizeAward.Status = PrizeAwardStatuses.ReadyToClaim;
                        }
                    }
                }
            }

            var totalValidRegistrations = await _context.RaceRegistrations
                .CountAsync(r =>
                    r.RaceId == result.RaceId &&
                    r.Status != RaceRegistrationStatuses.Cancelled &&
                    r.Status != RaceRegistrationStatuses.Rejected);

            var approvedResultsAfterThisApprove = await _context.RaceResults
                .CountAsync(r =>
                    r.RaceId == result.RaceId &&
                    r.Registration.Status != RaceRegistrationStatuses.Cancelled &&
                    r.Registration.Status != RaceRegistrationStatuses.Rejected &&
                    (
                        r.Status == RaceResultStatuses.AdminApproved ||
                        r.ResultId == result.ResultId
                    ));

            if (totalValidRegistrations > 0 &&
                approvedResultsAfterThisApprove >= totalValidRegistrations)
            {
                var race = await _context.Races
                    .Include(r => r.Tournament)
                    .FirstOrDefaultAsync(r => r.RaceId == result.RaceId);

                if (race != null && race.Tournament.Status != TournamentStatuses.Cancelled)
                {
                    race.Status = RaceStatuses.Published;
                    race.UpdatedAt = now;

                    race.Tournament.Status = TournamentStatuses.Completed;
                    race.Tournament.UpdatedAt = now;
                }
            }

            await _context.SaveChangesAsync();

            await _predictionEvaluationService.EvaluateRacePredictionsAsync(result.RaceId);

            return Ok(new AdminActionResponse
            {
                Message = "Race result approved successfully",
                Id = result.ResultId,
                Status = result.Status
            });
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteResult(int id)
        {
            var result = await _context.RaceResults
                .FirstOrDefaultAsync(r => r.ResultId == id);

            if (result == null)
            {
                return NotFound(new AdminActionResponse
                {
                    Message = "Race result not found",
                    Id = id
                });
            }

            _context.RaceResults.Remove(result);
            await _context.SaveChangesAsync();

            return Ok(new AdminActionResponse
            {
                Message = "Race result deleted successfully",
                Id = id
            });
        }

        [HttpPut("{id}/reject")]
        public async Task<IActionResult> RejectResult(int id)
        {
            var result = await _context.RaceResults
                .FirstOrDefaultAsync(r => r.ResultId == id);

            if (result == null)
            {
                return NotFound(new AdminActionResponse
                {
                    Message = "Race result not found",
                    Id = id
                });
            }

            result.Status = RaceResultStatuses.Returned;
            result.Note = "Returned by admin";
            result.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new AdminActionResponse
            {
                Message = "Race result returned successfully",
                Id = result.ResultId,
                Status = result.Status
            });
        }
    }
}