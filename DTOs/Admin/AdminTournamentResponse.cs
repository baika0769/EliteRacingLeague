namespace Eliteracingleague.API.DTOs.Admin
{
    public class AdminTournamentResponse
    {
        public int TournamentId { get; set; }
        public string TournamentName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Location { get; set; } = string.Empty;
        public DateOnly RegistrationDeadline { get; set; }
        public DateOnly RaceDate { get; set; }
        public int MaxHorses { get; set; }
        public decimal? PrizePool { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public string? Rules { get; set; }
        public int CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public int EntriesCount { get; set; }
        public string EntriesText { get; set; } = string.Empty;
        public string? Referee { get; set; }
        public int? RaceId { get; set; }
        public DateTime? RaceDateTime { get; set; }
        public string? RaceStartTime { get; set; }
        public int? DistanceMeters { get; set; }
        public string? RaceStatus { get; set; }
    }
}