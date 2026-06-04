using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Admin;
namespace Eliteracingleague.API.Controllers.Admin
{
    [ApiController]
    [Route("api/admin/dashboard")]
    public class AdminDashboardController : ControllerBase
    {
        private readonly EliteRacingLeagueContext _context;

        public AdminDashboardController(EliteRacingLeagueContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetDashboard()
        {
            var totalUsers = await _context.Users.CountAsync();
            var totalHorses = await _context.Horses.CountAsync();
            var totalRaces = await _context.Races.CountAsync();
            var totalTournaments = await _context.Tournaments.CountAsync();

            var pendingRegistrations = await _context.RaceRegistrations
                .CountAsync(r => r.Status == "Pending");

            var pendingResults = await _context.RaceResults
                .CountAsync(r => r.Status == "Pending");

            return Ok(new
            {
                totalUsers,
                totalHorses,
                totalRaces,
                totalTournaments,
                pendingRegistrations,
                pendingResults
            });
        }
    }
}