namespace Eliteracingleague.API.DTOs.Spectator;

public class CreatePredictionRequest
{
    public int RaceId { get; set; }
    public int PredictedRegistrationId { get; set; }
}