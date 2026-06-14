namespace Eliteracingleague.API.DTOs.Owner;

public class SendJockeyInvitationRequest
{
    public int JockeyId { get; set; }
    public decimal? FeeAmount { get; set; }
    public string? Message { get; set; }
}
