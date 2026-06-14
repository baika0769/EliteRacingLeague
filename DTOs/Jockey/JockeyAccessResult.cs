using Eliteracingleague.API.Models;

namespace Eliteracingleague.API.DTOs.Jockey;

public class JockeyAccessResult
{
    public bool Succeeded { get; set; }
    public int StatusCode { get; set; }
    public string? Message { get; set; }
    public string? NextStep { get; set; }
    public User? User { get; set; }
    public Eliteracingleague.API.Models.Jockey? Jockey { get; set; }
}
