using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Admin;
namespace Eliteracingleague.API.Controllers.Admin
{
    [ApiController]
    [Route("api/admin/tournaments")]
    public class AdminTournamentsController : ControllerBase
    {
        private readonly EliteRacingLeagueContext _context;

        public AdminTournamentsController(EliteRacingLeagueContext context)
        {
            _context = context;
        }

        // Lấy danh sách tất cả giải đấu
        [HttpGet]
        public async Task<IActionResult> GetTournaments()
        {
            var tournaments = await _context.Tournaments
                .Select(t => new
                {
                    t.TournamentId,
                    t.TournamentName,
                    t.Description,
                    t.Location,
                    t.StartDate,
                    t.EndDate,
                    t.MaxHorses,
                    t.PrizePool,
                    t.Status,
                    t.CreatedAt
                })
                .ToListAsync();

            return Ok(tournaments);
        }

        // Lấy chi tiết 1 giải đấu theo ID
        [HttpGet("{id}")]
        public async Task<IActionResult> GetTournamentById(int id)
        {
            var tournament = await _context.Tournaments
                .Where(t => t.TournamentId == id)
                .Select(t => new
                {
                    t.TournamentId,
                    t.TournamentName,
                    t.Description,
                    t.Location,
                    t.StartDate,
                    t.EndDate,
                    t.MaxHorses,
                    t.MinHorseAge,
                    t.MaxHorseAge,
                    t.MinHorseWeightKg,
                    t.MaxHorseWeightKg,
                    t.PrizePool,
                    t.Rules,
                    t.Status,
                    t.CreatedBy,
                    t.CreatedAt
                })
                .FirstOrDefaultAsync();

            if (tournament == null)
                return NotFound(new { message = "Tournament not found" });

            return Ok(tournament);
        }

        // Duyệt giải đấu
        [HttpPut("{id}/approve")]
        public async Task<IActionResult> ApproveTournament(int id)
        {
            var tournament = await _context.Tournaments
                .FirstOrDefaultAsync(t => t.TournamentId == id);

            if (tournament == null)
                return NotFound(new { message = "Tournament not found" });

            tournament.Status = "OpenRegistration";
            tournament.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Tournament approved successfully",
                tournament.TournamentId,
                tournament.TournamentName,
                tournament.Status
            });
        }

        // Hủy giải đấu
        [HttpPut("{id}/cancel")]
        public async Task<IActionResult> CancelTournament(int id)
        {
            var tournament = await _context.Tournaments
                .FirstOrDefaultAsync(t => t.TournamentId == id);

            if (tournament == null)
                return NotFound(new { message = "Tournament not found" });

            tournament.Status = "Cancelled";
            tournament.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Tournament cancelled successfully",
                tournament.TournamentId,
                tournament.TournamentName,
                tournament.Status
            });
        }
    }
}