using Eliteracingleague.API.Models;

namespace Eliteracingleague.API.Services.JockeyMatching;

public interface IJockeyMatchScoreService
{
    JockeyMatchScoreResult Calculate(
        Jockey jockey,
        Horse horse,
        Race race,
        HorseBreed breed);
}
