using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Admin;
namespace Eliteracingleague.API.Controllers.Admin
{
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
                .Select(r => new
                {
                    r.ResultId,
                    r.RaceId,
                    r.RegistrationId,
                    r.FinishTimeSeconds,
                    r.FinishPosition,
                    r.Score,
                    r.Status,
                    r.EnteredByRefereeId,
                    r.AdminConfirmedBy,
                    r.PublishedAt,
                    r.Note,
                    r.CreatedAt
                })
                .ToListAsync();

            return Ok(results);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetResultById(int id)
        {
            var result = await _context.RaceResults
                .Where(r => r.ResultId == id)
                .Select(r => new
                {
                    r.ResultId,
                    r.RaceId,
                    r.RegistrationId,
                    r.FinishTimeSeconds,
                    r.FinishPosition,
                    r.Score,
                    r.Status,
                    r.EnteredByRefereeId,
                    r.AdminConfirmedBy,
                    r.PublishedAt,
                    r.Note,
                    r.CreatedAt,
                    r.UpdatedAt
                })
                .FirstOrDefaultAsync();

            if (result == null)
                return NotFound(new { message = "Race result not found" });

            return Ok(result);
        }

        [HttpGet("pending")]
        public async Task<IActionResult> GetPendingResults()
        {
            var results = await _context.RaceResults
                .Where(r => r.Status == "Pending")
                .Select(r => new
                {
                    r.ResultId,
                    r.RaceId,
                    r.RegistrationId,
                    r.FinishTimeSeconds,
                    r.FinishPosition,
                    r.Score,
                    r.Status,
                    r.EnteredByRefereeId,
                    r.CreatedAt
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
                return NotFound(new { message = "Race result not found" });

            result.Status = "Confirmed";
            result.PublishedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Race result approved successfully",
                result.ResultId,
                result.Status,
                result.PublishedAt
            });
        }

        [HttpPut("{id}/reject")]
        public async Task<IActionResult> RejectResult(int id)
        {
            var result = await _context.RaceResults
                .FirstOrDefaultAsync(r => r.ResultId == id);

            if (result == null)
                return NotFound(new { message = "Race result not found" });

            result.Status = "Rejected";
            result.Note = "Rejected by admin";

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Race result rejected successfully",
                result.ResultId,
                result.Status,
                result.Note
            });
        }
    }
}