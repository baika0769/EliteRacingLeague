namespace Eliteracingleague.API.Constants;

public static class HorseHealthStatuses
{
    public const string Healthy = "Healthy";
    public const string NeedsCheck = "NeedsCheck";
    public const string Sick = "Sick";
    public const string Injured = "Injured";
    public const string Recovering = "Recovering";
    public const string UnfitToRace = "UnfitToRace";

    public static readonly string[] All =
    {
        Healthy,
        NeedsCheck,
        Sick,
        Injured,
        Recovering,
        UnfitToRace
    };

    public static bool IsValid(string? status)
    {
        return !string.IsNullOrWhiteSpace(status)
            && All.Contains(status);
    }

    public static bool CanRace(string? status)
    {
        return status == Healthy;
    }
}