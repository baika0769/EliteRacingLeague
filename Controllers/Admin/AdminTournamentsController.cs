using Microsoft.AspNetCore.Mvc;
using Eliteracingleague.API.Services;
using Microsoft.EntityFrameworkCore;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Admin;
using Microsoft.AspNetCore.Authorization;
using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Models;
using System.Security.Claims;
using Eliteracingleague.API.Services.Notifications;
using System.Globalization;

namespace Eliteracingleague.API.Controllers.Admin
{
    [Authorize(Roles = UserRoles.Admin)]
    [ApiController]
    [Route("api/admin/tournaments")]
    public class AdminTournamentsController : ControllerBase
    {
        private readonly EliteRacingLeagueContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly TournamentStatusService _tournamentStatusService;
        private readonly INotificationService _notificationService;

        public AdminTournamentsController(
    EliteRacingLeagueContext context,
    IWebHostEnvironment env,
    TournamentStatusService tournamentStatusService,
    INotificationService notificationService)
        {
            _context = context;
            _env = env;
            _tournamentStatusService = tournamentStatusService;
            _notificationService = notificationService;
        }

        [HttpGet]
        public async Task<IActionResult> GetTournaments()
        {
            await _tournamentStatusService.SyncTournamentStatusesAsync();

            var tournaments = await _context.Tournaments
                .AsNoTracking()
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
                    ImageUrl = t.ImageUrl,
                    Status = t.Status,
                    Rules = t.Rules,
                    CreatedBy = t.CreatedBy,
                    CreatedAt = t.CreatedAt,
                    RaceId = t.Race == null ? null : t.Race.RaceId,
                    RaceDateTime = t.Race == null ? null : t.Race.RaceDate,
                    RaceStartTime = t.Race == null ? null : t.Race.RaceDate.ToString("HH:mm"),
                    DistanceMeters = t.Race == null ? null : t.Race.DistanceMeters,
                    RaceStatus = t.Race == null ? null : t.Race.Status,

                    EntriesCount = _context.RaceRegistrations
                        .Count(r => r.Race.TournamentId == t.TournamentId),

                    EntriesText =
                        _context.RaceRegistrations
                            .Count(r => r.Race.TournamentId == t.TournamentId)
                        + "/" + t.MaxHorses,

                    Referee = t.Race == null
                        ? "Unassigned"
                        : t.Race.RefereeAssignments
                            .OrderByDescending(a => a.AssignedAt)
                            .Select(a => a.Referee.Referee.FullName)
                            .FirstOrDefault() ?? "Unassigned"
                })
                .OrderByDescending(t => t.TournamentId)
                .ToListAsync();

