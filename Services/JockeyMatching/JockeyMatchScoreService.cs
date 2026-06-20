using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Models;

namespace Eliteracingleague.API.Services.JockeyMatching;

public class JockeyMatchScoreService : IJockeyMatchScoreService
{
    public JockeyMatchScoreResult Calculate(
        Jockey jockey,
        Horse horse,
        Race race,
        HorseBreed breed)
    {
        var score = 0;
        var reasons = new List<string>();

        var distanceLevel = jockey.JockeyDistanceExperiences
            .Where(e => e.DistanceMeters == race.DistanceMeters)
            .Select(e => e.SkillLevel)
            .FirstOrDefault();

        score += distanceLevel switch
        {
            JockeyDistanceSkillLevels.Expert => 40,
            JockeyDistanceSkillLevels.Good => 30,
            JockeyDistanceSkillLevels.Basic => 18,
            _ => 5
        };

        if (distanceLevel == JockeyDistanceSkillLevels.Expert)
        {
            reasons.Add($"Expert in {race.DistanceMeters}m races");
        }
        else if (distanceLevel == JockeyDistanceSkillLevels.Good)
        {
            reasons.Add($"Good experience in {race.DistanceMeters}m races");
        }

        var breedLevel = jockey.JockeyBreedExperiences
            .Where(e => e.BreedId == horse.BreedId)
            .Select(e => e.ExperienceLevel)
            .FirstOrDefault();

        score += breedLevel switch
        {
            JockeyBreedSkillLevels.Expert => 30,
            JockeyBreedSkillLevels.Good => 22,
            JockeyBreedSkillLevels.Basic => 12,
            _ => 5
        };

        if (breedLevel == JockeyBreedSkillLevels.Expert)
        {
            reasons.Add($"{breed.BreedName} breed specialist");
        }
        else if (breedLevel == JockeyBreedSkillLevels.Good)
        {
            reasons.Add($"Good experience with {breed.BreedName} horses");
        }

        score += jockey.YearsOfExperience switch
        {
            >= 5 => 20,
            >= 3 => 15,
            >= 1 => 8,
            _ => 3
        };

        if (jockey.YearsOfExperience >= 3)
        {
            reasons.Add("Experienced jockey");
        }

        if (JockeyHealthStatuses.CanRace(jockey.HealthStatus))
        {
            score += 10;
        }
        else
        {
            reasons.Add("Health status may affect race eligibility");
        }

        return new JockeyMatchScoreResult
        {
            MatchScore = Math.Clamp(score, 0, 100),
            MatchReasons = reasons
        };
    }
}
