using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Public;
using Eliteracingleague.API.Services.SystemTime;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Services;

public class PublicHomeService
{
    private readonly EliteRacingLeagueContext _context;
    private readonly IDateTimeProvider _dateTimeProvider;

    public PublicHomeService(
        EliteRacingLeagueContext context,
        IDateTimeProvider dateTimeProvider)
    {
        _context = context;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<HomePageResponse> GetHomePageAsync(
        CancellationToken cancellationToken = default)
    {
        var currentSeason = await _context.Seasons
            .AsNoTracking()
            .Where(item => item.Status == SeasonStatuses.Active)
            .OrderByDescending(item => item.StartDate)
            .Select(item => new PublicSeasonResponse
            {
                SeasonId = item.SeasonId,
                SeasonName = item.SeasonName,
                StartDate = item.StartDate,
                EndDate = item.EndDate,
                Status = item.Status
            })
            .FirstOrDefaultAsync(cancellationToken);

        var upcomingTournaments = await GetUpcomingTournamentsAsync(3, cancellationToken);

        var latestRace = await _context.Races
            .AsNoTracking()
            .Where(item =>
                item.Status == RaceStatuses.Published &&
                item.Tournament.Status != TournamentStatuses.Cancelled &&
                item.RaceResults.Any(result =>
                    result.Status == RaceResultStatuses.Published &&
                    result.OutcomeStatus == RaceOutcomeStatuses.Finished &&
                    result.FinishPosition.HasValue))
            .Select(item => new
            {
                item.RaceId,
                item.RaceName,
                item.TournamentId,
                item.Tournament.TournamentName,
                PublishedAt = item.RaceResults
                    .Where(result => result.Status == RaceResultStatuses.Published)
                    .Max(result => (DateTime?)result.PublishedAt)
            })
            .OrderByDescending(item => item.PublishedAt)
            .ThenByDescending(item => item.RaceId)
            .FirstOrDefaultAsync(cancellationToken);

        PublicLatestResultResponse? latestResult = null;

        if (latestRace != null)
        {
            var standings = await _context.RaceResults
                .AsNoTracking()
                .Where(item =>
                    item.RaceId == latestRace.RaceId &&
                    item.Status == RaceResultStatuses.Published &&
                    item.OutcomeStatus == RaceOutcomeStatuses.Finished &&
                    item.FinishPosition.HasValue &&
                    !_context.RaceViolations.Any(violation =>
                        violation.RaceId == item.RaceId &&
                        violation.RegistrationId == item.RegistrationId &&
                        violation.Action == RaceViolationActions.Disqualified))
                .OrderBy(item => item.FinishPosition)
                .ThenBy(item => item.FinishTimeSeconds)
                .ThenBy(item => item.ResultId)
                .Take(3)
                .Select(item => new PublicStandingResponse
                {
                    Position = item.FinishPosition!.Value,
                    HorseId = item.Registration.HorseId,
                    HorseName = item.Registration.Horse.HorseName,
                    HorseImageUrl = item.Registration.Horse.ImageUrl,
                    JockeyName = item.Registration.Jockey == null
                        ? null
                        : item.Registration.Jockey.JockeyNavigation.FullName,
                    OwnerName = item.Registration.Horse.Owner.Owner.FullName,
                    FinishTimeSeconds = item.FinishTimeSeconds
                })
                .ToListAsync(cancellationToken);

            latestResult = new PublicLatestResultResponse
            {
                TournamentId = latestRace.TournamentId,
                TournamentName = latestRace.TournamentName,
                RaceId = latestRace.RaceId,
                RaceName = latestRace.RaceName,
                PublishedAt = latestRace.PublishedAt,
                Standings = standings
            };
        }

        return new HomePageResponse
        {
            CurrentSeason = currentSeason,
            UpcomingTournaments = upcomingTournaments,
            LatestResult = latestResult
        };
    }
    public async Task<List<PublicTournamentResponse>> GetUpcomingTournamentsAsync(
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 200);
        var localNow = _dateTimeProvider.GetLocalNow(_dateTimeProvider.TimeZoneId);

        var candidateRaces = await _context.Races
            .AsNoTracking()
            .Where(race =>
                race.Tournament.Status != TournamentStatuses.Draft &&
                race.Tournament.Status != TournamentStatuses.Cancelled &&
                race.Tournament.Status != TournamentStatuses.Completed &&
                race.Status != RaceStatuses.Cancelled &&
                race.RaceDate >= localNow)
            .OrderBy(race => race.RaceDate)
            .ThenByDescending(race => race.Tournament.PrizePool)
            .Select(race => new
            {
                race.RaceId,
                race.RaceName,
                race.RaceDate,
                race.DistanceMeters,
                RaceLocation = race.Location,
                RaceStatus = race.Status,
                race.TournamentId,
                race.Tournament.TournamentName,
                race.Tournament.Description,
                TournamentLocation = race.Tournament.Location,
                race.Tournament.StartDate,
                race.Tournament.EndDate,
                race.Tournament.PrizePool,
                race.Tournament.ImageUrl,
                TournamentStatus = race.Tournament.Status,
                race.Tournament.SeasonId,
                race.Tournament.Season.SeasonName,
                RegisteredHorseCount = race.RaceRegistrations.Count(registration =>
                    registration.Status != RaceRegistrationStatuses.Rejected &&
                    registration.Status != RaceRegistrationStatuses.Cancelled &&
                    registration.Status != RaceRegistrationStatuses.Withdrawn)
            })
            .ToListAsync(cancellationToken);

        return candidateRaces
            .GroupBy(item => item.TournamentId)
            .Select(group => group.OrderBy(item => item.RaceDate).First())
            .OrderBy(item => item.RaceDate)
            .ThenByDescending(item => item.PrizePool)
            .Take(safeLimit)
            .Select(item => new PublicTournamentResponse
            {
                TournamentId = item.TournamentId,
                TournamentName = item.TournamentName,
                Description = item.Description,
                Location = item.TournamentLocation,
                StartDate = item.StartDate,
                EndDate = item.EndDate,
                PrizePool = item.PrizePool,
                ImageUrl = item.ImageUrl,
                Status = item.TournamentStatus,
                SeasonId = item.SeasonId,
                SeasonName = item.SeasonName,
                RegisteredHorseCount = item.RegisteredHorseCount,
                Race = new PublicRaceResponse
                {
                    RaceId = item.RaceId,
                    RaceName = item.RaceName,
                    RaceDate = item.RaceDate,
                    DistanceMeters = item.DistanceMeters,
                    Location = item.RaceLocation,
                    Status = item.RaceStatus
                }
            })
            .ToList();
    }

}
