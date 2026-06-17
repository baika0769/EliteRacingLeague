using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Constants;

public static class RaceStatuses
{

    public const string Open = "Open";
    public const string Closed = "Closed";
    public const string Ongoing = "Ongoing";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";

    public const string Scheduled = "Scheduled";
    public const string AssignedReferee = "AssignedReferee";
    public const string RefereeReady = "RefereeReady";
    public const string ResultPending = "ResultPending";
    public const string Published = "Published";

    public static readonly string[] All =
    {
        Open,
        Closed,
        Ongoing,
        Completed,
        Cancelled,
        Scheduled,
        AssignedReferee,
        RefereeReady,
        ResultPending,
        Published
    };

    public static bool IsValid(string status)
        => All.Contains(status);


}