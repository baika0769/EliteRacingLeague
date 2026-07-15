using System.ComponentModel.DataAnnotations;

namespace Eliteracingleague.API.DTOs.Spectator;

public class CreatePredictionRequest
{
    public int TournamentId { get; set; }
    public int PredictedHorseId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Stake points must be greater than 0.")]
    public int StakePoints { get; set; }
}
