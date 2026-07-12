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

        [HttpGet("{id:int}")]
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

        [HttpPut("{id:int}/approve")]
        public async Task<IActionResult> ApproveResult(int id)
        {
            var result = await _context.RaceResults
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.ResultId == id);

            if (result == null)
            {
                return NotFound(new AdminActionResponse
                {
                    Message = "Race result not found",
                    Id = id
                });
            }

            return BadRequest(new AdminActionResponse
            {
                Message = "Individual result approval is disabled. Approve all results of the race in one transaction.",
                Id = result.ResultId,
                Status = result.Status,
                Note = $"Use PUT /api/admin/results/race/{result.RaceId}/approve-all"
            });
        }

        [HttpPut("race/{raceId:int}/approve-all")]
        public async Task<IActionResult> ApproveAllResults(int raceId)
        {
            if (!User.TryGetUserId(out var adminId))
            {
                return Unauthorized(new AdminActionResponse
                {
                    Message = "Invalid admin token",
                    Id = raceId
                });
            }

            var race = await _context.Races
                .Include(r => r.Tournament)
                .FirstOrDefaultAsync(r => r.RaceId == raceId);

            if (race == null)
            {
                return NotFound(new AdminActionResponse
                {
                    Message = "Race not found",
                    Id = raceId
                });
            }

            if (race.Tournament == null)
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "Tournament information was not found",
                    Id = raceId,
                    Status = race.Status
                });
            }

            if (race.Status == RaceStatuses.Published)
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "This race has already been published",
                    Id = raceId,
                    Status = race.Status
                });
            }

            if (race.Status != RaceStatuses.ResultPending)
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "Only races waiting for result approval can be approved",
                    Id = raceId,
                    Status = race.Status
                });
            }

            if (race.Tournament.Status == TournamentStatuses.Cancelled)
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "A cancelled tournament cannot be completed",
                    Id = raceId,
                    Status = race.Tournament.Status
                });
            }

            var registrations = await _context.RaceRegistrations
                .Where(r =>
                    r.RaceId == raceId &&
                    r.Status != RaceRegistrationStatuses.Cancelled &&
                    r.Status != RaceRegistrationStatuses.Rejected)
                .ToListAsync();

            if (registrations.Count == 0)
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "No eligible registrations were found for this race",
                    Id = raceId,
                    Status = race.Status
                });
            }

            var registrationIds = registrations
                .Select(r => r.RegistrationId)
                .ToList();

            var results = await _context.RaceResults
                .Where(r =>
                    r.RaceId == raceId &&
                    registrationIds.Contains(r.RegistrationId))
                .ToListAsync();

            var duplicatedRegistrationResult = results
                .GroupBy(r => r.RegistrationId)
                .FirstOrDefault(group => group.Count() > 1);

            if (duplicatedRegistrationResult != null)
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "A registration has more than one race result",
                    Id = duplicatedRegistrationResult.Key,
                    Status = race.Status
                });
            }

            var resultRegistrationIds = results
                .Select(r => r.RegistrationId)
                .ToHashSet();

            var missingRegistration = registrations
                .FirstOrDefault(r => !resultRegistrationIds.Contains(r.RegistrationId));

            if (missingRegistration != null)
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "Every eligible registration must have a race result before approval",
                    Id = missingRegistration.RegistrationId,
                    Status = race.Status,
                    Note = $"Registration #{missingRegistration.RegistrationId} has no result"
                });
            }

            var resultWithoutPosition = results
                .FirstOrDefault(r =>
                    !r.FinishPosition.HasValue ||
                    r.FinishPosition.Value <= 0);

            if (resultWithoutPosition != null)
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "Every race result must have a valid finish position before approval",
                    Id = resultWithoutPosition.ResultId,
                    Status = resultWithoutPosition.Status
                });
            }

            var duplicatedPosition = results
                .GroupBy(r => r.FinishPosition!.Value)
                .FirstOrDefault(group => group.Count() > 1);

            if (duplicatedPosition != null)
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "Two or more race results have the same finish position",
                    Id = raceId,
                    Status = race.Status,
                    Note = $"Duplicated finish position: {duplicatedPosition.Key}"
                });
            }

            var invalidResult = results.FirstOrDefault(r =>
                r.Status != RaceResultStatuses.RefereeConfirmed &&
                r.Status != RaceResultStatuses.AdminApproved);

            if (invalidResult != null)
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "All results must be referee-confirmed before bulk approval",
                    Id = invalidResult.ResultId,
                    Status = invalidResult.Status
                });
            }

            var disqualifiedRegistrationIds = await _context.RaceViolations
                .AsNoTracking()
                .Where(v =>
                    v.RaceId == raceId &&
                    v.Action == RaceViolationActions.Disqualified &&
                    registrationIds.Contains(v.RegistrationId))
                .Select(v => v.RegistrationId)
                .Distinct()
                .ToListAsync();

            var disqualifiedIds = disqualifiedRegistrationIds.ToHashSet();

            if (disqualifiedIds.Count == registrations.Count)
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "All race participants are disqualified. At least one valid result is required.",
                    Id = raceId,
                    Status = race.Status
                });
            }

            var prizeRules = await _context.PrizeRules
                .Where(r => r.RaceId == raceId)
                .ToListAsync();

            var prizeRuleByRank = prizeRules
                .GroupBy(r => r.RankPosition)
                .ToDictionary(group => group.Key, group => group.First());

            var existingAwards = await _context.PrizeAwards
                .Where(a => a.RaceId == raceId)
                .ToListAsync();

            var lockedAward = existingAwards.FirstOrDefault(a =>
                a.Status == PrizeAwardStatuses.UnderReview ||
                a.Status == PrizeAwardStatuses.Paid);

            if (lockedAward != null)
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "This race has a prize that is under review or already paid. Resolve it before recalculating results.",
                    Id = lockedAward.PrizeAwardId,
                    Status = lockedAward.Status
                });
            }

            var rankingSeed = results
                .Select(result => new
                {
                    Result = result,
                    OriginalPosition = result.FinishPosition!.Value,
                    FinishTime = result.FinishTimeSeconds ?? decimal.MaxValue
                })
                .OrderBy(item => item.OriginalPosition)
                .ThenBy(item => item.FinishTime)
                .ThenBy(item => item.Result.ResultId)
                .ToList();

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                foreach (var result in results)
                {
                    result.FinishPosition = null;
                }

                if (existingAwards.Count > 0)
                {
                    _context.PrizeAwards.RemoveRange(existingAwards);
                }

                await _context.SaveChangesAsync();

                var registrationById = registrations
                    .ToDictionary(r => r.RegistrationId);

                var now = DateTime.UtcNow;
                var officialRank = 0;
                var promotedCount = 0;

                foreach (var item in rankingSeed)
                {
                    var result = item.Result;
                    var registration = registrationById[result.RegistrationId];
                    var isDisqualified = disqualifiedIds.Contains(result.RegistrationId);

                    result.AdminConfirmedBy = adminId;
                    result.Status = RaceResultStatuses.Published;
                    result.PublishedAt = now;
                    result.UpdatedAt = now;

                    registration.Status = RaceRegistrationStatuses.Completed;

                    if (isDisqualified)
                    {
                        result.FinishPosition = null;
                        continue;
                    }

                    officialRank++;
                    result.FinishPosition = officialRank;

                    if (officialRank != item.OriginalPosition)
                    {
                        promotedCount++;
                    }

                    if (!prizeRuleByRank.TryGetValue(
                            officialRank,
                            out var prizeRule))
                    {
                        continue;
                    }

                    var prizeAward = new PrizeAward
                    {
                        RaceId = raceId,
                        RegistrationId = result.RegistrationId,
                        OwnerId = registration.OwnerId,
                        JockeyId = registration.JockeyId,
                        RankPosition = officialRank,
                        PrizeAmount = prizeRule.PrizeAmount,
                        Status = PrizeAwardStatuses.ReadyToClaim,
                        CreatedAt = now
                    };

                    _context.PrizeAwards.Add(prizeAward);
                }

                race.Status = RaceStatuses.Published;
                race.UpdatedAt = now;
                race.Tournament.Status = TournamentStatuses.Completed;
                race.Tournament.UpdatedAt = now;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var predictionEvaluationSucceeded = true;

                try
                {
                    await _predictionEvaluationService
                        .EvaluateRacePredictionsAsync(raceId);
                }
                catch
                {
                    predictionEvaluationSucceeded = false;
                }

                return Ok(new AdminActionResponse
                {
                    Message = predictionEvaluationSucceeded
                        ? "All results were approved, official ranks were recalculated, race published and tournament completed."
                        : "Results and official ranks were published, but prediction evaluation failed.",
                    Id = raceId,
                    Status = race.Status,
                    Note =
                        $"Published: {results.Count}; " +
                        $"disqualified: {disqualifiedIds.Count}; " +
                        $"promoted: {promotedCount}"
                });
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
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

            if (result.Status is not RaceResultStatuses.Draft
                and not RaceResultStatuses.Returned)
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

        [HttpPut("{id:int}/reject")]
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

            if (result.Status is
                RaceResultStatuses.AdminApproved or
                RaceResultStatuses.Published)
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

            if (race != null &&
                race.Status == RaceStatuses.ResultPending)
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