namespace Eliteracingleague.API.Constants;

public static class RaceStatuses
{
    public const string Open = "Open";
    public const string Closed = "Closed";
    public const string Ongoing = "Ongoing";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";

    public static readonly string[] All =
    {
        Open,
        Closed,
        Ongoing,
        Completed,
        Cancelled
    };

    public static bool IsValid(string? status)
    {
        return !string.IsNullOrWhiteSpace(status)
            && All.Contains(status);
    }
}