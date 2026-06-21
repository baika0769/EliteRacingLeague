namespace Eliteracingleague.API.DTOs.Owner;

public class OwnerRaceDetailResponse
{
    public int RaceId { get; set; }
    public int TournamentId { get; set; }
    public string TournamentName { get; set; } = null!;
    public string? TournamentDescription { get; set; }
    public string? TournamentStatus { get; set; }
    public string? TournamentImageUrl { get; set; }
    public decimal? PrizePool { get; set; }
    public string? Rules { get; set; }
    public string? TournamentStartDate { get; set; }
    public string? TournamentEndDate { get; set; }
    public string RaceName { get; set; } = null!;
    public string RaceDate { get; set; } = null!;
    public string? Location { get; set; }
    public int Distance { get; set; }
    public int MaxHorses { get; set; }
    public string Status { get; set; } = null!;
    public string? JockeySelectionDeadline { get; set; }
    public string? PredictionDeadline { get; set; }
    public int? RegistrationId { get; set; }
    public string? RegistrationStatus { get; set; }
    public string? HorseName { get; set; }
    public string? OfficialJockeyName { get; set; }
    public int? RefereeAssignmentId { get; set; }
    public int? RefereeId { get; set; }
    public string? RefereeName { get; set; }
    public string? RefereeEmail { get; set; }
    public string? RefereePhone { get; set; }
    public string? RefereeLicenseNo { get; set; }
    public int? RefereeExperienceYears { get; set; }
    public string? RefereeAssignmentStatus { get; set; }
    public string? RefereeAssignedAt { get; set; }
}
