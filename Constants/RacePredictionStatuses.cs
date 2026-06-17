namespace Eliteracingleague.API.Constants;

public static class RacePredictionStatuses
{
    public const string Pending = "Pending";
    public const string Locked = "Locked";
    public const string Evaluated = "Evaluated";
    public const string Cancelled = "Cancelled";

    public static readonly string[] All =
    {
        Pending,
        Locked,
        Evaluated,
        Cancelled
    };

    public static bool IsValid(string status)
        => All.Contains(status);
}