namespace Eliteracingleague.API.DTOs.Spectator;

public class CreatePredictionRequest
{
    public int TournamentId { get; set; }
    public int PredictedHorseId { get; set; }
}