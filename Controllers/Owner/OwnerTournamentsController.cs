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
    public async Task<IActionResult> GetNewTournaments()
    {
        var ownerId = GetCurrentUserId();

        if (ownerId == null)
        {
            return InvalidToken();
        }

        var ownerProfileError = await ValidateOwnerProfileAsync(ownerId.Value);

        if (ownerProfileError != null)
        {
            return ownerProfileError;
        }

        var localNow = _dateTimeProvider.GetLocalNow(_dateTimeProvider.TimeZoneId);
        var localToday = DateOnly.FromDateTime(localNow);

        var data = await _context.Tournaments
            .AsNoTracking()
            .Where(t =>
                t.Season.Status == SeasonStatuses.Active &&
                t.Status == TournamentStatuses.OpenRegistration &&
                t.StartDate >= localToday &&
                t.Race != null &&
                t.Race.RaceDate >= localNow &&
                t.Race.RaceRegistrations.Count(r =>
                    r.Status != RaceRegistrationStatuses.Rejected &&
                    r.Status != RaceRegistrationStatuses.Cancelled) < t.Race.MaxHorses &&
                !t.Race.RaceRegistrations.Any(r =>
                    r.OwnerId == ownerId.Value &&
                    r.Status != RaceRegistrationStatuses.Rejected &&
                    r.Status != RaceRegistrationStatuses.Cancelled))
            .OrderBy(t => t.Race!.RaceDate)
            .Select(t => new
            {
                t.TournamentId,
                t.TournamentName,

                SeasonId = t.SeasonId,
                SeasonName = t.Season.SeasonName,
                SeasonStatus = t.Season.Status,
                RegistrationDeadline = t.StartDate,

                t.ImageUrl,
                RaceId = t.Race!.RaceId,
                RaceStatus = t.Race.Status,
                RaceDate = t.Race.RaceDate,
                Location = t.Race.Location ?? t.Location
            })
            .ToListAsync();

        var response = data
            .Where(t => RaceStatuses.CanRegister(t.RaceStatus))
            .Take(5)
            .Select(t => new OwnerNewTournamentResponse
            {
                TournamentId = t.TournamentId,
                TournamentName = t.TournamentName,

                SeasonId = t.SeasonId,
                SeasonName = t.SeasonName,
                SeasonStatus = t.SeasonStatus,
                RegistrationDeadline = t.RegistrationDeadline.ToString("yyyy-MM-dd"),

                RaceId = t.RaceId,
                RaceDate = t.RaceDate.ToString("yyyy-MM-dd"),
                Location = t.Location
            });

        return Ok(response);
    }
}