            return Ok(tournaments);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetTournamentById(int id)
        {
            await _tournamentStatusService.SyncTournamentStatusesAsync();

            var tournament = await _context.Tournaments
                .AsNoTracking()
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
                    ImageUrl = t.ImageUrl,
                    Status = t.Status,
                    Rules = t.Rules,
                    CreatedBy = t.CreatedBy,
                    CreatedAt = t.CreatedAt,
                    RaceId = t.Race == null ? null : t.Race.RaceId,
                    RaceDateTime = t.Race == null ? null : t.Race.RaceDate,
                    RaceStartTime = t.Race == null ? null : t.Race.RaceDate.ToString("HH:mm"),
                    DistanceMeters = t.Race == null ? null : t.Race.DistanceMeters,
                    RaceStatus = t.Race == null ? null : t.Race.Status,

                    EntriesCount = _context.RaceRegistrations
                        .Count(r => r.Race.TournamentId == t.TournamentId),

                    EntriesText =
                        _context.RaceRegistrations
                            .Count(r => r.Race.TournamentId == t.TournamentId)
                        + "/" + t.MaxHorses,

                    Referee = t.Race == null
                        ? "Unassigned"
                        : t.Race.RefereeAssignments
                            .OrderByDescending(a => a.AssignedAt)
                            .Select(a => a.Referee.Referee.FullName)
                            .FirstOrDefault() ?? "Unassigned"
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
        public async Task<IActionResult> CreateTournament([FromForm] AdminTournamentRequest request)
        {
            var validationResult = ValidateTournamentRequest(request, 0);
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

            var raceDateTime = BuildRaceDateTime(request);
            var season = await FindSeasonForRaceDateAsync(raceDateTime);

            if (season == null)
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "No quarter season found for race date. Please create quarter seasons first."
                });
            }

            string? imageUrl;

            try
            {
                imageUrl = await SaveTournamentImageAsync(request.TournamentImage);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = ex.Message
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
                    Location = request.Location!.Trim(),
                    StartDate = request.RegistrationDeadline,
                    EndDate = request.RaceDate,
                    MaxHorses = request.MaxHorses,
                    PrizePool = request.PrizePool,
                    ImageUrl = imageUrl,
                    Status = TournamentStatuses.Draft,
                    Rules = request.Rules,
                    SeasonId = season.SeasonId,
                    CreatedAt = now,
                    CreatedBy = adminId,
                    UpdatedAt = now
                };

                _context.Tournaments.Add(tournament);
                await _context.SaveChangesAsync();

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
                    status = tournament.Status,
                    imageUrl = tournament.ImageUrl
                });
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateTournament(int id, [FromForm] AdminTournamentRequest request)
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

            if (request.TournamentImage != null && request.TournamentImage.Length > 0)
            {
                try
                {
                    tournament.ImageUrl = await SaveTournamentImageAsync(request.TournamentImage);
                }
                catch (InvalidOperationException ex)
                {
                    return BadRequest(new AdminActionResponse
                    {
                        Message = ex.Message,
                        Id = id
                    });
                }
            }

            tournament.TournamentName = request.TournamentName.Trim();
            tournament.Description = request.Description;
            tournament.Location = request.Location!.Trim();
            tournament.StartDate = request.RegistrationDeadline;
            tournament.EndDate = request.RaceDate;
            tournament.MaxHorses = request.MaxHorses;
            tournament.PrizePool = request.PrizePool;
            tournament.Rules = request.Rules;

            if (!string.IsNullOrWhiteSpace(request.Status))
            {
                tournament.Status = request.Status;
            }

            tournament.UpdatedAt = now;

            var race = await _context.Races
                .FirstOrDefaultAsync(r => r.TournamentId == tournament.TournamentId);

            var raceDateTime = BuildRaceDateTime(request);
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
                    Status = MapTournamentStatusToRaceStatus(tournament.Status, RaceStatuses.Scheduled),
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

                if (!string.IsNullOrWhiteSpace(request.Status))
                {
                    race.Status = MapTournamentStatusToRaceStatus(tournament.Status, race.Status);
                }

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

        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateTournamentStatus(int id, [FromBody] UpdateTournamentStatusRequest request)
        {
            var tournament = await _context.Tournaments
                .Include(t => t.Race)
                .FirstOrDefaultAsync(t => t.TournamentId == id);

            if (tournament == null)
            {
                return NotFound(new AdminActionResponse
                {
                    Message = "Tournament not found",
                    Id = id
                });
            }

            if (string.IsNullOrWhiteSpace(request.Status) ||
                !TournamentStatuses.IsValid(request.Status))
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "Invalid tournament status",
                    Id = id,
                    Name = tournament.TournamentName,
                    Status = tournament.Status
                });
            }

            var now = DateTime.UtcNow;

            if (tournament.Status == TournamentStatuses.Completed &&
                request.Status != TournamentStatuses.Completed)
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "Completed tournament cannot be changed to another status",
                    Id = id,
                    Name = tournament.TournamentName,
                    Status = tournament.Status
                });
            }

            if (request.Status == TournamentStatuses.OpenRegistration &&
                tournament.StartDate < DateOnly.FromDateTime(now))
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "Cannot open registration because the registration deadline has passed. Please update the registration deadline first.",
                    Id = id,
                    Name = tournament.TournamentName,
                    Status = tournament.Status
                });
            }

            tournament.Status = request.Status;
            tournament.UpdatedAt = now;

            if (tournament.Race != null)
            {
                tournament.Race.Status = MapTournamentStatusToRaceStatus(
                    tournament.Status,
                    tournament.Race.Status
                );

                tournament.Race.UpdatedAt = now;
            }

            await _context.SaveChangesAsync();

            return Ok(new AdminActionResponse
            {
                Message = "Tournament status updated successfully",
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

            var race = await _context.Races
                .FirstOrDefaultAsync(r => r.TournamentId == tournament.TournamentId);

            if (race != null && race.Status == RaceStatuses.Cancelled)
            {
                race.Status = RaceStatuses.Scheduled;
                race.UpdatedAt = DateTime.UtcNow;
            }

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
                var now = DateTime.UtcNow;

                tournament.Status = TournamentStatuses.Cancelled;
                tournament.UpdatedAt = now;

                var races = await _context.Races
                    .Where(r => r.TournamentId == id)
                    .ToListAsync();

                foreach (var race in races)
                {
                    race.Status = RaceStatuses.Cancelled;
                    race.UpdatedAt = now;
                }

                var raceIds = races.Select(r => r.RaceId).ToList();

                await CancelTournamentRelatedDataAsync(raceIds, now);

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

            var now = DateTime.UtcNow;

            tournament.Status = TournamentStatuses.Draft;
            tournament.UpdatedAt = now;

            var races = await _context.Races
                .Where(r => r.TournamentId == id)
                .ToListAsync();

            foreach (var race in races)
            {
                race.Status = RaceStatuses.Scheduled;
                race.UpdatedAt = now;
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

        [HttpPut("{id}/assign-referee")]
        public async Task<IActionResult> AssignReferee(
            int id,
            [FromBody] AssignRefereeRequest request)
        {
            var race = await _context.Races
                .Include(r => r.Tournament)
                .FirstOrDefaultAsync(r => r.TournamentId == id);

            if (race == null)
            {
                return NotFound(new
                {
                    message = "Race not found"
                });
            }

            var referee = await _context.RaceReferees
                .FirstOrDefaultAsync(r => r.RefereeId == request.RefereeId);

            if (referee == null)
            {
                return BadRequest(new
                {
                    message = "Referee not found"
                });
            }

            var existing = await _context.RefereeAssignments
                .FirstOrDefaultAsync(x => x.RaceId == race.RaceId);

            if (existing != null)
            {
                existing.RefereeId = request.RefereeId;
                existing.Status = RefereeAssignmentStatuses.Assigned;
                existing.AssignedAt = DateTime.UtcNow;
            }
            else
            {
                var adminIdText = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (!int.TryParse(adminIdText, out var adminId))
                {
                    return Unauthorized(new
                    {
                        message = "Invalid admin token"
                    });
                }

                _context.RefereeAssignments.Add(
                    new RefereeAssignment
                    {
                        RaceId = race.RaceId,
                        RefereeId = request.RefereeId,
                        AssignedBy = adminId,
                        Status = RefereeAssignmentStatuses.Assigned,
                        AssignedAt = DateTime.UtcNow
                    });
            }

            if (race.Tournament.Status == TournamentStatuses.Ongoing)
            {
                race.Status = RaceStatuses.Ongoing;
            }
            else if (race.Status == RaceStatuses.Scheduled)
            {
                race.Status = RaceStatuses.AssignedReferee;
            }

            race.UpdatedAt = DateTime.UtcNow;
            await _notificationService.CreateForUserAsync(
    request.RefereeId,
    "Race Assigned",
    $"You have been assigned to referee {race.RaceName} in tournament {race.Tournament.TournamentName}.",
    "RefereeRaceAssignment",
    "/referee/races",
    "Race",
    race.RaceId);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Referee assigned successfully"
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

            var now = DateTime.UtcNow;

            tournament.Status = TournamentStatuses.Cancelled;
            tournament.UpdatedAt = now;

            var races = await _context.Races
                .Where(r => r.TournamentId == id)
                .ToListAsync();

            foreach (var race in races)
            {
                race.Status = RaceStatuses.Cancelled;
                race.UpdatedAt = now;
            }

            var raceIds = races.Select(r => r.RaceId).ToList();

            await CancelTournamentRelatedDataAsync(raceIds, now);

            await _context.SaveChangesAsync();

            return Ok(new AdminActionResponse
            {
                Message = "Tournament cancelled successfully",
                Id = tournament.TournamentId,
                Name = tournament.TournamentName,
                Status = tournament.Status
            });
        }

        private static readonly string[] RaceStartTimeFormats =
        {
            "HH:mm",
            "H:mm",
            "HH:mm:ss",
            "H:mm:ss"
        };

        private static bool TryParseRaceStartTime(string? value, out TimeOnly raceStartTime)
        {
            raceStartTime = default;

            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return TimeOnly.TryParseExact(
                value.Trim(),
                RaceStartTimeFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out raceStartTime);
        }

        private static DateTime BuildRaceDateTime(AdminTournamentRequest request)
        {
            TryParseRaceStartTime(request.RaceStartTime, out var raceStartTime);

            return request.RaceDate.ToDateTime(raceStartTime);
        }

        private async Task<Season?> FindSeasonForRaceDateAsync(DateTime raceDateTime)
        {
            var matchingSeasons = await _context.Seasons
                .Where(s =>
                    s.Status != SeasonStatuses.Cancelled &&
                    s.StartDate <= raceDateTime &&
                    s.EndDate >= raceDateTime)
                .ToListAsync();

            return matchingSeasons
                .OrderBy(s => (s.EndDate.Date - s.StartDate.Date).Days)
                .ThenByDescending(s => s.StartDate)
                .FirstOrDefault();
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

            if (!TryParseRaceStartTime(request.RaceStartTime, out _))
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "Race start time is required and must be in HH:mm format. Example: 14:30",
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

        private async Task<string?> SaveTournamentImageAsync(IFormFile? file)
        {
            if (file == null || file.Length == 0)
            {
                return null;
            }

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(extension))
            {
                throw new InvalidOperationException("Only JPG, JPEG, PNG, and WEBP images are allowed.");
            }

            if (file.Length > 5 * 1024 * 1024)
            {
                throw new InvalidOperationException("Tournament image must be less than 5MB.");
            }

            var webRootPath = _env.WebRootPath;

            if (string.IsNullOrWhiteSpace(webRootPath))
            {
                webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            }

            var uploadFolder = Path.Combine(webRootPath, "uploads", "tournaments");

            if (!Directory.Exists(uploadFolder))
            {
                Directory.CreateDirectory(uploadFolder);
            }

            var fileName = $"{Guid.NewGuid():N}{extension}";
            var fullPath = Path.Combine(uploadFolder, fileName);

            await using var stream = System.IO.File.Create(fullPath);
            await file.CopyToAsync(stream);

            return $"/uploads/tournaments/{fileName}";
        }


        private async Task CancelTournamentRelatedDataAsync(List<int> raceIds, DateTime now)
        {
            if (raceIds == null || raceIds.Count == 0)
            {
                return;
            }

            var registrations = await _context.RaceRegistrations
                .Where(r => raceIds.Contains(r.RaceId)
                    && r.Status != RaceRegistrationStatuses.Cancelled
                    && r.Status != RaceRegistrationStatuses.Rejected)
                .ToListAsync();

            foreach (var registration in registrations)
            {
                registration.Status = RaceRegistrationStatuses.Cancelled;
                registration.AdminNote = "Tournament cancelled";
                registration.ReviewedAt = now;
            }

            var invitations = await _context.JockeyInvitations
                .Where(i => raceIds.Contains(i.Registration.RaceId)
                    && i.Status == InvitationStatuses.Pending)
                .ToListAsync();

            foreach (var invitation in invitations)
            {
                invitation.Status = InvitationStatuses.Cancelled;
                invitation.RespondedAt = now;
            }

            var assignments = await _context.RefereeAssignments
                .Where(a => raceIds.Contains(a.RaceId)
                    && a.Status == RefereeAssignmentStatuses.Assigned)
                .ToListAsync();

            foreach (var assignment in assignments)
            {
                assignment.Status = RefereeAssignmentStatuses.Cancelled;
            }
        }

        private static string MapTournamentStatusToRaceStatus(
            string tournamentStatus,
            string currentRaceStatus)
        {
            return tournamentStatus switch
            {
                TournamentStatuses.Ongoing => RaceStatuses.Ongoing,
                TournamentStatuses.Completed => RaceStatuses.Finished,
                TournamentStatuses.Cancelled => RaceStatuses.Cancelled,
                TournamentStatuses.Draft => RaceStatuses.Scheduled,
                TournamentStatuses.OpenRegistration => currentRaceStatus == RaceStatuses.Cancelled
                    ? RaceStatuses.Scheduled
                    : currentRaceStatus,
                TournamentStatuses.ClosedRegistration => currentRaceStatus == RaceStatuses.Scheduled
                    ? RaceStatuses.Scheduled
                    : currentRaceStatus,
                _ => string.IsNullOrWhiteSpace(currentRaceStatus)
                    ? RaceStatuses.Scheduled
                    : currentRaceStatus
            };
        }
    }


}
