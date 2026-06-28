namespace Eliteracingleague.API.Services.SystemTime;

public class SystemDateTimeProvider : IDateTimeProvider
{
    public const string DefaultTimeZoneId = "Asia/Ho_Chi_Minh";
    public const string WindowsVietnamTimeZoneId = "SE Asia Standard Time";

    private readonly object _syncRoot = new();
    private DateTime? _overriddenUtcNow;
    private string _timeZoneId = DefaultTimeZoneId;

    public DateTime UtcNow
    {
        get
        {
            lock (_syncRoot)
            {
                return _overriddenUtcNow ?? RealUtcNow;
            }
        }
    }

    public DateTime RealUtcNow => DateTime.UtcNow;

    public bool IsOverridden
    {
        get
        {
            lock (_syncRoot)
            {
                return _overriddenUtcNow.HasValue;
            }
        }
    }

    public string TimeZoneId
    {
        get
        {
            lock (_syncRoot)
            {
                return _timeZoneId;
            }
        }
    }

    public DateTime GetLocalNow(string timeZoneId)
    {
        var zone = ResolveTimeZone(timeZoneId);
        return TimeZoneInfo.ConvertTimeFromUtc(UtcNow, zone);
    }

    public void OverrideUtcNow(DateTime utcNow, string timeZoneId)
    {
        _ = ResolveTimeZone(timeZoneId);

        lock (_syncRoot)
        {
            _overriddenUtcNow = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
            _timeZoneId = string.IsNullOrWhiteSpace(timeZoneId)
                ? DefaultTimeZoneId
                : timeZoneId.Trim();
        }
    }

    public void ClearOverride()
    {
        lock (_syncRoot)
        {
            _overriddenUtcNow = null;
            _timeZoneId = DefaultTimeZoneId;
        }
    }

    public void Advance(TimeSpan duration)
    {
        lock (_syncRoot)
        {
            _overriddenUtcNow = (_overriddenUtcNow ?? RealUtcNow).Add(duration);
        }
    }

    public static bool TryResolveTimeZone(string? timeZoneId, out TimeZoneInfo timeZone)
    {
        var normalized = string.IsNullOrWhiteSpace(timeZoneId)
            ? DefaultTimeZoneId
            : timeZoneId.Trim();

        if (TryFindTimeZone(normalized, out timeZone))
        {
            return true;
        }

        if (string.Equals(normalized, DefaultTimeZoneId, StringComparison.OrdinalIgnoreCase) &&
            TryFindTimeZone(WindowsVietnamTimeZoneId, out timeZone))
        {
            return true;
        }

        if (string.Equals(normalized, WindowsVietnamTimeZoneId, StringComparison.OrdinalIgnoreCase) &&
            TryFindTimeZone(DefaultTimeZoneId, out timeZone))
        {
            return true;
        }

        timeZone = null!;
        return false;
    }

    public static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        if (TryResolveTimeZone(timeZoneId, out var timeZone))
        {
            return timeZone;
        }

        throw new TimeZoneNotFoundException("Invalid timezone. Use Asia/Ho_Chi_Minh or SE Asia Standard Time.");
    }

    private static bool TryFindTimeZone(string timeZoneId, out TimeZoneInfo timeZone)
    {
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            timeZone = null!;
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            timeZone = null!;
            return false;
        }
    }
}
