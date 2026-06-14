namespace Eliteracingleague.API.Constants;

public static class JockeyAvailabilityStatuses
{
    public const string Available = "Available";
    public const string Unavailable = "Unavailable";
    public const string RacingDay = "RacingDay";

    public static readonly string[] PersistedStatuses =
    {
        Available,
        Unavailable
    };

    public static bool IsPersistedStatus(string? status)
    {
        return !string.IsNullOrWhiteSpace(status)
            && PersistedStatuses.Contains(status);
    }
}
