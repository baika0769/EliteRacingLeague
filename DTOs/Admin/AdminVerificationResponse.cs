namespace Eliteracingleague.API.DTOs.Admin
{
    public class AdminVerificationResponse
    {
        public int UserId { get; set; }
        public int? JockeyId { get; set; }
        public string? JockeyCode { get; set; }

        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string Role { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public bool EmailVerified { get; set; }
        public DateTime CreatedAt { get; set; }

        public string? Address { get; set; }

        public bool? IsActive { get; set; }
        public string? ProfileImageUrl { get; set; }
        public string? IdCardFrontUrl { get; set; }
        public string? IdCardBackUrl { get; set; }
        public string? CertificateNo { get; set; }
        public string? CertificateFileUrl { get; set; }
        public string? HealthCertificateUrl { get; set; }
        public decimal? WeightKg { get; set; }
        public int? YearsOfExperience { get; set; }
        public string? HealthStatus { get; set; }

        public List<AdminVerificationDistanceExperienceResponse> DistanceExperiences { get; set; } = new();
        public List<AdminVerificationBreedExperienceResponse> BreedExperiences { get; set; } = new();
    }

    public class AdminVerificationDistanceExperienceResponse
    {
        public int DistanceMeters { get; set; }
        public string Label { get; set; } = string.Empty;
        public string SkillLevel { get; set; } = string.Empty;
    }

    public class AdminVerificationBreedExperienceResponse
    {
        public int BreedId { get; set; }
        public string BreedName { get; set; } = string.Empty;
        public string ExperienceLevel { get; set; } = string.Empty;
    }
}