using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Admin;
using Microsoft.AspNetCore.Authorization;
using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Models;
using System.Security.Claims;

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

        [HttpPost]
        public async Task<IActionResult> CreateTournament([FromBody] AdminTournamentRequest request)
        {
            if (request.StartDate >= request.EndDate)
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "Start date must be before end date"
                });
            }

            if (request.MaxHorses <= 0)
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "Max horses must be greater than 0"
                });
            }

            var userIdText = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(userIdText, out var adminId))
            {
                return Unauthorized(new AdminActionResponse
                {
                    Message = "Invalid admin token"
                });
            }

            var tournament = new Tournament
            {
                TournamentName = request.TournamentName,
                Description = request.Description,
                Location = request.Location,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                MaxHorses = request.MaxHorses,
                PrizePool = request.PrizePool,
                Status = TournamentStatuses.Draft,
                MinHorseAge = request.MinHorseAge,
                MaxHorseAge = request.MaxHorseAge,
                MinHorseWeightKg = request.MinHorseWeightKg,
                MaxHorseWeightKg = request.MaxHorseWeightKg,
                Rules = request.Rules,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = adminId,
                UpdatedAt = DateTime.UtcNow,
            };

            _context.Tournaments.Add(tournament);
            await _context.SaveChangesAsync();

            return Ok(new AdminActionResponse
            {
                Message = "Tournament created successfully",
                Id = tournament.TournamentId,
                Name = tournament.TournamentName,
                Status = tournament.Status
            });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateTournament(int id, [FromBody] AdminTournamentRequest request)
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

            if (request.StartDate >= request.EndDate)
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "Start date must be before end date",
                    Id = id
                });
            }

            if (request.MaxHorses < 1 || request.MaxHorses > 20)
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "Max horses must be between 1 and 20"
                });
            }
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "Max horses must be greater than 0",
                    Id = id
                });
            }

            tournament.TournamentName = request.TournamentName;
            tournament.Description = request.Description;
            tournament.Location = request.Location;
            tournament.StartDate = request.StartDate;
            tournament.EndDate = request.EndDate;
            tournament.MaxHorses = request.MaxHorses;
            tournament.PrizePool = request.PrizePool;
            tournament.MinHorseAge = request.MinHorseAge;
            tournament.MaxHorseAge = request.MaxHorseAge;
            tournament.MinHorseWeightKg = request.MinHorseWeightKg;
            tournament.MaxHorseWeightKg = request.MaxHorseWeightKg;
            tournament.Rules = request.Rules;
            tournament.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new AdminActionResponse
            {
                Message = "Tournament updated successfully",
                Id = tournament.TournamentId,
                Name = tournament.TournamentName,
                Status = tournament.Status
            });
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