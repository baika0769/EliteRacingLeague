namespace Eliteracingleague.API.DTOs.Admin
{
    public class AdminVerificationResponse
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string Role { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public bool EmailVerified { get; set; }
        public DateTime CreatedAt { get; set; }

        public string? Address { get; set; }

        public decimal? WeightKg { get; set; }
        public int? YearsOfExperience { get; set; }
        public string? HealthStatus { get; set; }
        public string? CertificateNo { get; set; }
        public string? CertificateFileUrl { get; set; }
    }
}