namespace Eliteracingleague.API.Services.JockeyMatching;

public class JockeyMatchScoreResult
{
    public int MatchScore { get; set; }
    public List<string> MatchReasons { get; set; } = new();
}
