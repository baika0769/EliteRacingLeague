namespace Eliteracingleague.API.Models;

public class RaceResultRevision
{
    public long RaceResultRevisionId { get; set; }
    public int RaceId { get; set; }
    public int? ResultId { get; set; }
    public int RegistrationId { get; set; }
    public int VersionNumber { get; set; }
    public string ChangeType { get; set; } = null!;
    public string SnapshotJson { get; set; } = null!;
    public string Reason { get; set; } = null!;
    public int ChangedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public virtual Race Race { get; set; } = null!;
    public virtual RaceResult? Result { get; set; }
    public virtual RaceRegistration Registration { get; set; } = null!;
    public virtual User ChangedByUser { get; set; } = null!;
}
