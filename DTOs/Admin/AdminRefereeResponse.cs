namespace Eliteracingleague.API.DTOs.Admin;

public class AdminRefereeResponse
{
    public int RefereeId { get; set; }

    public int UserId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string Role { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public bool EmailVerified { get; set; }

    public string? LicenseNo { get; set; }

    public int? ExperienceYears { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }
}