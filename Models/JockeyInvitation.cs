using System;
using System.Collections.Generic;

namespace Eliteracingleague.API.Models;

public partial class JockeyInvitation
{
    public int InvitationId { get; set; }

    public int RegistrationId { get; set; }

    public int JockeyId { get; set; }

    public int InvitedByOwnerId { get; set; }

    public string Status { get; set; } = null!;

    public decimal? FeeAmount { get; set; }

    public string? Message { get; set; }

    public DateTime SentAt { get; set; }

    public DateTime? RespondedAt { get; set; }

    public virtual HorseOwner InvitedByOwner { get; set; } = null!;

    public virtual Jockey Jockey { get; set; } = null!;

    public virtual RaceRegistration Registration { get; set; } = null!;
}
