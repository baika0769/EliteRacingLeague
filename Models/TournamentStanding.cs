namespace Eliteracingleague.API.Models;

public class TournamentStanding
{
    public long TournamentStandingId { get; set; }
    public int TournamentId { get; set; }
    public int HorseId { get; set; }
    public int OwnerId { get; set; }
    public int? JockeyId { get; set; }
    public int TotalPoints { get; set; }
    public int Wins { get; set; }
    public int SecondPlaces { get; set; }
    public int ThirdPlaces { get; set; }
    public int CompletedRaces { get; set; }
    public decimal TotalFinishTimeSeconds { get; set; }
    public int FinalRank { get; set; }
    public bool IsFinal { get; set; }
    public DateTime CalculatedAt { get; set; }
    public DateTime? FinalizedAt { get; set; }
    public virtual Tournament Tournament { get; set; } = null!;
    public virtual Horse Horse { get; set; } = null!;
    public virtual HorseOwner Owner { get; set; } = null!;
    public virtual Jockey? Jockey { get; set; }
}
