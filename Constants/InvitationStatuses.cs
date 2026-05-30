namespace Eliteracingleague.API.Constants;

public static class InvitationStatuses
{
    public const string Pending = "Pending";
    public const string Accepted = "Accepted";
    public const string Rejected = "Rejected";
    public const string Cancelled = "Cancelled";
    public const string Expired = "Expired";

    public static readonly string[] All =
    {
        Pending,
        Accepted,
        Rejected,
        Cancelled,
        Expired
    };

    public static bool IsValid(string? status)
    {
        return !string.IsNullOrWhiteSpace(status)
            && All.Contains(status);
    }

    public static bool CanTransition(string currentStatus, string nextStatus)
    {
        if (!IsValid(currentStatus) || !IsValid(nextStatus))
        {
            return false;
        }

        return currentStatus switch
        {
            Pending => nextStatus is Accepted or Rejected or Cancelled or Expired,

            Accepted => false,
            Rejected => false,
            Cancelled => false,
            Expired => false,

            _ => false
        };
    }
}