namespace Eliteracingleague.API.DTOs.Admin
{
    public class AdminRegistrationResponse
    {
        public int RegistrationId { get; set; }

        public int RaceId { get; set; }
        public string RaceName { get; set; } = string.Empty;
        public DateTime RaceDate { get; set; }
        public int DistanceMeters { get; set; }
        public string RaceStatus { get; set; } = string.Empty;

        public int TournamentId { get; set; }
        public string TournamentName { get; set; } = string.Empty;
        public string TournamentLocation { get; set; } = string.Empty;

        public int HorseId { get; set; }
        public string HorseName { get; set; } = string.Empty;
        public string BreedName { get; set; } = string.Empty;
        public int Age { get; set; }
        public decimal? HeightCm { get; set; }
        public decimal WeightKg { get; set; }
        public string HealthStatus { get; set; } = string.Empty;
        public bool HorseIsActive { get; set; }
        public string? HorseImageUrl { get; set; }

        public int OwnerId { get; set; }
        public string OwnerName { get; set; } = string.Empty;
        public string OwnerEmail { get; set; } = string.Empty;
        public string? OwnerPhone { get; set; }

        public int? JockeyId { get; set; }
        public string? JockeyName { get; set; }

        public string Status { get; set; } = string.Empty;
        public DateTime SubmittedAt { get; set; }

        public int? ReviewedBy { get; set; }
        public string? ReviewedByName { get; set; }
        public DateTime? ReviewedAt { get; set; }

        public DateTime? JockeyConfirmedAt { get; set; }
        public string? AdminNote { get; set; }
    }
}