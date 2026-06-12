namespace Eliteracingleague.API.Constants;

public static class PredictionStatuses
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
}