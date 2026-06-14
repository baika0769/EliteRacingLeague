namespace Eliteracingleague.API.Constants;

public static class PreRaceInspectionStatuses
{
    public const string PendingConfirmation = "PendingConfirmation";
    public const string Passed = "Passed";
    public const string Failed = "Failed";

    public static readonly string[] All =
    {
        PendingConfirmation,
        Passed,
        Failed
    };

    public static bool IsValid(string status)
        => All.Contains(status);
}