namespace Eliteracingleague.API.Constants;

public static class RaceStatuses
{
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

    public static bool IsValid(string? status)
    {
        return !string.IsNullOrWhiteSpace(status)
            && All.Contains(status);
    }

    public static bool CanRegister(string? status)
    {
        return status is Scheduled or AssignedReferee or RefereeReady;
    }

    public static bool IsClosedForPrediction(string? status)
    {
        return status is Ongoing or Finished or ResultPending or Published or Cancelled;
    }

    public static bool IsClosedForJockeyAssignment(string? status)
    {
        return status is Ongoing or Finished or ResultPending or Published or Cancelled;
    }

    public static bool IsCompletedForDashboard(string? status)
    {
        return status is Finished or ResultPending or Published;
    }
}
