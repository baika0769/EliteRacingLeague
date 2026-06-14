namespace Eliteracingleague.API.Constants;

public static class JockeyDistanceMeters
{
    public const int Sprint = 1000;
    public const int Mid = 1500;
    public const int Endurance = 2400;

    public const string SprintLabel = "1000m Sprint";
    public const string MidLabel = "1500m Mid";
    public const string EnduranceLabel = "2400m Endurance";

    public static readonly int[] All =
    {
        Sprint,
        Mid,
        Endurance
    };

    public static readonly IReadOnlyDictionary<int, string> Labels = new Dictionary<int, string>
    {
        [Sprint] = SprintLabel,
        [Mid] = MidLabel,
        [Endurance] = EnduranceLabel
    };

    public static bool IsValid(int distanceMeters)
    {
        return All.Contains(distanceMeters);
    }
}
