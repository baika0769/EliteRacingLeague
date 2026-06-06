namespace Eliteracingleague.API.Constants;

public static class UserStatuses
{
    public const string Pending = "Pending";
    public const string Active = "Active";
    public const string Inactive = "Inactive";
    public const string Banned = "Banned";

    public static readonly string[] All =
    {
        Pending,
        Active,
        Inactive,
        Banned
    };

    public static bool IsValid(string? status)
    {
        return !string.IsNullOrWhiteSpace(status)
            && All.Contains(status);
    }
}