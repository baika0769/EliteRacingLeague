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
using Eliteracingleague.API.Services.SystemTime;
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
        private readonly IDateTimeProvider _dateTimeProvider;

        public AdminTournamentsController(
            EliteRacingLeagueContext context,
            IWebHostEnvironment env,
            TournamentStatusService tournamentStatusService,
            INotificationService notificationService,
            IDateTimeProvider dateTimeProvider)
        {
            _context = context;
            _env = env;
            _tournamentStatusService = tournamentStatusService;
            _notificationService = notificationService;
            _dateTimeProvider = dateTimeProvider;
        }

        [HttpGet]
        public async Task<IActionResult> GetTournaments()
        {
            await _tournamentStatusService.SyncTournamentStatusesAsync();

            var tournaments = await _context.Tournaments
                .AsNoTracking()
                .Select(t => new
                {
                    tournamentId = t.TournamentId,
                    tournamentName = t.TournamentName,
                    description = t.Description,
                    location = t.Location,
                    startDate = t.StartDate,
                    endDate = t.EndDate,
                    maxHorses = t.MaxHorses,
                    prizePool = t.PrizePool,
                    imageUrl = t.ImageUrl,
                    status = t.Status,
                    rules = t.Rules,
                    createdBy = t.CreatedBy,
                    createdAt = t.CreatedAt,

                    seasonId = t.SeasonId,
                    seasonName = t.Season.SeasonName,
                    seasonStatus = t.Season.Status,

                    raceId = t.Race == null ? (int?)null : t.Race.RaceId,
                    raceDateTime = t.Race == null ? (DateTime?)null : t.Race.RaceDate,
                    raceStartTime = t.Race == null
                        ? null
                        : t.Race.RaceDate.ToString("HH:mm"),
                    distanceMeters = t.Race == null ? (int?)null : t.Race.DistanceMeters,
                    raceStatus = t.Race == null ? null : t.Race.Status,
                    predictionDeadline = t.Race == null
                        ? (DateTime?)null
                        : t.Race.PredictionDeadline,

                    entriesCount = _context.RaceRegistrations
                        .Count(r =>
                            r.Race.TournamentId == t.TournamentId &&
                            r.Status != RaceRegistrationStatuses.Rejected &&
                            r.Status != RaceRegistrationStatuses.Cancelled),

                    entriesText =
                        _context.RaceRegistrations
                            .Count(r =>
                                r.Race.TournamentId == t.TournamentId &&
                                r.Status != RaceRegistrationStatuses.Rejected &&
                                r.Status != RaceRegistrationStatuses.Cancelled)
                        + "/" + t.MaxHorses,

                    referee = t.Race == null
                        ? "Unassigned"
                        : t.Race.RefereeAssignments
                            .OrderByDescending(a => a.AssignedAt)
                            .Select(a => a.Referee.Referee.FullName)
                            .FirstOrDefault() ?? "Unassigned"
                })
                .OrderByDescending(t => t.tournamentId)
                .ToListAsync();

            return Ok(tournaments);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetTournamentById(int id)
        {
            await _tournamentStatusService.SyncTournamentStatusesAsync();

            var tournament = await _context.Tournaments
                .AsNoTracking()
                .Where(t => t.TournamentId == id)
                .Select(t => new
                {
                    tournamentId = t.TournamentId,
                    tournamentName = t.TournamentName,
                    description = t.Description,
                    location = t.Location,
                    startDate = t.StartDate,
                    endDate = t.EndDate,
                    maxHorses = t.MaxHorses,
                    prizePool = t.PrizePool,
                    imageUrl = t.ImageUrl,
                    status = t.Status,
                    rules = t.Rules,
                    createdBy = t.CreatedBy,
                    createdAt = t.CreatedAt,

                    seasonId = t.SeasonId,
                    seasonName = t.Season.SeasonName,
                    seasonStatus = t.Season.Status,

                    raceId = t.Race == null ? (int?)null : t.Race.RaceId,
                    raceDateTime = t.Race == null ? (DateTime?)null : t.Race.RaceDate,
                    raceStartTime = t.Race == null
                        ? null
                        : t.Race.RaceDate.ToString("HH:mm"),
                    distanceMeters = t.Race == null ? (int?)null : t.Race.DistanceMeters,
                    raceStatus = t.Race == null ? null : t.Race.Status,
                    predictionDeadline = t.Race == null
                        ? (DateTime?)null
                        : t.Race.PredictionDeadline,

                    entriesCount = _context.RaceRegistrations
                        .Count(r =>
                            r.Race.TournamentId == t.TournamentId &&
                            r.Status != RaceRegistrationStatuses.Rejected &&
                            r.Status != RaceRegistrationStatuses.Cancelled),

                    entriesText =
                        _context.RaceRegistrations
                            .Count(r =>
                                r.Race.TournamentId == t.TournamentId &&
                                r.Status != RaceRegistrationStatuses.Rejected &&
                                r.Status != RaceRegistrationStatuses.Cancelled)
                        + "/" + t.MaxHorses,

                    referee = t.Race == null
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
        public async Task<IActionResult> CreateTournament(
            [FromForm] AdminTournamentRequest request)
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
                return BadRequest(new
                {
                    code = "ELIGIBLE_SEASON_NOT_FOUND",
                    message = "No draft or active season contains the selected race date. Create or update a suitable season first.",
                    raceDate = request.RaceDate
                });
            }

            string? imageUrl;

            try
            {
                imageUrl = await SaveTournamentImageAsync(
                    request.TournamentImage);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = ex.Message
                });
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                var now = _dateTimeProvider.UtcNow;

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

                var jockeySelectionDeadline =
                    request.RegistrationDeadline.ToDateTime(TimeOnly.MaxValue);

                var race = new Race
                {
                    TournamentId = tournament.TournamentId,
                    RaceName = tournament.TournamentName,
                    RaceDate = raceDateTime,
                    DistanceMeters = request.DistanceMeters,
                    Location = tournament.Location,
                    MaxHorses = request.MaxHorses,
                    Status = RaceStatuses.Scheduled,
                    JockeySelectionDeadline = jockeySelectionDeadline,
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
                    seasonId = season.SeasonId,
                    seasonName = season.SeasonName,
                    seasonStatus = season.Status,
                    imageUrl = tournament.ImageUrl
                });
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateTournament(
            int id,
            [FromForm] AdminTournamentRequest request)
        {
            var validationResult = ValidateTournamentRequest(request, id);
            if (validationResult != null)
            {
                return validationResult;
            }

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

            if (tournament.Status is
                TournamentStatuses.Completed or
                TournamentStatuses.Cancelled)
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "Completed or cancelled tournament cannot be edited from this endpoint.",
                    Id = id,
                    Name = tournament.TournamentName,
                    Status = tournament.Status
                });
            }

            var race = tournament.Race;

            if (race != null &&
                race.Status is
                    RaceStatuses.Ongoing or
                    RaceStatuses.Finished or
                    RaceStatuses.ResultPending or
                    RaceStatuses.Published or
                    RaceStatuses.Cancelled)
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "Tournament cannot be edited after its race has started, finished, entered result approval, been published, or been cancelled.",
                    Id = id,
                    Name = tournament.TournamentName,
                    Status = race.Status
                });
            }

            var raceDateTime = BuildRaceDateTime(request);
            var jockeySelectionDeadline =
                request.RegistrationDeadline.ToDateTime(TimeOnly.MaxValue);
            var matchingSeason =
                await FindSeasonForRaceDateAsync(raceDateTime);

            if (matchingSeason == null)
            {
                return BadRequest(new
                {
                    code = "ELIGIBLE_SEASON_NOT_FOUND",
                    message = "No draft or active season contains the selected race date.",
                    tournamentId = id,
                    raceDate = request.RaceDate
                });
            }

            if (tournament.Status != TournamentStatuses.Draft &&
                matchingSeason.Status != SeasonStatuses.Active)
            {
                return BadRequest(new
                {
                    code = "SEASON_NOT_ACTIVE",
                    message = "A non-draft tournament must remain in an active season.",
                    tournamentId = tournament.TournamentId,
                    tournamentStatus = tournament.Status,
                    seasonId = matchingSeason.SeasonId,
                    seasonName = matchingSeason.SeasonName,
                    seasonStatus = matchingSeason.Status
                });
            }

            if (race != null)
            {
                var hasRegistrations = await _context.RaceRegistrations
                    .AnyAsync(r => r.RaceId == race.RaceId);

                if (hasRegistrations)
                {
                    var sensitiveFieldChanged =
                        tournament.StartDate != request.RegistrationDeadline ||
                        tournament.EndDate != request.RaceDate ||
                        tournament.MaxHorses != request.MaxHorses ||
                        tournament.PrizePool != request.PrizePool ||
                        race.RaceDate != raceDateTime ||
                        race.DistanceMeters != request.DistanceMeters ||
                        race.MaxHorses != request.MaxHorses ||
                        race.JockeySelectionDeadline != jockeySelectionDeadline ||
                        race.PredictionDeadline != raceDateTime.AddHours(-1) ||
                        tournament.SeasonId != matchingSeason.SeasonId;

                    if (sensitiveFieldChanged)
                    {
                        return BadRequest(new AdminActionResponse
                        {
                            Message = "Tournament already has registrations. Dates, deadlines, distance, max horses, prize pool, and season cannot be changed.",
                            Id = id,
                            Name = tournament.TournamentName,
                            Status = tournament.Status
                        });
                    }
                }
            }

            if (request.TournamentImage != null &&
                request.TournamentImage.Length > 0)
            {
                try
                {
                    tournament.ImageUrl = await SaveTournamentImageAsync(
                        request.TournamentImage);
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

            var now = _dateTimeProvider.UtcNow;

            tournament.TournamentName = request.TournamentName.Trim();
            tournament.Description = request.Description;
            tournament.Location = request.Location!.Trim();
            tournament.StartDate = request.RegistrationDeadline;
            tournament.EndDate = request.RaceDate;
            tournament.MaxHorses = request.MaxHorses;
            tournament.PrizePool = request.PrizePool;
            tournament.Rules = request.Rules;
            tournament.SeasonId = matchingSeason.SeasonId;
            tournament.UpdatedAt = now;

          
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
                    JockeySelectionDeadline = jockeySelectionDeadline,
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
                race.JockeySelectionDeadline = jockeySelectionDeadline;
                race.PredictionDeadline = raceDateTime.AddHours(-1);
                race.UpdatedAt = now;
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Tournament updated successfully",
                tournamentId = tournament.TournamentId,
                tournamentName = tournament.TournamentName,
                status = tournament.Status,
                seasonId = matchingSeason.SeasonId,
                seasonName = matchingSeason.SeasonName,
                seasonStatus = matchingSeason.Status
            });
        }

        [HttpPut("{id:int}/status")]
        public async Task<IActionResult> UpdateTournamentStatus(
            int id,
            [FromBody] UpdateTournamentStatusRequest request)
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

            if (request.Status == tournament.Status)
            {
                return Ok(new AdminActionResponse
                {
                    Message = "Tournament already has the requested status",
                    Id = tournament.TournamentId,
                    Name = tournament.TournamentName,
                    Status = tournament.Status
                });
            }

            var validManualTransition =
                tournament.Status == TournamentStatuses.OpenRegistration &&
                request.Status == TournamentStatuses.ClosedRegistration;

            if (!validManualTransition)
            {
                return BadRequest(new
                {
                    code = "INVALID_TOURNAMENT_STATUS_TRANSITION",
                    message = "This endpoint only supports manually closing registration: OpenRegistration -> ClosedRegistration.",
                    tournamentId = tournament.TournamentId,
                    tournamentName = tournament.TournamentName,
                    currentStatus = tournament.Status,
                    requestedStatus = request.Status
                });
            }

            var now = _dateTimeProvider.UtcNow;

            tournament.Status = TournamentStatuses.ClosedRegistration;
            tournament.UpdatedAt = now;

            if (tournament.Race != null)
            {
                tournament.Race.UpdatedAt = now;
            }

            await _context.SaveChangesAsync();

            return Ok(new AdminActionResponse
            {
                Message = "Tournament registration closed successfully",
                Id = tournament.TournamentId,
                Name = tournament.TournamentName,
                Status = tournament.Status
            });
        }

        [HttpPut("{id:int}/approve")]
        public async Task<IActionResult> ApproveTournament(int id)
        {
            var tournament = await _context.Tournaments
                .Include(t => t.Season)
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

            if (tournament.Status != TournamentStatuses.Draft)
            {
                return BadRequest(new
                {
                    code = "TOURNAMENT_NOT_DRAFT",
                    message = "Only a draft tournament can be approved and opened for registration.",
                    tournamentId = tournament.TournamentId,
                    tournamentName = tournament.TournamentName,
                    currentStatus = tournament.Status
                });
            }

            if (tournament.Season == null)
            {
                return BadRequest(new
                {
                    code = "SEASON_NOT_ASSIGNED",
                    message = "Tournament has not been assigned to a season.",
                    tournamentId = tournament.TournamentId,
                    tournamentName = tournament.TournamentName
                });
            }

            if (tournament.Season.Status != SeasonStatuses.Active)
            {
                return BadRequest(new
                {
                    code = "SEASON_NOT_ACTIVE",
                    message = "The tournament season must be active before registration can be opened.",
                    tournamentId = tournament.TournamentId,
                    tournamentName = tournament.TournamentName,
                    seasonId = tournament.Season.SeasonId,
                    seasonName = tournament.Season.SeasonName,
                    seasonStatus = tournament.Season.Status
                });
            }

            if (tournament.Race == null)
            {
                return BadRequest(new
                {
                    code = "RACE_NOT_FOUND",
                    message = "Tournament cannot be approved because its race does not exist.",
                    tournamentId = tournament.TournamentId
                });
            }

            if (!RaceStatuses.CanRegister(tournament.Race.Status))
            {
                return BadRequest(new
                {
                    code = "RACE_NOT_REGISTERABLE",
                    message = "Tournament cannot be approved because the race is not in a registerable status.",
                    tournamentId = tournament.TournamentId,
                    raceId = tournament.Race.RaceId,
                    raceStatus = tournament.Race.Status
                });
            }

            var localNow = _dateTimeProvider.GetLocalNow(
                _dateTimeProvider.TimeZoneId);
            var localToday = DateOnly.FromDateTime(localNow);

            if (tournament.StartDate < localToday)
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "Cannot open registration because the registration deadline has passed. Update the registration deadline first.",
                    Id = tournament.TournamentId,
                    Name = tournament.TournamentName,
                    Status = tournament.Status
                });
            }

            var raceDate = DateOnly.FromDateTime(
                tournament.Race.RaceDate);

            if (raceDate < localToday)
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "Cannot open registration for a race date that has already passed.",
                    Id = tournament.TournamentId,
                    Name = tournament.TournamentName,
                    Status = tournament.Status
                });
            }

            var now = _dateTimeProvider.UtcNow;

            tournament.Status = TournamentStatuses.OpenRegistration;
            tournament.UpdatedAt = now;
            tournament.Race.UpdatedAt = now;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Tournament approved and registration opened successfully",
                tournamentId = tournament.TournamentId,
                tournamentName = tournament.TournamentName,
                status = tournament.Status,
                seasonId = tournament.Season.SeasonId,
                seasonName = tournament.Season.SeasonName,
                seasonStatus = tournament.Season.Status,
                raceId = tournament.Race.RaceId,
                raceStatus = tournament.Race.Status
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

            if (tournament.Status == TournamentStatuses.Completed)
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "Completed tournament cannot be moved to trash without a correction flow",
                    Id = tournament.TournamentId,
                    Name = tournament.TournamentName,
                    Status = tournament.Status
                });
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var now = _dateTimeProvider.UtcNow;

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

        [HttpPut("{id:int}/restore")]
        public async Task<IActionResult> RestoreTournament(int id)
        {
            var tournament = await _context.Tournaments
                .Include(t => t.Season)
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

            if (tournament.Season == null ||
                tournament.Season.Status is
                    SeasonStatuses.Closed or
                    SeasonStatuses.Cancelled)
            {
                return BadRequest(new
                {
                    code = "SEASON_NOT_RESTORABLE",
                    message = "Tournament cannot be restored into a closed, cancelled, or missing season.",
                    tournamentId = tournament.TournamentId,
                    seasonId = tournament.Season?.SeasonId,
                    seasonName = tournament.Season?.SeasonName,
                    seasonStatus = tournament.Season?.Status
                });
            }

            var raceIds = await _context.Races
                .Where(r => r.TournamentId == id)
                .Select(r => r.RaceId)
                .ToListAsync();

            var hasChildWorkflowData =
                await _context.RaceRegistrations
                    .AnyAsync(r => raceIds.Contains(r.RaceId)) ||
                await _context.RacePredictions
                    .AnyAsync(p => raceIds.Contains(p.RaceId)) ||
                await _context.JockeyInvitations
                    .AnyAsync(i =>
                        raceIds.Contains(i.Registration.RaceId)) ||
                await _context.RefereeAssignments
                    .AnyAsync(a => raceIds.Contains(a.RaceId));

            if (hasChildWorkflowData)
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "Cannot restore a cancelled tournament that already has registrations, predictions, invitations, or referee assignments. Create a new tournament instead.",
                    Id = tournament.TournamentId,
                    Name = tournament.TournamentName,
                    Status = tournament.Status
                });
            }

            var now = _dateTimeProvider.UtcNow;

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
                existing.AssignedAt = _dateTimeProvider.UtcNow;
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
                        AssignedAt = _dateTimeProvider.UtcNow
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

            race.UpdatedAt = _dateTimeProvider.UtcNow;
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

            if (tournament.Status == TournamentStatuses.Completed)
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "Completed tournament cannot be cancelled without a correction flow",
                    Id = tournament.TournamentId,
                    Name = tournament.TournamentName,
                    Status = tournament.Status
                });
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var now = _dateTimeProvider.UtcNow;

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
                    Message = "Tournament cancelled successfully",
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

        private async Task<Season?> FindSeasonForRaceDateAsync(
            DateTime raceDateTime)
        {
            var raceDate = raceDateTime.Date;

            return await _context.Seasons
                .AsNoTracking()
                .Where(s =>
                    s.Status != SeasonStatuses.Closed &&
                    s.Status != SeasonStatuses.Cancelled &&
                    s.StartDate <= raceDate &&
                    s.EndDate >= raceDate)
                .OrderByDescending(s =>
                    s.Status == SeasonStatuses.Active)
                .ThenBy(s => s.StartDate)
                .ThenBy(s => s.SeasonId)
                .FirstOrDefaultAsync();
        }

        private IActionResult? ValidateTournamentRequest(
            AdminTournamentRequest request,
            int id)
        {
            if (string.IsNullOrWhiteSpace(request.TournamentName))
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "Tournament name is required",
                    Id = id
                });
            }

            if (request.TournamentName.Trim().Length > 200)
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "Tournament name cannot exceed 200 characters",
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

            if (request.RaceDate == default)
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "Race date is required",
                    Id = id
                });
            }

            if (request.RegistrationDeadline == default)
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "Registration deadline is required",
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

            if (!TryParseRaceStartTime(
                    request.RaceStartTime,
                    out _))
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "Race start time is required and must be in HH:mm format. Example: 14:30",
                    Id = id
                });
            }

            if (request.MaxHorses < 1 ||
                request.MaxHorses > 20)
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

            if (request.PrizePool < 0)
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "Prize pool cannot be negative",
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
                    && r.Status != RaceRegistrationStatuses.Rejected
                    && r.Status != RaceRegistrationStatuses.Completed)
                .ToListAsync();

            foreach (var registration in registrations)
            {
                registration.Status = RaceRegistrationStatuses.Cancelled;
                registration.AdminNote = "Tournament cancelled";
                registration.ReviewedAt = now;
            }

            var invitations = await _context.JockeyInvitations
                .Where(i => raceIds.Contains(i.Registration.RaceId)
                    && (i.Status == InvitationStatuses.Pending || i.Status == InvitationStatuses.Accepted))
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

            var refundablePredictions = await _context.RacePredictions
                .Include(p => p.Spectator)
                .Where(p => raceIds.Contains(p.RaceId)
                    && (p.Status == RacePredictionStatuses.Pending || p.Status == RacePredictionStatuses.Locked))
                .ToListAsync();

            foreach (var prediction in refundablePredictions)
            {
                if (prediction.StakePoints > 0)
                {
                    prediction.Spectator.BettingPoints += prediction.StakePoints;
                    prediction.Spectator.UpdatedAt = now;
                }

                prediction.Status = RacePredictionStatuses.Cancelled;
                prediction.RewardStatus = PredictionRewardStatuses.None;
                prediction.PointsAwarded = 0;
                prediction.IsCorrect = null;
                prediction.UpdatedAt = now;

                _context.Notifications.Add(new Notification
                {
                    UserId = prediction.SpectatorId,
                    Title = "Prediction Refunded",
                    Message = $"Race was cancelled. Your {prediction.StakePoints} stake points have been returned.",
                    IsRead = false,
                    CreatedAt = now,
                    ActionType = "SpectatorPredictions",
                    ActionUrl = "/spectator/predictions",
                    RelatedType = "RacePrediction",
                    RelatedId = prediction.PredictionId
                });
            }

            var prizeAwards = await _context.PrizeAwards
                .Where(a => raceIds.Contains(a.RaceId) && a.Status != PrizeAwardStatuses.Paid)
                .ToListAsync();

            foreach (var prizeAward in prizeAwards)
            {
                prizeAward.Status = PrizeAwardStatuses.Rejected;
            }
        }

        private static string MapTournamentStatusToRaceStatus(
            string tournamentStatus,
            string currentRaceStatus)
        {
            return tournamentStatus switch
            {
                TournamentStatuses.Ongoing => currentRaceStatus,
                TournamentStatuses.Completed => currentRaceStatus,
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