namespace Eliteracingleague.API.Constants;

public static class RaceResultStatuses
{
    public const string Draft = "Draft";
    public const string RefereeConfirmed = "RefereeConfirmed";
    public const string AdminApproved = "AdminApproved";
    public const string Published = "Published";
    public const string Returned = "Returned";

    public static readonly string[] All =
    {
        Draft,
        RefereeConfirmed,
        AdminApproved,
        Published,
        Returned
    };

    public static bool IsValid(string? status)
    {
        return !string.IsNullOrWhiteSpace(status)
            && All.Contains(status);
    }
}