namespace Eliteracingleague.API.DTOs.Jockey;

public class UpdateJockeyVerificationRequest
{
    public decimal WeightKg { get; set; }
    public int YearsOfExperience { get; set; }
    public string? HealthStatus { get; set; }
    public string? CertificateNo { get; set; }
    public string? CertificateFileUrl { get; set; }
    public string? ProfileImageUrl { get; set; }
    public string? IdCardFrontUrl { get; set; }
    public string? IdCardBackUrl { get; set; }
    public string? HealthCertificateUrl { get; set; }
    public List<JockeyDistanceExperienceRequest> DistanceExperiences { get; set; } = new();
    public List<JockeyBreedExperienceRequest> BreedExperiences { get; set; } = new();
}
