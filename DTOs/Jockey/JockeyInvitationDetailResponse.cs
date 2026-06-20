namespace Eliteracingleague.API.DTOs.Jockey;

public class JockeyInvitationDetailResponse
{
    public int InvitationId { get; set; }
    public string Status { get; set; } = null!;
    public DateTime SentAt { get; set; }
    public DateTime? RespondedAt { get; set; }
    public string? OwnerMessage { get; set; }
    public decimal? FeeAmount { get; set; }
    public JockeyInvitationRaceResponse Race { get; set; } = null!;
    public JockeyInvitationTournamentResponse Tournament { get; set; } = null!;
    public JockeyInvitationHorseResponse Horse { get; set; } = null!;
    public JockeyInvitationOwnerResponse Owner { get; set; } = null!;
    public int MatchScore { get; set; }
    public List<string> MatchReasons { get; set; } = new();
}
