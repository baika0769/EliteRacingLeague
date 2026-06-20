using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Admin;
using Microsoft.AspNetCore.Authorization;
using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Models;
namespace Eliteracingleague.API.Controllers.Admin
{
    [Authorize(Roles = UserRoles.Admin)]
    [ApiController]
    [Route("api/admin/results")]
    public class AdminRaceResultsController : ControllerBase
    {
        private readonly EliteRacingLeagueContext _context;


    public AdminRaceResultsController(EliteRacingLeagueContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetResults()
        {
            var results = await _context.RaceResults
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
                .Where(r => r.Status == RaceResultStatuses.Draft)
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
                    CreatedAt = r.CreatedAt
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

            result.Status = RaceResultStatuses.AdminApproved;
            result.PublishedAt = DateTime.UtcNow;

            if (result.FinishPosition.HasValue)
            {
                var registration = await _context.RaceRegistrations
                    .FirstOrDefaultAsync(r => r.RegistrationId == result.RegistrationId);

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
                            CreatedAt = DateTime.UtcNow
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

            await _context.SaveChangesAsync();

            return Ok(new AdminActionResponse
            {
                Message = "Race result approved successfully",
                Id = result.ResultId,
                Status = result.Status
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
