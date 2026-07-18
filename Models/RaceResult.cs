using System;
using System.Collections.Generic;

namespace Eliteracingleague.API.Models;

public partial class RaceResult
{
    public int ResultId { get; set; }

    public int RaceId { get; set; }

    public int RegistrationId { get; set; }

    public decimal? FinishTimeSeconds { get; set; }

    public int? FinishPosition { get; set; }

    public decimal? Score { get; set; }

    public string OutcomeStatus { get; set; } = "Finished";

    public string Status { get; set; } = null!;

    public int EnteredByRefereeId { get; set; }

    public int? AdminConfirmedBy { get; set; }

    public DateTime? PublishedAt { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int RevisionNumber { get; set; }

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public virtual User? AdminConfirmedByNavigation { get; set; }

    public virtual RaceReferee EnteredByReferee { get; set; } = null!;

    public virtual Race Race { get; set; } = null!;

    public virtual RaceRegistration Registration { get; set; } = null!;

    public virtual ICollection<RaceResultRevision> Revisions { get; set; } = new List<RaceResultRevision>();
}
