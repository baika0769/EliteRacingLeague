using System.ComponentModel.DataAnnotations;

using Eliteracingleague.API.Constants;

namespace Eliteracingleague.API.DTOs.Spectator;

public class CreatePredictionRequest
{
    public int TournamentId { get; set; }
    public int PredictedHorseId { get; set; }

    [Range(
        SpectatorBettingRules.MinimumStakePoints,
        SpectatorBettingRules.MaximumStakePoints,
        ErrorMessage = "Stake points must be between 10 and 100.")]
    public int StakePoints { get; set; }
}
