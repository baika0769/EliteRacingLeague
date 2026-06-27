namespace Eliteracingleague.API.Constants;

public static class SeasonStatuses
{
    public const string Draft = "Draft";
    public const string Active = "Active";
    public const string Closed = "Closed";
    public const string Cancelled = "Cancelled";

    public static readonly string[] All =
    {
        Draft,
        Active,
        Closed,
        Cancelled
    };
}
