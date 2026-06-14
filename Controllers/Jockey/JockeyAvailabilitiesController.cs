using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Jockey.Calendar;
using Eliteracingleague.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Eliteracingleague.API.Controllers.Jockey;

[Route("api/jockey/availabilities")]
[ApiController]
[Authorize(Roles = UserRoles.Jockey)]
public class JockeyAvailabilitiesController : ControllerBase
{
    private readonly EliteRacingLeagueContext _context;

    public JockeyAvailabilitiesController(EliteRacingLeagueContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAvailabilities([FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
    {
        var jockeyId = GetCurrentJockeyId();

        if (jockeyId == null)
        {
            return InvalidToken();
        }

        var profileError = await ValidateActiveJockeyAsync(jockeyId.Value);

        if (profileError != null)
        {
            return profileError;
        }

        if (!from.HasValue || !to.HasValue)
        {
            return BadRequest(new { message = "from và to là bắt buộc. Định dạng hợp lệ: yyyy-MM-dd." });
        }

        if (from.Value > to.Value)
        {
            return BadRequest(new { message = "from không được lớn hơn to." });
        }

        var items = await _context.JockeyAvailabilities
            .AsNoTracking()
            .Where(a => a.JockeyId == jockeyId.Value
                && a.AvailableDate >= from.Value
                && a.AvailableDate <= to.Value)
            .OrderBy(a => a.AvailableDate)
            .Select(a => new JockeyAvailabilityResponse
            {
                Date = a.AvailableDate,
                Status = a.Status
            })
            .ToListAsync();

        return Ok(new { items });
    }

    [HttpPut]
    public async Task<IActionResult> UpdateAvailabilities(UpdateJockeyAvailabilitiesRequest request)
    {
        var jockeyId = GetCurrentJockeyId();

        if (jockeyId == null)
        {
            return InvalidToken();
        }

        var profileError = await ValidateActiveJockeyAsync(jockeyId.Value);

        if (profileError != null)
        {
            return profileError;
        }

        if (request.Items.Count == 0)
        {
            return BadRequest(new { message = "Danh sách availability không được rỗng." });
        }

        var duplicateDates = request.Items
            .GroupBy(i => i.Date)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key.ToString("yyyy-MM-dd"))
            .ToList();

        if (duplicateDates.Count > 0)
        {
            return BadRequest(new
            {
                message = "Request không được chứa ngày trùng lặp.",
                duplicateDates
            });
        }

        var invalidStatus = request.Items
            .FirstOrDefault(i => !JockeyAvailabilityStatuses.IsPersistedStatus(i.Status));

        if (invalidStatus != null)
        {
            return BadRequest(new
            {
                message = "Status chỉ được là Available hoặc Unavailable.",
                status = invalidStatus.Status
            });
        }

        var dates = request.Items
            .Select(i => i.Date)
            .Distinct()
            .ToList();

        var minDate = dates.Min();
        var maxDate = dates.Max();
        var minDateTime = minDate.ToDateTime(TimeOnly.MinValue);
        var maxDateTimeExclusive = maxDate.AddDays(1).ToDateTime(TimeOnly.MinValue);

        var racingDates = await _context.RaceRegistrations
            .AsNoTracking()
            .Where(r => r.JockeyId == jockeyId.Value
                && r.Status == RaceRegistrationStatuses.ReadyToRace
                && r.Race.RaceDate >= minDateTime
                && r.Race.RaceDate < maxDateTimeExclusive
                && r.Race.Status != RaceStatuses.Cancelled)
            .Select(r => r.Race.RaceDate)
            .Distinct()
            .ToListAsync();

        var blockedDates = dates
            .Where(d => racingDates.Any(raceDate => DateOnly.FromDateTime(raceDate) == d))
            .OrderBy(d => d)
            .ToList();

        if (blockedDates.Count > 0)
        {
            return BadRequest(new
            {
                message = "Không thể cập nhật availability cho ngày đã có race.",
                blockedDates
            });
        }

        var existingAvailabilities = await _context.JockeyAvailabilities
            .Where(a => a.JockeyId == jockeyId.Value && dates.Contains(a.AvailableDate))
            .ToDictionaryAsync(a => a.AvailableDate);

        var updatedCount = 0;

        foreach (var item in request.Items)
        {
            if (existingAvailabilities.TryGetValue(item.Date, out var availability))
            {
                availability.Status = item.Status;
            }
            else
            {
                _context.JockeyAvailabilities.Add(new JockeyAvailability
                {
                    JockeyId = jockeyId.Value,
                    AvailableDate = item.Date,
                    Status = item.Status
                });
            }

            updatedCount++;
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Cập nhật lịch rảnh/bận thành công.",
            updatedCount
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

    private async Task<IActionResult?> ValidateActiveJockeyAsync(int jockeyId)
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

        if (data.Role != UserRoles.Jockey)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Tài khoản không có quyền Jockey."
            });
        }

        if (!data.EmailVerified)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Email chưa được xác thực.",
                nextStep = AuthNextSteps.VerifyEmail
            });
        }

        if (data.UserStatus == UserStatuses.Banned)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Tài khoản đã bị khóa.",
                nextStep = AuthNextSteps.AccountBlocked
            });
        }

        if (data.UserStatus == UserStatuses.Inactive)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Tài khoản đang bị vô hiệu hóa.",
                nextStep = AuthNextSteps.ContactSupport
            });
        }

        if (data.UserStatus != UserStatuses.Active || !data.IsActive)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Tài khoản jockey chưa được kích hoạt.",
                status = data.UserStatus,
                isActive = data.IsActive,
                nextStep = AuthNextSteps.WaitForActivation
            });
        }

        return null;
    }
}
