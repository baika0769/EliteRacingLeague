namespace Eliteracingleague.API.Constants;

public static class RaceStatuses
{
    public const string Open = "Open";
    public const string Closed = "Closed";
    public const string Completed = "Completed";

    public const string Scheduled = "Scheduled";
    public const string AssignedReferee = "AssignedReferee";
    public const string RefereeReady = "RefereeReady";
    public const string Ongoing = "Ongoing";
    public const string Finished = "Finished";
    public const string ResultPending = "ResultPending";
    public const string Published = "Published";
    public const string Cancelled = "Cancelled";

    public static readonly string[] All =
    {
        Scheduled,
        AssignedReferee,
        RefereeReady,
        Ongoing,
        Finished,
        ResultPending,
        Published,
        Cancelled
    };

    public static bool IsValid(string status)
        => All.Contains(status);
}