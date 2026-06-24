namespace Eliteracingleague.API.DTOs.Jockey;

public class JockeyInvitationHorseResponse
{
    public int HorseId { get; set; }
    public string HorseName { get; set; } = null!;
    public string? ImageUrl { get; set; }
    public string? HealthCertificateImageUrl { get; set; }
    public string BreedName { get; set; } = null!;
    public int Age { get; set; }
    public string HealthStatus { get; set; } = null!;
}
