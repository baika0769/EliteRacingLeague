using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Admin;
using Microsoft.AspNetCore.Authorization;
using Eliteracingleague.API.Constants;
namespace Eliteracingleague.API.Controllers.Admin
{
    [Authorize(Roles = UserRoles.Admin)]
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
            var totalTournaments = await _context.Tournaments
    .CountAsync(t =>
        t.Status == TournamentStatuses.Draft ||
        t.Status == TournamentStatuses.OpenRegistration ||
        t.Status == TournamentStatuses.Ongoing);
            var pendingRegistrations = await _context.RaceRegistrations
    .CountAsync(r => r.Status == RaceRegistrationStatuses.Pending);

            var pendingResults = await _context.RaceResults
    .CountAsync(r => r.Status == RaceResultStatuses.RefereeConfirmed);

            var response = new AdminDashboardResponse
            {
                TotalUsers = totalUsers,
                TotalHorses = totalHorses,
                TotalRaces = totalRaces,
                TotalTournaments = totalTournaments,
                PendingRegistrations = pendingRegistrations,
                PendingResults = pendingResults
            };

            return Ok(response);
        }
    }
}
