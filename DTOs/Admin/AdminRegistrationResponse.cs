namespace Eliteracingleague.API.DTOs.Admin
{
    public class AdminRegistrationResponse
    {
        public int RegistrationId { get; set; }
        public int RaceId { get; set; }
        public int HorseId { get; set; }
        public int OwnerId { get; set; }
        public int? JockeyId { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime SubmittedAt { get; set; }
        public int? ReviewedBy { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public DateTime? JockeyConfirmedAt { get; set; }
        public string? AdminNote { get; set; }
    }
}