namespace Eliteracingleague.API.Constants;

public static class JockeyDistanceSkillLevels
{
    public const string NoExperience = "NoExperience";
    public const string Basic = "Basic";
    public const string Good = "Good";
    public const string Expert = "Expert";

    public static readonly string[] All =
    {
        NoExperience,
        Basic,
        Good,
        Expert
    };

    public static bool IsValid(string? skillLevel)
    {
        return !string.IsNullOrWhiteSpace(skillLevel)
            && All.Contains(skillLevel);
    }
}
