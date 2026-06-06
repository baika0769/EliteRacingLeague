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
    [Route("api/admin/tournaments")]
    public class AdminTournamentsController : ControllerBase
    {
        private readonly EliteRacingLeagueContext _context;

        public AdminTournamentsController(EliteRacingLeagueContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetTournaments()
        {
            var tournaments = await _context.Tournaments
                .Select(t => new AdminTournamentResponse
                {
                    TournamentId = t.TournamentId,
                    TournamentName = t.TournamentName,
                    Description = t.Description,
                    Location = t.Location,
                    StartDate = t.StartDate,
                    EndDate = t.EndDate,
                    MaxHorses = t.MaxHorses,
                    PrizePool = t.PrizePool,
                    Status = t.Status,
                    MinHorseAge = t.MinHorseAge,
                    MaxHorseAge = t.MaxHorseAge,
                    MinHorseWeightKg = t.MinHorseWeightKg,
                    MaxHorseWeightKg = t.MaxHorseWeightKg,
                    Rules = t.Rules,
                    CreatedBy = t.CreatedBy,
                    CreatedAt = t.CreatedAt,

                    EntriesCount = _context.RaceRegistrations
                        .Count(r => r.Race.TournamentId == t.TournamentId),

                    EntriesText =
                        _context.RaceRegistrations
                            .Count(r => r.Race.TournamentId == t.TournamentId)
                        + "/" + t.MaxHorses
                })
                .ToListAsync();

            return Ok(tournaments);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetTournamentById(int id)
        {
            var tournament = await _context.Tournaments
                .Where(t => t.TournamentId == id)
                .Select(t => new AdminTournamentResponse
                {
                    TournamentId = t.TournamentId,
                    TournamentName = t.TournamentName,
                    Description = t.Description,
                    Location = t.Location,
                    StartDate = t.StartDate,
                    EndDate = t.EndDate,
                    MaxHorses = t.MaxHorses,
                    PrizePool = t.PrizePool,
                    Status = t.Status,
                    MinHorseAge = t.MinHorseAge,
                    MaxHorseAge = t.MaxHorseAge,
                    MinHorseWeightKg = t.MinHorseWeightKg,
                    MaxHorseWeightKg = t.MaxHorseWeightKg,
                    Rules = t.Rules,
                    CreatedBy = t.CreatedBy,
                    CreatedAt = t.CreatedAt,

                    EntriesCount = _context.RaceRegistrations
                        .Count(r => r.Race.TournamentId == t.TournamentId),

                    EntriesText =
                        _context.RaceRegistrations
                            .Count(r => r.Race.TournamentId == t.TournamentId)
                        + "/" + t.MaxHorses
                })
                .FirstOrDefaultAsync();

            if (tournament == null)
            {
                return NotFound(new AdminActionResponse
                {
                    Message = "Tournament not found",
                    Id = id
                });
            }

            return Ok(tournament);
        }

        [HttpPut("{id}/approve")]
        public async Task<IActionResult> ApproveTournament(int id)
        {
            var tournament = await _context.Tournaments
                .FirstOrDefaultAsync(t => t.TournamentId == id);

            if (tournament == null)
            {
                return NotFound(new AdminActionResponse
                {
                    Message = "Tournament not found",
                    Id = id
                });
            }

            tournament.Status = TournamentStatuses.OpenRegistration;
            tournament.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new AdminActionResponse
            {
                Message = "Tournament approved successfully",
                Id = tournament.TournamentId,
                Name = tournament.TournamentName,
                Status = tournament.Status
            });
        }

        [HttpPut("{id}/cancel")]
        public async Task<IActionResult> CancelTournament(int id)
        {
            var tournament = await _context.Tournaments
                .FirstOrDefaultAsync(t => t.TournamentId == id);

            if (tournament == null)
            {
                return NotFound(new AdminActionResponse
                {
                    Message = "Tournament not found",
                    Id = id
                });
            }

            tournament.Status = TournamentStatuses.Cancelled;
            tournament.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new AdminActionResponse
            {
                Message = "Tournament cancelled successfully",
                Id = tournament.TournamentId,
                Name = tournament.TournamentName,
                Status = tournament.Status
            });
        }
    }
}