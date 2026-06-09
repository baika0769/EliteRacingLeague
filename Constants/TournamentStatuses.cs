namespace Eliteracingleague.API.Constants;

public static class TournamentStatuses
{
    public const string Draft = "Draft";
    public const string OpenRegistration = "OpenRegistration";
    public const string Ongoing = "Ongoing";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";

    public static readonly string[] All =
    {
        Draft,
        OpenRegistration,
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