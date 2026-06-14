namespace Eliteracingleague.API.Constants;

public static class JockeyBreedSkillLevels
{
    public const string Basic = "Basic";
    public const string Good = "Good";
    public const string Expert = "Expert";

    public static readonly string[] All =
    {
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
