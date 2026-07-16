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
        private const decimal MaxPrizePool = 1_000_000_000m;
        private const int MaxRulesLength = 10_000;

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

            var normalizedTournamentName = request.TournamentName.Trim();
            var duplicateNameExists = await _context.Tournaments
                .AsNoTracking()
                .AnyAsync(t =>
                    t.SeasonId == season.SeasonId &&
                    t.Status != TournamentStatuses.Cancelled &&
                    t.TournamentName == normalizedTournamentName);

            if (duplicateNameExists)
            {
                return Conflict(new
                {
                    message = "A tournament with the same name already exists in this season.",
                    seasonId = season.SeasonId,
                    seasonName = season.SeasonName
                });
            }

            if (request.RefereeId.HasValue)
            {
                var refereeIsEligible = await _context.RaceReferees
                    .AsNoTracking()
                    .AnyAsync(r =>
                        r.RefereeId == request.RefereeId.Value &&
                        r.IsActive &&
                        r.Referee.Role == UserRoles.RaceReferee &&
                        r.Referee.Status == UserStatuses.Active);

                if (!refereeIsEligible)
                {
                    return BadRequest(new
                    {
                        message = "Selected referee does not exist or is not active.",
                        refereeId = request.RefereeId.Value
                    });
                }

                var hasScheduleConflict = await _context.RefereeAssignments
                    .AsNoTracking()
                    .AnyAsync(a =>
                        a.RefereeId == request.RefereeId.Value &&
                        a.Status == RefereeAssignmentStatuses.Assigned &&
                        a.Race.RaceDate == raceDateTime &&
                        a.Race.Status != RaceStatuses.Cancelled &&
                        a.Race.Status != RaceStatuses.Published &&
                        a.Race.Tournament.Status != TournamentStatuses.Cancelled &&
                        a.Race.Tournament.Status != TournamentStatuses.Completed);

                if (hasScheduleConflict)
                {
                    return BadRequest(new
                    {
                        message = "Selected referee is already assigned to another race at the same time.",
                        refereeId = request.RefereeId.Value,
                        raceDateTime
                    });
                }
            }

            string? imageUrl;

            try
            {
                imageUrl = await SaveTournamentImageAsync(
                    request.TournamentImage);
                imageUrl ??= string.IsNullOrWhiteSpace(request.ImageUrl)
                    ? null
                    : request.ImageUrl.Trim();
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
                    TournamentName = normalizedTournamentName,
                    Description = string.IsNullOrWhiteSpace(request.Description)
                        ? null
                        : request.Description.Trim(),
                    Location = request.Location!.Trim(),
                    StartDate = request.RegistrationDeadline,
                    EndDate = request.RaceDate,
                    MaxHorses = request.MaxHorses,
                    PrizePool = request.PrizePool,
                    ImageUrl = imageUrl,
                    Status = TournamentStatuses.Draft,
                    Rules = string.IsNullOrWhiteSpace(request.Rules)
                        ? null
                        : request.Rules.Trim(),
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

                if (request.RefereeId.HasValue)
                {
                    _context.RefereeAssignments.Add(new RefereeAssignment
                    {
                        RaceId = race.RaceId,
                        RefereeId = request.RefereeId.Value,
                        AssignedBy = adminId,
                        Status = RefereeAssignmentStatuses.Assigned,
                        AssignedAt = now
                    });

                    race.Status = RaceStatuses.AssignedReferee;
                    race.UpdatedAt = now;

                    await _notificationService.CreateForUserAsync(
                        request.RefereeId.Value,
                        "Race Assigned",
                        $"You have been assigned to referee {race.RaceName} in tournament {tournament.TournamentName}.",
                        "RefereeRaceAssignment",
                        "/referee/races",
                        "Race",
                        race.RaceId);

                    await _context.SaveChangesAsync();
                }

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
                    imageUrl = tournament.ImageUrl,
                    refereeId = request.RefereeId,
                    raceStatus = race.Status
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

            var normalizedTournamentName = request.TournamentName.Trim();
            var duplicateNameExists = await _context.Tournaments
                .AsNoTracking()
                .AnyAsync(t =>
                    t.TournamentId != tournament.TournamentId &&
                    t.SeasonId == matchingSeason.SeasonId &&
                    t.Status != TournamentStatuses.Cancelled &&
                    t.TournamentName == normalizedTournamentName);

            if (duplicateNameExists)
            {
                return Conflict(new
                {
                    message = "A tournament with the same name already exists in this season.",
                    tournamentId = tournament.TournamentId,
                    seasonId = matchingSeason.SeasonId,
                    seasonName = matchingSeason.SeasonName
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
            else if (request.ImageUrl != null)
            {
                tournament.ImageUrl = string.IsNullOrWhiteSpace(request.ImageUrl)
                    ? null
                    : request.ImageUrl.Trim();
            }

            var now = _dateTimeProvider.UtcNow;

            tournament.TournamentName = normalizedTournamentName;
            tournament.Description = string.IsNullOrWhiteSpace(request.Description)
                ? null
                : request.Description.Trim();
            tournament.Location = request.Location!.Trim();
            tournament.StartDate = request.RegistrationDeadline;
            tournament.EndDate = request.RaceDate;
            tournament.MaxHorses = request.MaxHorses;
            tournament.PrizePool = request.PrizePool;
            tournament.Rules = string.IsNullOrWhiteSpace(request.Rules)
                ? null
                : request.Rules.Trim();
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

        [HttpPut("{id:int}/assign-referee")]
        public async Task<IActionResult> AssignReferee(
            int id,
            [FromBody] AssignRefereeRequest request)
        {
            var adminIdText = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(adminIdText, out var adminId))
            {
                return Unauthorized(new
                {
                    message = "Invalid admin token"
                });
            }

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

            if (race.Tournament.Status is
                    TournamentStatuses.Completed or
                    TournamentStatuses.Cancelled ||
                race.Status is
                    RaceStatuses.Ongoing or
                    RaceStatuses.Finished or
                    RaceStatuses.ResultPending or
                    RaceStatuses.Published or
                    RaceStatuses.Cancelled)
            {
                return BadRequest(new
                {
                    message = "Referee cannot be changed after the race has started, finished, or been cancelled.",
                    tournamentStatus = race.Tournament.Status,
                    raceStatus = race.Status
                });
            }

            var referee = await _context.RaceReferees
                .Include(r => r.Referee)
                .FirstOrDefaultAsync(r =>
                    r.RefereeId == request.RefereeId &&
                    r.Referee.Role == UserRoles.RaceReferee);

            if (referee == null)
            {
                return BadRequest(new
                {
                    message = "Referee not found"
                });
            }

            if (!referee.IsActive ||
                referee.Referee.Status != UserStatuses.Active)
            {
                return BadRequest(new
                {
                    message = "Only an active referee account can be assigned.",
                    refereeId = referee.RefereeId,
                    userStatus = referee.Referee.Status,
                    refereeIsActive = referee.IsActive
                });
            }

            var existing = await _context.RefereeAssignments
                .FirstOrDefaultAsync(x => x.RaceId == race.RaceId);

            if (existing != null &&
                existing.RefereeId == request.RefereeId &&
                existing.Status == RefereeAssignmentStatuses.Assigned)
            {
                return Ok(new
                {
                    message = "This referee is already assigned to the race.",
                    refereeId = request.RefereeId,
                    raceId = race.RaceId
                });
            }

            var hasScheduleConflict = await _context.RefereeAssignments
                .AsNoTracking()
                .AnyAsync(a =>
                    a.RaceId != race.RaceId &&
                    a.RefereeId == request.RefereeId &&
                    a.Status == RefereeAssignmentStatuses.Assigned &&
                    a.Race.RaceDate == race.RaceDate &&
                    a.Race.Status != RaceStatuses.Cancelled &&
                    a.Race.Status != RaceStatuses.Published &&
                    a.Race.Tournament.Status != TournamentStatuses.Cancelled &&
                    a.Race.Tournament.Status != TournamentStatuses.Completed);

            if (hasScheduleConflict)
            {
                return BadRequest(new
                {
                    message = "Selected referee is already assigned to another race at the same time.",
                    refereeId = request.RefereeId,
                    raceDateTime = race.RaceDate
                });
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                var now = _dateTimeProvider.UtcNow;

                if (existing != null)
                {
                    existing.RefereeId = request.RefereeId;
                    existing.AssignedBy = adminId;
                    existing.Status = RefereeAssignmentStatuses.Assigned;
                    existing.AssignedAt = now;
                }
                else
                {
                    _context.RefereeAssignments.Add(new RefereeAssignment
                    {
                        RaceId = race.RaceId,
                        RefereeId = request.RefereeId,
                        AssignedBy = adminId,
                        Status = RefereeAssignmentStatuses.Assigned,
                        AssignedAt = now
                    });
                }

                if (race.Status == RaceStatuses.Scheduled)
                {
                    race.Status = RaceStatuses.AssignedReferee;
                }

                race.UpdatedAt = now;

                await _notificationService.CreateForUserAsync(
                    request.RefereeId,
                    "Race Assigned",
                    $"You have been assigned to referee {race.RaceName} in tournament {race.Tournament.TournamentName}.",
                    "RefereeRaceAssignment",
                    "/referee/races",
                    "Race",
                    race.RaceId);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new
                {
                    message = "Referee assigned successfully",
                    refereeId = request.RefereeId,
                    raceId = race.RaceId,
                    raceStatus = race.Status
                });
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
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
            var tournamentName = request.TournamentName?.Trim() ?? string.Empty;
            if (tournamentName.Length is < 3 or > 200)
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "Tournament name must contain between 3 and 200 characters",
                    Id = id
                });
            }

            var location = request.Location?.Trim() ?? string.Empty;
            if (location.Length is < 3 or > 255)
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "Location must contain between 3 and 255 characters",
                    Id = id
                });
            }

            if (!string.IsNullOrWhiteSpace(request.Description) &&
                request.Description.Trim().Length > 1000)
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "Description cannot exceed 1,000 characters",
                    Id = id
                });
            }

            if (!string.IsNullOrWhiteSpace(request.Rules) &&
                request.Rules.Trim().Length > MaxRulesLength)
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = $"Rules cannot exceed {MaxRulesLength:N0} characters",
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

            if (request.RaceDate.Year is < 2000 or > 2100)
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "Race date year must be between 2000 and 2100",
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

            if (request.RegistrationDeadline.Year is < 2000 or > 2100)
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "Registration deadline year must be between 2000 and 2100",
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

            if (request.MaxHorses is < 2 or > 20)
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "Max horses must be between 2 and 20",
                    Id = id
                });
            }

            if (request.DistanceMeters is not (1000 or 1500 or 2400))
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = "Distance must be 1000, 1500, or 2400 meters",
                    Id = id
                });
            }

            if (request.PrizePool is < 0 or > MaxPrizePool)
            {
                return BadRequest(new AdminActionResponse
                {
                    Message = $"Prize pool must be between 0 and {MaxPrizePool:N0}",
                    Id = id
                });
            }

            if (!string.IsNullOrWhiteSpace(request.ImageUrl))
            {
                var imageUrl = request.ImageUrl.Trim();
                if (imageUrl.Length > 500 || !IsAllowedImageUrl(imageUrl))
                {
                    return BadRequest(new AdminActionResponse
                    {
                        Message = "Image URL must be a relative path or an HTTP/HTTPS URL with at most 500 characters",
                        Id = id
                    });
                }
            }

            return null;
        }

        private static bool IsAllowedImageUrl(string imageUrl)
        {
            if (imageUrl.StartsWith("/", StringComparison.Ordinal) &&
                !imageUrl.StartsWith("//", StringComparison.Ordinal))
            {
                return true;
            }

            return Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri) &&
                   (uri.Scheme == Uri.UriSchemeHttp ||
                    uri.Scheme == Uri.UriSchemeHttps);
        }

        private async Task<string?> SaveTournamentImageAsync(IFormFile? file)
        {
            if (file == null || file.Length == 0)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(file.FileName))
            {
                throw new InvalidOperationException("Tournament image file name is invalid.");
            }

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var allowedContentTypes = new[]
            {
                "image/jpeg",
                "image/png",
                "image/webp"
            };

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var contentType = (file.ContentType ?? string.Empty).Trim().ToLowerInvariant();

            if (!allowedExtensions.Contains(extension) ||
                !allowedContentTypes.Contains(contentType))
            {
                throw new InvalidOperationException(
                    "Only valid JPG, JPEG, PNG, and WEBP images are allowed.");
            }

            if (file.Length > 5 * 1024 * 1024)
            {
                throw new InvalidOperationException("Tournament image must be 5MB or smaller.");
            }

            if (!await HasValidImageSignatureAsync(file, extension))
            {
                throw new InvalidOperationException(
                    "The uploaded file content does not match a valid JPG, PNG, or WEBP image.");
            }

            var webRootPath = _env.WebRootPath;

            if (string.IsNullOrWhiteSpace(webRootPath))
            {
                webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            }

            var uploadFolder = Path.Combine(webRootPath, "uploads", "tournaments");
            Directory.CreateDirectory(uploadFolder);

            var fileName = $"{Guid.NewGuid():N}{extension}";
            var fullPath = Path.Combine(uploadFolder, fileName);

            await using var stream = System.IO.File.Create(fullPath);
            await file.CopyToAsync(stream);

            return $"/uploads/tournaments/{fileName}";
        }

        private static async Task<bool> HasValidImageSignatureAsync(
            IFormFile file,
            string extension)
        {
            await using var stream = file.OpenReadStream();
            var header = new byte[12];
            var bytesRead = await stream.ReadAsync(header.AsMemory(0, header.Length));

            return extension switch
            {
                ".jpg" or ".jpeg" =>
                    bytesRead >= 3 &&
                    header[0] == 0xFF &&
                    header[1] == 0xD8 &&
                    header[2] == 0xFF,

                ".png" =>
                    bytesRead >= 8 &&
                    header[0] == 0x89 &&
                    header[1] == 0x50 &&
                    header[2] == 0x4E &&
                    header[3] == 0x47 &&
                    header[4] == 0x0D &&
                    header[5] == 0x0A &&
                    header[6] == 0x1A &&
                    header[7] == 0x0A,

                ".webp" =>
                    bytesRead >= 12 &&
                    header[0] == 0x52 &&
                    header[1] == 0x49 &&
                    header[2] == 0x46 &&
                    header[3] == 0x46 &&
                    header[8] == 0x57 &&
                    header[9] == 0x45 &&
                    header[10] == 0x42 &&
                    header[11] == 0x50,

                _ => false
            };
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
