using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Eliteracingleague.API.Data;

namespace Eliteracingleague.API.Controllers
{
    [ApiController]
    [Route("api/admin/horses")]
    public class AdminHorsesController : ControllerBase
    {
        private readonly EliteRacingLeagueContext _context;

        public AdminHorsesController(EliteRacingLeagueContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetHorses()
        {
            var horses = await _context.Horses
                .Select(h => new
                {
                    h.HorseId,
                    h.HorseName,
                    h.Age,
                    h.HeightCm,
                    h.WeightKg,
                    h.HealthStatus,
                    h.IsActive,
                    h.OwnerId,
                    h.BreedId,
                    h.CreatedAt
                })
                .ToListAsync();

            return Ok(horses);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetHorseById(int id)
        {
            var horse = await _context.Horses
                .Where(h => h.HorseId == id)
                .Select(h => new
                {
                    h.HorseId,
                    h.HorseName,
                    h.Age,
                    h.HeightCm,
                    h.WeightKg,
                    h.HealthStatus,
                    h.AchievementSummary,
                    h.IsActive,
                    h.OwnerId,
                    h.BreedId,
                    h.CreatedAt
                })
                .FirstOrDefaultAsync();

            if (horse == null)
                return NotFound(new { message = "Horse not found" });

            return Ok(horse);
        }

        [HttpPut("{id}/approve")]
        public async Task<IActionResult> ApproveHorse(int id)
        {
            var horse = await _context.Horses
                .FirstOrDefaultAsync(h => h.HorseId == id);

            if (horse == null)
                return NotFound(new { message = "Horse not found" });

            horse.IsActive = true;
            horse.HealthStatus = "Healthy";

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Horse approved successfully",
                horse.HorseId,
                horse.HorseName,
                horse.HealthStatus,
                horse.IsActive
            });
        }

        [HttpPut("{id}/suspend")]
        public async Task<IActionResult> SuspendHorse(int id)
        {
            var horse = await _context.Horses
                .FirstOrDefaultAsync(h => h.HorseId == id);

            if (horse == null)
                return NotFound(new { message = "Horse not found" });

            horse.IsActive = false;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Horse suspended successfully",
                horse.HorseId,
                horse.HorseName,
                horse.HealthStatus,
                horse.IsActive
            });
        }
    }
}