using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Admin;

namespace Eliteracingleague.API.Controllers.Admin
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
                .Select(h => new AdminHorseResponse
                {
                    HorseId = h.HorseId,
                    HorseName = h.HorseName,
                    Age = h.Age,
                    HeightCm = h.HeightCm,
                    WeightKg = h.WeightKg,
                    HealthStatus = h.HealthStatus,
                    IsActive = h.IsActive,
                    OwnerId = h.OwnerId,
                    BreedId = h.BreedId,
                    AchievementSummary = h.AchievementSummary,
                    CreatedAt = h.CreatedAt
                })
                .ToListAsync();

            return Ok(horses);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetHorseById(int id)
        {
            var horse = await _context.Horses
                .Where(h => h.HorseId == id)
                .Select(h => new AdminHorseResponse
                {
                    HorseId = h.HorseId,
                    HorseName = h.HorseName,
                    Age = h.Age,
                    HeightCm = h.HeightCm,
                    WeightKg = h.WeightKg,
                    HealthStatus = h.HealthStatus,
                    IsActive = h.IsActive,
                    OwnerId = h.OwnerId,
                    BreedId = h.BreedId,
                    AchievementSummary = h.AchievementSummary,
                    CreatedAt = h.CreatedAt
                })
                .FirstOrDefaultAsync();

            if (horse == null)
            {
                return NotFound(new AdminActionResponse
                {
                    Message = "Horse not found",
                    Id = id
                });
            }

            return Ok(horse);
        }

        [HttpPut("{id}/approve")]
        public async Task<IActionResult> ApproveHorse(int id)
        {
            var horse = await _context.Horses
                .FirstOrDefaultAsync(h => h.HorseId == id);

            if (horse == null)
            {
                return NotFound(new AdminActionResponse
                {
                    Message = "Horse not found",
                    Id = id
                });
            }

            horse.IsActive = true;
            horse.HealthStatus = "Healthy";

            await _context.SaveChangesAsync();

            return Ok(new AdminActionResponse
            {
                Message = "Horse approved successfully",
                Id = horse.HorseId,
                Name = horse.HorseName,
                Status = horse.HealthStatus
            });
        }

        [HttpPut("{id}/suspend")]
        public async Task<IActionResult> SuspendHorse(int id)
        {
            var horse = await _context.Horses
                .FirstOrDefaultAsync(h => h.HorseId == id);

            if (horse == null)
            {
                return NotFound(new AdminActionResponse
                {
                    Message = "Horse not found",
                    Id = id
                });
            }

            horse.IsActive = false;

            await _context.SaveChangesAsync();

            return Ok(new AdminActionResponse
            {
                Message = "Horse suspended successfully",
                Id = horse.HorseId,
                Name = horse.HorseName,
                Status = horse.HealthStatus
            });
        }
    }
}