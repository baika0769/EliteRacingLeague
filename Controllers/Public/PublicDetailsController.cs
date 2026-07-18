using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Public;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Controllers.Public;

[ApiController]
[Route("api/public")]
[AllowAnonymous]
public class PublicDetailsController : ControllerBase
{
    private readonly EliteRacingLeagueContext _context;

    public PublicDetailsController(EliteRacingLeagueContext context)
    {
        _context = context;
    }

    [HttpGet("tournaments/{id:int}")]
    public async Task<IActionResult> Tournament(int id, CancellationToken cancellationToken)
    {
        var tournament = await _context.Tournaments.AsNoTracking()
            .Where(t => t.TournamentId == id && t.Status != TournamentStatuses.Draft)
            .Select(t => new PublicTournamentDetailResponse
            {
                TournamentId = t.TournamentId,
                TournamentName = t.TournamentName,
                Description = t.Description,
                Location = t.Location,
                Status = t.Status,
                SeasonId = t.SeasonId,
                SeasonName = t.Season.SeasonName,
                StartDate = t.StartDate,
                EndDate = t.EndDate,
                PrizePool = t.PrizePool,
                ImageUrl = t.ImageUrl,
                Races = t.Races.OrderBy(r => r.RaceDate).Select(r => new PublicRaceSummaryResponse
                {
                    RaceId = r.RaceId,
                    RaceName = r.RaceName,
                    RaceDate = r.RaceDate,
                    DistanceMeters = r.DistanceMeters,
                    Location = r.Location,
                    Status = r.Status,
                    RegisteredCount = r.RaceRegistrations.Count(x =>
                        x.Status != RaceRegistrationStatuses.Rejected &&
                        x.Status != RaceRegistrationStatuses.Cancelled &&
                        x.Status != RaceRegistrationStatuses.Withdrawn)
                }).ToList(),
                Standings = t.TournamentStandings.OrderBy(s => s.FinalRank).Select(s => new PublicTournamentStandingResponse
                {
                    FinalRank = s.FinalRank,
                    HorseId = s.HorseId,
                    HorseName = s.Horse.HorseName,
                    OwnerName = s.Owner.Owner.FullName,
                    JockeyName = s.Jockey == null ? null : s.Jockey.JockeyNavigation.FullName,
                    TotalPoints = s.TotalPoints,
                    Wins = s.Wins,
                    CompletedRaces = s.CompletedRaces
                }).ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);
        return tournament == null ? NotFound(new { message = "Tournament not found." }) : Ok(tournament);
    }

    [HttpGet("races/{id:int}")]
    public async Task<IActionResult> Race(int id, CancellationToken cancellationToken)
    {
        var race = await _context.Races.AsNoTracking()
            .Where(r => r.RaceId == id && r.Tournament.Status != TournamentStatuses.Draft)
            .Select(r => new PublicRaceDetailResponse
            {
                RaceId = r.RaceId,
                RaceName = r.RaceName,
                RaceDate = r.RaceDate,
                DistanceMeters = r.DistanceMeters,
                Location = r.Location,
                Status = r.Status,
                TournamentName = r.Tournament.TournamentName,
                PredictionDeadline = r.PredictionDeadline,
                RegisteredCount = r.RaceRegistrations.Count(x =>
                    x.Status != RaceRegistrationStatuses.Rejected &&
                    x.Status != RaceRegistrationStatuses.Cancelled &&
                    x.Status != RaceRegistrationStatuses.Withdrawn),
                Participants = r.RaceRegistrations
                    .Where(x => x.Status != RaceRegistrationStatuses.Rejected &&
                                x.Status != RaceRegistrationStatuses.Cancelled &&
                                x.Status != RaceRegistrationStatuses.Withdrawn)
                    .OrderBy(x => x.Horse.HorseName)
                    .Select(x => new PublicParticipantResponse
                    {
                        RegistrationId = x.RegistrationId,
                        HorseId = x.HorseId,
                        HorseName = x.Horse.HorseName,
                        HorseImageUrl = x.Horse.ImageUrl,
                        OwnerName = x.Owner.Owner.FullName,
                        JockeyName = x.Jockey == null ? null : x.Jockey.JockeyNavigation.FullName,
                        RegistrationStatus = x.Status
                    }).ToList(),
                Results = r.RaceResults
                    .Where(x => x.Status == RaceResultStatuses.Published)
                    .OrderBy(x => x.FinishPosition)
                    .ThenBy(x => x.ResultId)
                    .Select(x => new PublicRaceResultResponse
                    {
                        RegistrationId = x.RegistrationId,
                        HorseName = x.Registration.Horse.HorseName,
                        JockeyName = x.Registration.Jockey == null ? null : x.Registration.Jockey.JockeyNavigation.FullName,
                        FinishPosition = x.FinishPosition,
                        FinishTimeSeconds = x.FinishTimeSeconds,
                        OutcomeStatus = x.OutcomeStatus
                    }).ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);
        return race == null ? NotFound(new { message = "Race not found." }) : Ok(race);
    }

    [HttpGet("horses/{id:int}")]
    public async Task<IActionResult> Horse(int id, CancellationToken cancellationToken)
    {
        var horse = await _context.Horses.AsNoTracking()
            .Where(h => h.HorseId == id && h.IsActive)
            .Select(h => new
            {
                h.HorseId, h.HorseName, h.ImageUrl, h.Age, h.WeightKg, h.HeightCm,
                h.HealthStatus, h.AchievementSummary,
                BreedName = h.Breed.BreedName,
                OwnerId = h.OwnerId,
                OwnerName = h.Owner.Owner.FullName,
                PublishedResults = h.RaceRegistrations.Count(r => r.RaceResult != null && r.RaceResult.Status == RaceResultStatuses.Published),
                Wins = h.RaceRegistrations.Count(r => r.RaceResult != null && r.RaceResult.Status == RaceResultStatuses.Published && r.RaceResult.FinishPosition == 1)
            }).FirstOrDefaultAsync(cancellationToken);
        return horse == null ? NotFound(new { message = "Horse not found." }) : Ok(horse);
    }

    [HttpGet("jockeys/{id:int}")]
    public async Task<IActionResult> Jockey(int id, CancellationToken cancellationToken)
    {
        var jockey = await _context.Jockeys.AsNoTracking()
            .Where(j => j.JockeyId == id)
            .Select(j => new
            {
                j.JockeyId,
                FullName = j.JockeyNavigation.FullName,
                j.YearsOfExperience,
                j.HealthStatus,
                j.ProfileImageUrl,
                IsActive = j.IsActive && j.JockeyNavigation.Status == UserStatuses.Active,
                PublishedRaces = j.RaceRegistrations.Count(r => r.RaceResult != null && r.RaceResult.Status == RaceResultStatuses.Published),
                Wins = j.RaceRegistrations.Count(r => r.RaceResult != null && r.RaceResult.Status == RaceResultStatuses.Published && r.RaceResult.FinishPosition == 1)
            }).FirstOrDefaultAsync(cancellationToken);
        return jockey == null ? NotFound(new { message = "Jockey not found." }) : Ok(jockey);
    }

    [HttpGet("owners/{id:int}")]
    public async Task<IActionResult> Owner(int id, CancellationToken cancellationToken)
    {
        var owner = await _context.HorseOwners.AsNoTracking()
            .Where(o => o.OwnerId == id)
            .Select(o => new
            {
                o.OwnerId,
                FullName = o.Owner.FullName,
                ActiveHorses = o.Horses.Count(h => h.IsActive),
                PublishedRaces = o.RaceRegistrations.Count(r => r.RaceResult != null && r.RaceResult.Status == RaceResultStatuses.Published),
                Wins = o.RaceRegistrations.Count(r => r.RaceResult != null && r.RaceResult.Status == RaceResultStatuses.Published && r.RaceResult.FinishPosition == 1)
            }).FirstOrDefaultAsync(cancellationToken);
        return owner == null ? NotFound(new { message = "Owner not found." }) : Ok(owner);
    }
}
