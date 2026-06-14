namespace Eliteracingleague.API.DTOs.Jockey;

public class JockeyProfileResponse
{
    public int UserId { get; set; }
    public int JockeyId { get; set; }
    public string? JockeyCode { get; set; }
    public string FullName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? Phone { get; set; }
    public string Role { get; set; } = null!;
    public string Status { get; set; } = null!;
    public bool EmailVerified { get; set; }
    public string NextStep { get; set; } = null!;
    public decimal WeightKg { get; set; }
    public int YearsOfExperience { get; set; }
    public string HealthStatus { get; set; } = null!;
    public string? CertificateNo { get; set; }
    public string? CertificateFileUrl { get; set; }
    public string? ProfileImageUrl { get; set; }
    public string? IdCardFrontUrl { get; set; }
    public string? IdCardBackUrl { get; set; }
    public string? HealthCertificateUrl { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<JockeyDistanceExperienceResponse> DistanceExperiences { get; set; } = new();
    public List<JockeyBreedExperienceResponse> BreedExperiences { get; set; } = new();
}
