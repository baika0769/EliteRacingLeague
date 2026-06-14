namespace Eliteracingleague.API.DTOs.Jockey;

public class JockeyDistanceExperienceResponse
{
    public int JockeyDistanceExperienceId { get; set; }
    public int DistanceMeters { get; set; }
    public string Label { get; set; } = null!;
    public string DistanceLabel { get; set; } = null!;
    public string SkillLevel { get; set; } = null!;
}
