namespace Eliteracingleague.API.DTOs.Jockey;

public class JockeyPendingInvitationResponse
{
    public int InvitationId { get; set; }
    public int RegistrationId { get; set; }
    public int RaceId { get; set; }
    public string RaceName { get; set; } = null!;
    public DateTime RaceDate { get; set; }
    public string? Location { get; set; }
    public int OwnerId { get; set; }
    public string OwnerName { get; set; } = null!;
    public string Status { get; set; } = null!;
    public DateTime SentAt { get; set; }
}
