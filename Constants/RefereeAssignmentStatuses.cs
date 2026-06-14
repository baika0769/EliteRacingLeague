namespace Eliteracingleague.API.Constants;

public static class RefereeAssignmentStatuses
{
    public const string Assigned = "Assigned";
    public const string Cancelled = "Cancelled";

    public static readonly string[] All =
    {
        Assigned,
        Cancelled
    };

    public static bool IsValid(string status)
        => All.Contains(status);
}