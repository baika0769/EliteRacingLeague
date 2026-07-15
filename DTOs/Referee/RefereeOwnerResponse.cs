namespace Eliteracingleague.API.DTOs.Referee;

public class RefereeOwnerResponse
{
    public int OwnerId { get; set; }
    public string OwnerName { get; set; } = null!;
    public string? Email { get; set; }
    public string? Phone { get; set; }
}
