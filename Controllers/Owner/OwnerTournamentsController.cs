using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Owner;
using Eliteracingleague.API.Services.SystemTime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Controllers.Owner;

[Route("api/owner/tournaments")]
[ApiController]
[Authorize(Roles = UserRoles.HorseOwner)]
public class OwnerTournamentsController : OwnerBaseController
{
    private readonly IDateTimeProvider _dateTimeProvider;

    public OwnerTournamentsController(
        EliteRacingLeagueContext context,
        IDateTimeProvider dateTimeProvider) : base(context)
    {
        _dateTimeProvider = dateTimeProvider;
    }

    [HttpGet("new")]
    public async Task<IActionResult> GetNewTournaments(CancellationToken cancellationToken)
    {
        var ownerId = GetCurrentUserId();
        if (ownerId == null) return InvalidToken();

        var ownerProfileError = await ValidateOwnerProfileAsync(ownerId.Value);
        if (ownerProfileError != null) return ownerProfileError;

        var localNow = _dateTimeProvider.GetLocalNow(_dateTimeProvider.TimeZoneId);
        var localToday = DateOnly.FromDateTime(localNow);

        var races = await _context.Races
            .AsNoTracking()
            .Where(r =>
                r.Tournament.Season.Status == SeasonStatuses.Active &&
                r.Tournament.Status == TournamentStatuses.OpenRegistration &&
                r.Tournament.StartDate >= localToday &&
                r.RaceDate >= localNow &&
                RaceStatuses.RegisterableStatuses.Contains(r.Status) &&
                r.RaceRegistrations.Count(registration =>
                    registration.Status != RaceRegistrationStatuses.Rejected &&
                    registration.Status != RaceRegistrationStatuses.Cancelled &&
                    registration.Status != RaceRegistrationStatuses.Withdrawn) < r.MaxHorses &&
                !r.RaceRegistrations.Any(registration =>
                    registration.OwnerId == ownerId.Value &&
                    registration.Status != RaceRegistrationStatuses.Rejected &&
                    registration.Status != RaceRegistrationStatuses.Cancelled &&
                    registration.Status != RaceRegistrationStatuses.Withdrawn))
            .OrderBy(r => r.RaceDate)
            .Take(5)
            .Select(r => new OwnerNewTournamentResponse
            {
                TournamentId = r.TournamentId,
                TournamentName = r.Tournament.TournamentName,
                SeasonId = r.Tournament.SeasonId,
                SeasonName = r.Tournament.Season.SeasonName,
                SeasonStatus = r.Tournament.Season.Status,
                RegistrationDeadline = r.Tournament.StartDate.ToString("yyyy-MM-dd"),
                RaceId = r.RaceId,
                RaceDate = r.RaceDate.ToString("yyyy-MM-dd"),
                Location = r.Location ?? r.Tournament.Location
            })
            .ToListAsync(cancellationToken);

        return Ok(races);
    }
}
