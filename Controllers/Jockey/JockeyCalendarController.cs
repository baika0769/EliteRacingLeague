using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Jockey.Calendar;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Eliteracingleague.API.Controllers.Jockey;

[Route("api/jockey/calendar")]
[ApiController]
[Authorize(Roles = UserRoles.Jockey)]
public class JockeyCalendarController : ControllerBase
{
    private readonly EliteRacingLeagueContext _context;

    public JockeyCalendarController(EliteRacingLeagueContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetCalendar([FromQuery] int? month, [FromQuery] int? year)
    {
        var jockeyId = GetCurrentJockeyId();

        if (jockeyId == null)
        {
            return InvalidToken();
        }

        var jockey = await _context.Jockeys
            .AsNoTracking()
            .Where(j => j.JockeyId == jockeyId.Value)
            .Select(j => new
            {
                j.IsActive,
                j.JockeyNavigation.Role,
                UserStatus = j.JockeyNavigation.Status,
                j.JockeyNavigation.EmailVerified
            })
            .FirstOrDefaultAsync();

        if (jockey == null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Không tìm thấy hồ sơ jockey."
            });
        }

        var profileError = ValidateActiveJockey(jockey.Role, jockey.UserStatus, jockey.EmailVerified, jockey.IsActive);

        if (profileError != null)
        {
            return profileError;
        }

        var now = DateTime.UtcNow;
        var calendarMonth = month ?? now.Month;
        var calendarYear = year ?? now.Year;

        if (calendarMonth < 1 || calendarMonth > 12)
        {
            return BadRequest(new { message = "Month không hợp lệ." });
        }

        if (calendarYear < 1)
        {
            return BadRequest(new { message = "Year không hợp lệ." });
        }

        var startDate = new DateOnly(calendarYear, calendarMonth, 1);
        var daysInMonth = DateTime.DaysInMonth(calendarYear, calendarMonth);
        var endDate = startDate.AddDays(daysInMonth);
        var startDateTime = startDate.ToDateTime(TimeOnly.MinValue);
        var endDateTime = endDate.ToDateTime(TimeOnly.MinValue);

        var availabilityByDate = await _context.JockeyAvailabilities
            .AsNoTracking()
            .Where(a => a.JockeyId == jockeyId.Value
                && a.AvailableDate >= startDate
                && a.AvailableDate < endDate)
            .ToDictionaryAsync(a => a.AvailableDate, a => a.Status);

        var raceItems = await _context.RaceRegistrations
            .AsNoTracking()
            .Where(r => r.JockeyId == jockeyId.Value
                && r.Status == RaceRegistrationStatuses.ReadyToRace
                && r.Race.RaceDate >= startDateTime
                && r.Race.RaceDate < endDateTime
                && r.Race.Status != RaceStatuses.Cancelled)
            .Select(r => new
            {
                RaceDateOnly = DateOnly.FromDateTime(r.Race.RaceDate),
                Item = new JockeyCalendarItemResponse
                {
                    RaceId = r.RaceId,
                    RaceName = r.Race.RaceName,
                    RaceDate = r.Race.RaceDate,
                    Location = r.Race.Location,
                    HorseId = r.HorseId,
                    HorseName = r.Horse.HorseName,
                    OwnerId = r.OwnerId,
                    OwnerName = r.Owner.Owner.FullName,
                    RegistrationStatus = r.Status
                }
            })
            .ToListAsync();

        var raceItemsByDate = raceItems
            .GroupBy(r => r.RaceDateOnly)
            .ToDictionary(g => g.Key, g => g.Select(r => r.Item).ToList());

        var days = Enumerable.Range(0, daysInMonth)
            .Select(offset =>
            {
                var date = startDate.AddDays(offset);
                availabilityByDate.TryGetValue(date, out var availabilityStatus);
                raceItemsByDate.TryGetValue(date, out var items);

                items ??= new List<JockeyCalendarItemResponse>();

                return new JockeyCalendarDayResponse
                {
                    Date = date,
                    IsAvailable = availabilityStatus != null && !IsUnavailableStatus(availabilityStatus),
                    AvailabilityStatus = availabilityStatus,
                    HasRace = items.Count > 0,
                    Items = items
                };
            })
            .ToList();

        return Ok(new JockeyCalendarResponse
        {
            Month = calendarMonth,
            Year = calendarYear,
            ProfileStatus = jockey.UserStatus,
            IsActive = jockey.IsActive,
            Days = days
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

    private static bool IsUnavailableStatus(string status)
    {
        return string.Equals(status, "Unavailable", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "Busy", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "Inactive", StringComparison.OrdinalIgnoreCase);
    }
}
