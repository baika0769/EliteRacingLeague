namespace Eliteracingleague.API.Services.SystemTime;

public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
    DateTime RealUtcNow { get; }
    bool IsOverridden { get; }
    string TimeZoneId { get; }
    DateTime GetLocalNow(string timeZoneId);
    void OverrideUtcNow(DateTime utcNow, string timeZoneId);
    void ClearOverride();
    void Advance(TimeSpan duration);
}
