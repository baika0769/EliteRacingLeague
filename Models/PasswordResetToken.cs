namespace Eliteracingleague.API.Models;

public class PasswordResetToken
{
    public long PasswordResetTokenId { get; set; }
    public int UserId { get; set; }
    public string TokenHash { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public string? RequestedIp { get; set; }
    public virtual User User { get; set; } = null!;
}
