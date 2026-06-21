namespace Eliteracingleague.API.Constants;

public static class PrizeAwardStatuses
{
    public const string ReadyToClaim = "ReadyToClaim";
    public const string UnderReview = "UnderReview";
    public const string Paid = "Paid";
    public const string Rejected = "Rejected";

    public static readonly string[] All =
    {
        ReadyToClaim,
        UnderReview,
        Paid,
        Rejected
    };

    public static bool IsValid(string? status)
    {
        return !string.IsNullOrWhiteSpace(status)
            && All.Contains(status);
    }

    public static bool CanClaim(string? status)
    {
        return status == ReadyToClaim;
    }
}
