namespace Eliteracingleague.API.Constants;

public static class RaceRegistrationStatuses
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string JockeyInvited = "JockeyInvited";
    public const string ReadyToRace = "ReadyToRace";
    public const string Rejected = "Rejected";
    public const string Cancelled = "Cancelled";
    public const string Completed = "Completed";

    public static readonly string[] HorseDeleteBlockingStatuses =
    {
        Pending,
        Approved,
        JockeyInvited,
        ReadyToRace
    };

    public static readonly string[] All =
    {
        Pending,
        Approved,
        JockeyInvited,
        ReadyToRace,
        Rejected,
        Cancelled,
        Completed
    };

    public static bool IsValid(string? status)
    {
        return !string.IsNullOrWhiteSpace(status)
            && All.Contains(status);
    }

    public static bool BlocksHorseDeletion(string? status)
    {
        return !string.IsNullOrWhiteSpace(status)
            && HorseDeleteBlockingStatuses.Contains(status);
    }

    // Luồng chính:
    // Pending -> Approved -> JockeyInvited -> ReadyToRace -> Completed
    public static bool CanTransition(string currentStatus, string nextStatus)
    {
        if (!IsValid(currentStatus) || !IsValid(nextStatus))
        {
            return false;
        }

        return currentStatus switch
        {
            Pending => nextStatus is Approved or Rejected or Cancelled,
            Approved => nextStatus is JockeyInvited or Cancelled,
            JockeyInvited => nextStatus is ReadyToRace or Cancelled,
            ReadyToRace => nextStatus is Completed or Cancelled,

            Rejected => false,
            Cancelled => false,
            Completed => false,

            _ => false
        };
    }
}
