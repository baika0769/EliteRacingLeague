using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Models;

namespace Eliteracingleague.API.Services;

public static class JockeyProfileService
{
    public static bool IsJockeyProfileCompleted(Jockey jockey)
    {
        return jockey != null
            && !string.IsNullOrWhiteSpace(jockey.ProfileImageUrl)
            && !string.IsNullOrWhiteSpace(jockey.IdCardFrontUrl)
            && !string.IsNullOrWhiteSpace(jockey.IdCardBackUrl)
            && !string.IsNullOrWhiteSpace(jockey.CertificateNo)
            && !string.IsNullOrWhiteSpace(jockey.CertificateFileUrl)
            && !string.IsNullOrWhiteSpace(jockey.HealthCertificateUrl)
            && jockey.WeightKg > 0
            && jockey.YearsOfExperience >= 0
            && HorseHealthStatuses.IsValid(jockey.HealthStatus)
            && HasRequiredDistanceExperiences(jockey.JockeyDistanceExperiences);
    }

    private static bool HasRequiredDistanceExperiences(IEnumerable<JockeyDistanceExperience> distanceExperiences)
    {
        var experiences = distanceExperiences.ToList();
        var distances = experiences.Select(e => e.DistanceMeters).Distinct().ToHashSet();

        return JockeyDistanceMeters.All.All(distances.Contains)
            && experiences.All(e => JockeyDistanceMeters.IsValid(e.DistanceMeters)
                && JockeyDistanceSkillLevels.IsValid(e.SkillLevel));
    }
}
