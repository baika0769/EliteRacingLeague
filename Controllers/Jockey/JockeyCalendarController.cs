using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Jockey.Calendar;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Claims;

namespace Eliteracingleague.API.Controllers.Jockey;

[Route("api/jockey/calendar")]
[ApiController]
[Authorize(Roles = UserRoles.Jockey)]
public class JockeyCalendarController : ControllerBase
{
    private static readonly string[] CalendarRegistrationStatuses =
    {
        RaceRegistrationStatuses.ReadyToRace,
        RaceRegistrationStatuses.Completed
    };

    private readonly EliteRacingLeagueContext _context;

    public JockeyCalendarController(EliteRacingLeagueContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetCalendar([FromQuery] string? month)
    {
        var jockeyId = GetCurrentJockeyId();

        if (jockeyId == null)
        {
            return InvalidToken();
        }

        var profileError = await LoadActiveJockeyProfileAsync(jockeyId.Value);

        if (profileError != null)
        {
            return profileError;
        }

        if (!TryResolveMonth(month, out var startDate, out var monthText))
        {
            return BadRequest(new { message = "Month không hợp lệ. Định dạng hợp lệ: yyyy-MM." });
        }

        var daysInMonth = DateTime.DaysInMonth(startDate.Year, startDate.Month);
        var endDate = startDate.AddDays(daysInMonth);
        var startDateTime = startDate.ToDateTime(TimeOnly.MinValue);
        var endDateTime = endDate.ToDateTime(TimeOnly.MinValue);

        var unavailableDates = await _context.JockeyAvailabilities
            .AsNoTracking()
            .Where(a => a.JockeyId == jockeyId.Value
                && a.AvailableDate >= startDate
                && a.AvailableDate < endDate
                && a.Status == JockeyAvailabilityStatuses.Unavailable)
            .Select(a => a.AvailableDate)
            .ToListAsync();

        var raceItems = await _context.RaceRegistrations
            .AsNoTracking()
            .Where(r => r.JockeyId == jockeyId.Value
                && CalendarRegistrationStatuses.Contains(r.Status)
                && r.Race.RaceDate >= startDateTime
                && r.Race.RaceDate < endDateTime
                && r.Race.Status != RaceStatuses.Cancelled)
            .Select(r => new
            {
                r.RaceId,
                r.Race.RaceName,
                r.Race.RaceDate,
                r.Race.Location,
                r.Horse.HorseName,
                RegistrationStatus = r.Status
            })
            .ToListAsync();

        var raceItemsByDate = raceItems
            .GroupBy(r => DateOnly.FromDateTime(r.RaceDate))
            .ToDictionary(
                g => g.Key,
                g => g.Select(r => new JockeyCalendarRaceResponse
                {
                    RaceId = r.RaceId,
                    RaceName = r.RaceName,
                    RaceDate = r.RaceDate,
                    Location = r.Location,
                    HorseName = r.HorseName,
                    Status = r.RegistrationStatus
                }).ToList());

        var racingDates = raceItemsByDate.Keys.ToHashSet();
        var unavailableDateSet = unavailableDates.ToHashSet();
        var unavailableWithoutRace = unavailableDateSet.Count(d => !racingDates.Contains(d));

        var days = Enumerable.Range(0, daysInMonth)
            .Select(offset =>
            {
                var date = startDate.AddDays(offset);
                raceItemsByDate.TryGetValue(date, out var races);

                races ??= new List<JockeyCalendarRaceResponse>();

                var status = races.Count > 0
                    ? JockeyAvailabilityStatuses.RacingDay
                    : unavailableDateSet.Contains(date)
                        ? JockeyAvailabilityStatuses.Unavailable
                        : JockeyAvailabilityStatuses.Available;

                return new JockeyCalendarDayResponse
                {
                    Date = date,
                    DayNumber = date.Day,
                    Status = status,
                    IsCurrentMonth = true,
                    Races = races
                };
            })
            .ToList();

        var now = DateTime.UtcNow;
        var nextRaces = await _context.RaceRegistrations
            .AsNoTracking()
            .Where(r => r.JockeyId == jockeyId.Value
                && r.Status == RaceRegistrationStatuses.ReadyToRace
                && r.Race.RaceDate >= now
                && r.Race.Status != RaceStatuses.Cancelled)
            .OrderBy(r => r.Race.RaceDate)
            .Take(2)
            .Select(r => new JockeyNextRaceResponse
            {
                RaceId = r.RaceId,
                RaceName = r.Race.RaceName,
                RaceDate = r.Race.RaceDate,
                Location = r.Race.Location,
                HorseName = r.Horse.HorseName,
                PrizeText = null
            })
            .ToListAsync();

        return Ok(new JockeyCalendarResponse
        {
            Month = monthText,
            AvailableDays = daysInMonth - racingDates.Count - unavailableWithoutRace,
            RacingDays = racingDates.Count,
            Days = days,
            NextRaces = nextRaces
        });
    }

    private int? GetCurrentJockeyId()
    {
        var jockeyIdText = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return int.TryParse(jockeyIdText, out var jockeyId) ? jockeyId : null;
    }

    private IActionResult InvalidToken()
    {
        return Unauthorized(new { message = "Token không hợp lệ." });
    }

    private async Task<IActionResult?> LoadActiveJockeyProfileAsync(int jockeyId)
    {
        var data = await _context.Jockeys
            .AsNoTracking()
            .Where(j => j.JockeyId == jockeyId)
            .Select(j => new
            {
                j.IsActive,
                j.JockeyNavigation.Role,
                UserStatus = j.JockeyNavigation.Status,
                j.JockeyNavigation.EmailVerified
            })
            .FirstOrDefaultAsync();

        if (data == null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Không tìm thấy hồ sơ jockey."
            });
        }

        return ValidateActiveJockey(data.Role, data.UserStatus, data.EmailVerified, data.IsActive);
    }

    private IActionResult? ValidateActiveJockey(
        string role,
        string userStatus,
        bool emailVerified,
        bool isActive)
    {
        if (role != UserRoles.Jockey)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Tài khoản không có quyền Jockey."
            });
        }

        if (!emailVerified)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Email chưa được xác thực.",
                nextStep = AuthNextSteps.VerifyEmail
            });
        }

        if (userStatus == UserStatuses.Banned)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Tài khoản đã bị khóa.",
                nextStep = AuthNextSteps.AccountBlocked
            });
        }

        if (userStatus == UserStatuses.Inactive)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Tài khoản đang bị vô hiệu hóa.",
                nextStep = AuthNextSteps.ContactSupport
            });
        }

        if (userStatus != UserStatuses.Active || !isActive)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Tài khoản jockey chưa được kích hoạt.",
                status = userStatus,
                isActive,
                nextStep = AuthNextSteps.WaitForActivation
            });
        }

        return null;
    }

    private static bool TryResolveMonth(string? month, out DateOnly startDate, out string monthText)
    {
        if (string.IsNullOrWhiteSpace(month))
        {
            var now = DateTime.UtcNow;
            startDate = new DateOnly(now.Year, now.Month, 1);
            monthText = startDate.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            return true;
        }

        if (DateTime.TryParseExact(
            month,
            "yyyy-MM",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsedMonth))
        {
            startDate = new DateOnly(parsedMonth.Year, parsedMonth.Month, 1);
            monthText = startDate.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            return true;
        }

        startDate = default;
        monthText = string.Empty;
        return false;
    }
}
