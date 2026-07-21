namespace Eliteracingleague.API.Constants;

public static class SeasonRewardStatuses
{
    public const string Eligible = "Eligible";
    public const string Claimed = "Claimed";
    public const string Approved = "Approved";
    public const string Preparing = "Preparing";
    public const string Shipped = "Shipped";
    public const string Delivered = "Delivered";
    public const string Rejected = "Rejected";
    public const string Expired = "Expired";

    public static readonly string[] All =
    {
        Eligible,
        Claimed,
        Approved,
        Preparing,
        Shipped,
        Delivered,
        Rejected,
        Expired
    };

    public static bool IsValid(string? status)
        => !string.IsNullOrWhiteSpace(status) && All.Contains(status);
}
