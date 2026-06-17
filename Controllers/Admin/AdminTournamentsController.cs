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
                .OrderByDescending(t => t.TournamentId)
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
            var validationResult = ValidateTournamentRequest(request, 0 );
            if (validationResult != null)
            {
                return validationResult;
            }

            var userIdText = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(userIdText, out var adminId))
            {
                return Unauthorized(new AdminActionResponse
                {
                    Message = "Invalid admin token"
                });
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var now = DateTime.UtcNow;

                var tournament = new Tournament
                {
                    TournamentName = request.TournamentName.Trim(),
                    Description = request.Description,
                    Location = request.Location.Trim(),
                    StartDate = request.RegistrationDeadline,
                    EndDate = request.RaceDate,
                    MaxHorses = request.MaxHorses,
                    PrizePool = request.PrizePool,
                    Status = TournamentStatuses.Draft,
                    MinHorseAge = request.MinHorseAge,
                    MaxHorseAge = request.MaxHorseAge,
                    MinHorseWeightKg = request.MinHorseWeightKg,
                    MaxHorseWeightKg = request.MaxHorseWeightKg,
                    Rules = request.Rules,
                    CreatedAt = now,
                    CreatedBy = adminId,
                    UpdatedAt = now
                };

                _context.Tournaments.Add(tournament);
                await _context.SaveChangesAsync();

                var raceDateTime = request.RaceDate.ToDateTime(TimeOnly.MinValue);
                var registrationDeadline = request.RegistrationDeadline.ToDateTime(TimeOnly.MinValue);

                var race = new Race
                {
                    TournamentId = tournament.TournamentId,
                    RaceName = tournament.TournamentName,
                    RaceDate = raceDateTime,
                    DistanceMeters = request.DistanceMeters,
                    Location = tournament.Location,
                    MaxHorses = request.MaxHorses,
                    Status = RaceStatuses.Scheduled,
                    JockeySelectionDeadline = registrationDeadline,
                    PredictionDeadline = raceDateTime.AddHours(-1),
                    CreatedAt = now,
                    UpdatedAt = now
                };

                _context.Races.Add(race);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                return Ok(new
                {
                    message = "Tournament and race created successfully",
                    tournamentId = tournament.TournamentId,
                    raceId = race.RaceId,
                    name = tournament.TournamentName,
                    status = tournament.Status
                });
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateTournament(int id, [FromBody] AdminTournamentRequest request)
        {
            var validationResult = ValidateTournamentRequest(request, id);
            if (validationResult != null)
            {
                return validationResult;
            }

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

            if (!string.IsNullOrWhiteSpace(request.Status) &&
                !TournamentStatuses.IsValid(request.Status))
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "Invalid tournament status",
                    Id = id
                });
            }

            var now = DateTime.UtcNow;

            tournament.TournamentName = request.TournamentName.Trim();
            tournament.Description = request.Description;
            tournament.Location = request.Location.Trim();
            tournament.StartDate = request.RegistrationDeadline;
            tournament.EndDate = request.RaceDate;
            tournament.MaxHorses = request.MaxHorses;
            tournament.PrizePool = request.PrizePool;
            tournament.MinHorseAge = request.MinHorseAge;
            tournament.MaxHorseAge = request.MaxHorseAge;
            tournament.MinHorseWeightKg = request.MinHorseWeightKg;
            tournament.MaxHorseWeightKg = request.MaxHorseWeightKg;
            tournament.Rules = request.Rules;

            if (!string.IsNullOrWhiteSpace(request.Status))
            {
                tournament.Status = request.Status;
            }

            tournament.UpdatedAt = now;

            var race = await _context.Races
                .FirstOrDefaultAsync(r => r.TournamentId == tournament.TournamentId);

            var raceDateTime = request.RaceDate.ToDateTime(TimeOnly.MinValue);
            var registrationDeadline = request.RegistrationDeadline.ToDateTime(TimeOnly.MinValue);

            if (race == null)
            {
                race = new Race
                {
                    TournamentId = tournament.TournamentId,
                    RaceName = tournament.TournamentName,
                    RaceDate = raceDateTime,
                    DistanceMeters = request.DistanceMeters,
                    Location = tournament.Location,
                    MaxHorses = request.MaxHorses,
                    Status = RaceStatuses.Scheduled,
                    JockeySelectionDeadline = registrationDeadline,
                    PredictionDeadline = raceDateTime.AddHours(-1),
                    CreatedAt = now,
                    UpdatedAt = now
                };

                _context.Races.Add(race);
            }
            else
            {
                race.RaceName = tournament.TournamentName;
                race.RaceDate = raceDateTime;
                race.DistanceMeters = request.DistanceMeters;
                race.Location = tournament.Location;
                race.MaxHorses = request.MaxHorses;
                race.JockeySelectionDeadline = registrationDeadline;
                race.PredictionDeadline = raceDateTime.AddHours(-1);
                race.UpdatedAt = now;
            }

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

            if (tournament.Status == TournamentStatuses.Cancelled)
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "Cancelled tournament cannot be approved",
                    Id = id,
                    Name = tournament.TournamentName,
                    Status = tournament.Status
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

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTournament(int id)
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

            if (tournament.Status == TournamentStatuses.Cancelled)
            {
                return Ok(new AdminActionResponse
                {
                    Message = "Tournament is already in trash",
                    Id = tournament.TournamentId,
                    Name = tournament.TournamentName,
                    Status = tournament.Status
                });
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                tournament.Status = TournamentStatuses.Cancelled;
                tournament.UpdatedAt = DateTime.UtcNow;

                var races = await _context.Races
                    .Where(r => r.TournamentId == id)
                    .ToListAsync();

                foreach (var race in races)
                {
                    race.Status = RaceStatuses.Cancelled;
                    race.UpdatedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new AdminActionResponse
                {
                    Message = "Tournament moved to trash successfully",
                    Id = tournament.TournamentId,
                    Name = tournament.TournamentName,
                    Status = tournament.Status
                });
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        [HttpPut("{id}/restore")]
        public async Task<IActionResult> RestoreTournament(int id)
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

            if (tournament.Status != TournamentStatuses.Cancelled)
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "Only cancelled tournament can be restored",
                    Id = tournament.TournamentId,
                    Name = tournament.TournamentName,
                    Status = tournament.Status
                });
            }

            tournament.Status = TournamentStatuses.Draft;
            tournament.UpdatedAt = DateTime.UtcNow;

            var races = await _context.Races
                .Where(r => r.TournamentId == id)
                .ToListAsync();

            foreach (var race in races)
            {
                race.Status = RaceStatuses.Scheduled;
                race.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            return Ok(new AdminActionResponse
            {
                Message = "Tournament restored successfully",
                Id = tournament.TournamentId,
                Name = tournament.TournamentName,
                Status = tournament.Status
            });
        }


        [HttpGet("referees")]
        public async Task<IActionResult> GetRaceReferees()
        {
            var referees = await _context.Users
                .Where(u => u.Role == UserRoles.RaceReferee && u.Status == UserStatuses.Active)
                .Select(u => new
                {
                    refereeId = u.UserId,
                    fullName = u.FullName,
                    email = u.Email
                })
                .OrderBy(u => u.fullName)
                .ToListAsync();

            return Ok(referees);
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

            if (tournament.Status == TournamentStatuses.Cancelled)
            {
                return Ok(new AdminActionResponse
                {
                    Message = "Tournament is already cancelled",
                    Id = tournament.TournamentId,
                    Name = tournament.TournamentName,
                    Status = tournament.Status
                });
            }

            tournament.Status = TournamentStatuses.Cancelled;
            tournament.UpdatedAt = DateTime.UtcNow;

            var races = await _context.Races
                .Where(r => r.TournamentId == id)
                .ToListAsync();

            foreach (var race in races)
            {
                race.Status = RaceStatuses.Cancelled;
                race.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            return Ok(new AdminActionResponse
            {
                Message = "Tournament cancelled successfully",
                Id = tournament.TournamentId,
                Name = tournament.TournamentName,
                Status = tournament.Status
            });
        }

        private IActionResult? ValidateTournamentRequest(AdminTournamentRequest request, int id)
        {
            if (string.IsNullOrWhiteSpace(request.TournamentName))
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "Tournament name is required",
                    Id = id
                });
            }

            if (string.IsNullOrWhiteSpace(request.Location))
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "Location is required",
                    Id = id
                });
            }

            if (request.RegistrationDeadline >= request.RaceDate)
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "Registration deadline must be before race date",
                    Id = id
                });
            }

            if (request.MaxHorses < 1 || request.MaxHorses > 20)
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "Max horses must be between 1 and 20",
                    Id = id
                });
            }

            if (request.DistanceMeters != 1000 &&
                request.DistanceMeters != 1500 &&
                request.DistanceMeters != 2400)
            {
                return BadRequest(new AdminActionResponse
                {   
                    Message = "Distance must be 1000, 1500, or 2400 meters",
                    Id = id
                });
            }

            return null;
        }
    }
}