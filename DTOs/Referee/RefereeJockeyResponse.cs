namespace Eliteracingleague.API.DTOs.Referee;

public class RefereeJockeyResponse
{
    public int JockeyId { get; set; }
    public string JockeyName { get; set; } = null!;
    public string? ProfileImageUrl { get; set; }
    public string? Avatar { get; set; }
    public decimal WeightKg { get; set; }
    public int YearsOfExperience { get; set; }
    public int Experience { get; set; }
    public string HealthStatus { get; set; } = null!;
    public string? CertificateNo { get; set; }
    public string? CertificateFileUrl { get; set; }
    public string? HealthCertificateUrl { get; set; }
    public string? HealthCertificate { get; set; }
    public bool IsActive { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
}
