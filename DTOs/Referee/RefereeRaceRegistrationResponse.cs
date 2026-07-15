namespace Eliteracingleague.API.DTOs.Referee;

public class RefereeRaceRegistrationResponse
{
    public int RegistrationId { get; set; }
    public string RegistrationCode { get; set; } = null!;
    public string RegistrationStatus { get; set; } = null!;
    public string Status { get; set; } = null!;
    public RefereeHorseResponse Horse { get; set; } = null!;
    public RefereeOwnerResponse Owner { get; set; } = null!;
    public RefereeJockeyResponse? Jockey { get; set; }
    public RefereeInspectionResponse? Inspection { get; set; }

    public int HorseId { get; set; }
    public string HorseName { get; set; } = null!;
    public string? HorseImageUrl { get; set; }
    public string HorseHealthStatus { get; set; } = null!;
    public string? HealthCertificateImageUrl { get; set; }
    public int OwnerId { get; set; }
    public int? JockeyId { get; set; }
    public string? JockeyName { get; set; }
    public bool[]? Checklist { get; set; }
    public string? RuleRef { get; set; }
    public string? Severity { get; set; }
    public string? Violation { get; set; }
    public string? Outcome { get; set; }
}
