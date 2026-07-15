namespace Eliteracingleague.API.DTOs.Admin;

public class UpdateRefereeRequest
{
    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? LicenseNo { get; set; }

    public int? ExperienceYears { get; set; }

    public bool? IsActive { get; set; }
}