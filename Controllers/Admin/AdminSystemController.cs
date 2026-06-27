using System.Globalization;
using Eliteracingleague.API.Constants;
using Eliteracingleague.API.DTOs.Admin;
using Eliteracingleague.API.Services.SystemTime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eliteracingleague.API.Controllers.Admin;

[ApiController]
[Route("api/admin/system")]
[Authorize(Roles = UserRoles.Admin)]
public class AdminSystemController : ControllerBase
{
    private const string DisabledMessage = "Time override is disabled in this environment.";
    private const string InvalidTimezoneMessage = "Invalid timezone. Use Asia/Ho_Chi_Minh or SE Asia Standard Time.";

    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IRaceTimeStatusService _raceTimeStatusService;
    private readonly IConfiguration _configuration;

    public AdminSystemController(
        IDateTimeProvider dateTimeProvider,
        IRaceTimeStatusService raceTimeStatusService,
        IConfiguration configuration)
    {
        _dateTimeProvider = dateTimeProvider;
        _raceTimeStatusService = raceTimeStatusService;
        _configuration = configuration;
    }

    [HttpGet("time")]
    public IActionResult GetTime()
    {
        return Ok(BuildTimeResponse());
    }

    [HttpPost("time/override")]
    public async Task<IActionResult> OverrideTime(OverrideSystemTimeRequest request, CancellationToken cancellationToken)
    {
        if (!AllowTimeOverride)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = DisabledMessage });
        }

        if (!TryParseLocalTime(request.NowLocal, out var localNow))
        {
            return BadRequest(new { message = "Invalid nowLocal. Use format yyyy-MM-ddTHH:mm:ss." });
        }

        var timeZoneId = string.IsNullOrWhiteSpace(request.Timezone)
            ? SystemDateTimeProvider.DefaultTimeZoneId
            : request.Timezone.Trim();

        if (!SystemDateTimeProvider.TryResolveTimeZone(timeZoneId, out var timeZone))
        {
            return BadRequest(new { message = InvalidTimezoneMessage });
        }

        var unspecifiedLocalNow = DateTime.SpecifyKind(localNow, DateTimeKind.Unspecified);
        var utcNow = TimeZoneInfo.ConvertTimeToUtc(unspecifiedLocalNow, timeZone);
        _dateTimeProvider.OverrideUtcNow(utcNow, timeZoneId);

        SyncTimeStatusesResponse? syncResult = null;
        if (request.AutoSync)
        {
            syncResult = await _raceTimeStatusService.SyncAsync(cancellationToken);
        }

        return Ok(new
        {
            time = BuildTimeResponse(),
            syncResult
        });
    }

    [HttpPost("time/advance")]
    public async Task<IActionResult> AdvanceTime(AdvanceSystemTimeRequest request, CancellationToken cancellationToken)
    {
        if (!AllowTimeOverride)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = DisabledMessage });
        }

        if (request.Days < 0 || request.Hours < 0 || request.Minutes < 0)
        {
            return BadRequest(new { message = "Advance duration cannot be negative." });
        }

        var duration = new TimeSpan(request.Days, request.Hours, request.Minutes, 0);
        _dateTimeProvider.Advance(duration);

        SyncTimeStatusesResponse? syncResult = null;
        if (request.AutoSync)
        {
            syncResult = await _raceTimeStatusService.SyncAsync(cancellationToken);
        }

        return Ok(new
        {
            time = BuildTimeResponse(),
            syncResult
        });
    }

    [HttpDelete("time/override")]
    public IActionResult ClearOverride()
    {
        if (!AllowTimeOverride)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = DisabledMessage });
        }

        _dateTimeProvider.ClearOverride();
        return Ok(BuildTimeResponse());
    }

    [HttpPost("sync-time-statuses")]
    public async Task<IActionResult> SyncTimeStatuses(CancellationToken cancellationToken)
    {
        var result = await _raceTimeStatusService.SyncAsync(cancellationToken);
        return Ok(result);
    }

    private bool AllowTimeOverride => _configuration.GetValue("SystemTime:AllowTimeOverride", false);

    private SystemTimeResponse BuildTimeResponse()
    {
        var timeZoneId = _dateTimeProvider.TimeZoneId;
        var localNow = _dateTimeProvider.GetLocalNow(timeZoneId);

        return new SystemTimeResponse
        {
            RealUtcNow = _dateTimeProvider.RealUtcNow,
            EffectiveUtcNow = _dateTimeProvider.UtcNow,
            EffectiveLocalNow = localNow.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
            Timezone = timeZoneId,
            IsOverridden = _dateTimeProvider.IsOverridden,
            AllowTimeOverride = AllowTimeOverride
        };
    }

    private static bool TryParseLocalTime(string? value, out DateTime dateTime)
    {
        return DateTime.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out dateTime);
    }
}
