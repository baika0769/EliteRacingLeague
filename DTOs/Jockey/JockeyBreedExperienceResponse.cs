namespace Eliteracingleague.API.DTOs.Jockey;

public class JockeyBreedExperienceResponse
{
    public int JockeyBreedExperienceId { get; set; }
    public int BreedId { get; set; }
    public string BreedName { get; set; } = null!;
    public string ExperienceLevel { get; set; } = null!;
}
