using System;

namespace Eliteracingleague.API.Models;

public partial class EmailVerificationOtp
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string OtpHash { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }

    public bool IsUsed { get; set; }

    public int FailedAttempts { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UsedAt { get; set; }

    public virtual User User { get; set; } = null!;
}