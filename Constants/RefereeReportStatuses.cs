namespace Eliteracingleague.API.Constants;

public static class RefereeReportStatuses
{
    public const string Submitted = "Submitted";
    public const string Returned = "Returned";
    public const string Approved = "Approved";

    public static bool IsValid(string? status) => status is
        Submitted or Returned or Approved;
}
