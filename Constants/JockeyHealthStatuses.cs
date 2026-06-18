namespace Eliteracingleague.API.Constants;

public static class JockeyHealthStatuses
{
    public const string Fit = "Fit";
    public const string Injured = "Injured";
    public const string Suspended = "Suspended";
    public const string Unknown = "Unknown";

    public static readonly string[] All =
    {
        Fit,
        Injured,
        Suspended,
        Unknown
    };

    public static bool IsValid(string? status)
    {
        return Normalize(status) != null;
    }

    public static string? Normalize(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        var trimmedStatus = status.Trim();

        return All.FirstOrDefault(validStatus =>
            string.Equals(validStatus, trimmedStatus, StringComparison.OrdinalIgnoreCase));
    }

    public static bool CanRace(string? status)
    {
        return Normalize(status) == Fit;
    }
}
