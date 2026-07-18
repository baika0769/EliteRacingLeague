namespace Eliteracingleague.API.Constants;

public static class SeasonStatuses
{
    public const string Draft = "Draft";
    public const string Active = "Active";
    public const string Settling = "Settling";
    public const string Closed = "Closed";
    public const string Cancelled = "Cancelled";

    public static readonly string[] All =
    {
        Draft,
        Active,
        Settling,
        Closed,
        Cancelled
    };

    public static bool IsValid(string? status)
    {
        return !string.IsNullOrWhiteSpace(status)
            && All.Contains(status);
    }
}