namespace Eliteracingleague.API.Constants;

public static class RefereeReportTypes
{
    public const string PreRace = "PreRace";
    public const string PostRace = "PostRace";

    public static readonly string[] All =
    {
        PreRace,
        PostRace
    };

    public static bool IsValid(string? type)
    {
        return !string.IsNullOrWhiteSpace(type)
            && All.Contains(type);
    }
}