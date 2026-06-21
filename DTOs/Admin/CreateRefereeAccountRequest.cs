namespace Eliteracingleague.API.DTOs.Admin;

public class CreateRefereeAccountRequest
{
    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string Password { get; set; } = string.Empty;

    public string ConfirmPassword { get; set; } = string.Empty;

    public string? LicenseNo { get; set; }

    public int? ExperienceYears { get; set; }
}