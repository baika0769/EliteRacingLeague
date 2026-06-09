namespace Eliteracingleague.API.Constants;

public static class RaceViolationActions
{
    public const string Warning = "Warning";
    public const string PointDeduction = "PointDeduction";
    public const string Disqualified = "Disqualified";

    public static readonly string[] All =
    {
        Warning,
        PointDeduction,
        Disqualified
    };

    public static bool IsValid(string? action)
    {
        return !string.IsNullOrWhiteSpace(action)
            && All.Contains(action);
    }
}