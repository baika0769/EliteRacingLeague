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

            var disqualifiedViolation = await _context.RaceViolations
                .AsNoTracking()
                .Where(v =>
                    v.RaceId == result.RaceId &&
                    v.RegistrationId == result.RegistrationId &&
                    v.Action == RaceViolationActions.Disqualified)
                .Select(v => new
                {
                    v.ViolationId,
                    v.ViolationType
                })
                .FirstOrDefaultAsync();

            if (disqualifiedViolation != null)
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "This registration has a disqualification violation. Resolve the violation/correction flow before approving this result.",
                    Id = result.ResultId,
                    Status = result.Status,
                    Note = $"Violation #{disqualifiedViolation.ViolationId}: {disqualifiedViolation.ViolationType}"
                });
            }

            var now = DateTime.UtcNow;
            var tournamentCompleted = false;

            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var registration = await _context.RaceRegistrations
                    .FirstOrDefaultAsync(r => r.RegistrationId == result.RegistrationId);

                result.Status = RaceResultStatuses.AdminApproved;
                result.AdminConfirmedBy = adminId;
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

                tournamentCompleted = await CompleteTournamentIfAllResultsApprovedAsync(
                    result.RaceId,
                    result.ResultId,
                    now
                );

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            if (tournamentCompleted)
            {
                await _predictionEvaluationService.EvaluateRacePredictionsAsync(result.RaceId);
            }

            return Ok(new AdminActionResponse
            {
                Message = tournamentCompleted
                    ? "Race result approved successfully. Race published and tournament completed."
                    : "Race result approved successfully",
                Id = result.ResultId,
                Status = result.Status
            });
        }

        private async Task<bool> CompleteTournamentIfAllResultsApprovedAsync(
            int raceId,
            int approvedResultId,
            DateTime now)
        {
            var race = await _context.Races
                .Include(r => r.Tournament)
                .FirstOrDefaultAsync(r => r.RaceId == raceId);

            if (race == null || race.Tournament == null)
            {
                return false;
            }

            if (race.Status == RaceStatuses.Cancelled ||
                race.Tournament.Status == TournamentStatuses.Cancelled)
            {
                return false;
            }

            var eligibleRegistrationIds = await _context.RaceRegistrations
                .Where(r =>
                    r.RaceId == raceId &&
                    r.Status != RaceRegistrationStatuses.Cancelled &&
                    r.Status != RaceRegistrationStatuses.Rejected)
                .Select(r => r.RegistrationId)
                .ToListAsync();

            if (eligibleRegistrationIds.Count == 0)
            {
                return false;
            }

            var results = await _context.RaceResults
                .Where(r =>
                    r.RaceId == raceId &&
                    eligibleRegistrationIds.Contains(r.RegistrationId))
                .ToListAsync();

            var hasUnapprovedResult = results.Any(r =>
                r.ResultId != approvedResultId &&
                r.Status != RaceResultStatuses.AdminApproved &&
                r.Status != RaceResultStatuses.Published);

            if (hasUnapprovedResult || results.Count < eligibleRegistrationIds.Count)
            {
                return false;
            }

            foreach (var raceResult in results.Where(r => r.Status == RaceResultStatuses.AdminApproved))
            {
                raceResult.Status = RaceResultStatuses.Published;
                raceResult.PublishedAt = now;
                raceResult.UpdatedAt = now;
            }

            race.Status = RaceStatuses.Published;
            race.UpdatedAt = now;

            race.Tournament.Status = TournamentStatuses.Completed;
            race.Tournament.UpdatedAt = now;

            return true;
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

            if (result.Status is not RaceResultStatuses.Draft and not RaceResultStatuses.Returned)
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "Only draft or returned results can be deleted. Approved or published results require a correction flow.",
                    Id = id,
                    Status = result.Status
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

            if (result.Status is RaceResultStatuses.AdminApproved or RaceResultStatuses.Published)
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "Approved or published results cannot be returned. Use a correction flow instead.",
                    Id = id,
                    Status = result.Status
                });
            }

            var now = DateTime.UtcNow;
            result.Status = RaceResultStatuses.Returned;
            result.Note = "Returned by admin";
            result.UpdatedAt = now;

            var race = await _context.Races
                .FirstOrDefaultAsync(r => r.RaceId == result.RaceId);

            if (race != null && race.Status == RaceStatuses.ResultPending)
            {
                race.Status = RaceStatuses.Finished;
                race.UpdatedAt = now;
            }

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