namespace Eliteracingleague.API.DTOs.Owner;

public class OwnerJockeyAssignmentRegistrationResponse
{
    public int RegistrationId { get; set; }
    public string TournamentName { get; set; } = null!;
    public string RaceName { get; set; } = null!;
    public DateTime RaceDate { get; set; }
    public string? Location { get; set; }
    public int DistanceMeters { get; set; }
    public int HorseId { get; set; }
    public string HorseName { get; set; } = null!;
    public string? HorseImageUrl { get; set; }
    public string? HealthCertificateImageUrl { get; set; }
    public string RegistrationStatus { get; set; } = null!;
    public bool HasOfficialJockey { get; set; }
    public int? OfficialJockeyId { get; set; }
    public string? OfficialJockeyName { get; set; }
}

public class OwnerJockeyAssignmentContextResponse
{
    public int RegistrationId { get; set; }
    public string RegistrationStatus { get; set; } = null!;
    public int TournamentId { get; set; }
    public string TournamentName { get; set; } = null!;
    public int RaceId { get; set; }
    public string RaceName { get; set; } = null!;
    public DateTime RaceDate { get; set; }
    public string? Location { get; set; }
    public int DistanceMeters { get; set; }
    public int HorseId { get; set; }
    public string HorseName { get; set; } = null!;
    public string BreedName { get; set; } = null!;
    public int Age { get; set; }
    public decimal? HeightCm { get; set; }
    public decimal WeightKg { get; set; }
    public string HealthStatus { get; set; } = null!;
    public string? HealthCertificateImageUrl { get; set; }
    public bool HorseIsActive { get; set; }
    public int? AssignedJockeyId { get; set; }
    public string? AssignedJockeyName { get; set; }
    public bool HasOfficialJockey { get; set; }
    public int? OfficialJockeyId { get; set; }
    public string? OfficialJockeyName { get; set; }
    public string AssignmentStatus { get; set; } = null!;
    public bool CanSendInvitation { get; set; }
    public bool CanSignJockey { get; set; }
    public bool CanChangeTournament { get; set; }
}

public class OwnerJockeyCandidateResponse
{
    public int JockeyId { get; set; }
    public string FullName { get; set; } = null!;
    public string? ProfileImageUrl { get; set; }
    public decimal WeightKg { get; set; }
    public int YearsOfExperience { get; set; }
    public string HealthStatus { get; set; } = null!;
    public bool IsActive { get; set; }
    public string UserStatus { get; set; } = null!;
    public string? AvailabilityStatus { get; set; }
    public string DistanceSkillLevel { get; set; } = null!;
    public string? BreedSkillLevel { get; set; }
    public int AvailabilityScore { get; set; }
    public int WeightScore { get; set; }
    public int ExperienceScore { get; set; }
    public int DistanceScore { get; set; }
    public int BreedExperienceScore { get; set; }
    public int TotalScore { get; set; }
    public int RankNo { get; set; }
    public string RecommendationLevel { get; set; } = null!;
    public string PrimaryReason { get; set; } = null!;
    public bool AlreadyInvited { get; set; }
    public string? InvitationStatus { get; set; }
    public bool CanInvite { get; set; }
    public string? CannotInviteReason { get; set; }
}

public class OwnerJockeyCandidateListResponse
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
    public List<OwnerJockeyCandidateResponse> Items { get; set; } = new();
}

public class OwnerJockeyAssignmentSummaryResponse
{
    public int InvitedCount { get; set; }
    public int PendingCount { get; set; }
    public int AcceptedCount { get; set; }
}

public class OwnerJockeyInvitationResponse
{
    public int InvitationId { get; set; }
    public int JockeyId { get; set; }
    public string JockeyName { get; set; } = null!;
    public string? ProfileImageUrl { get; set; }
    public int? ExperienceYears { get; set; }
    public DateTime SentAt { get; set; }
    public DateTime? RespondedAt { get; set; }
    public string Status { get; set; } = null!;
    public bool CanSign { get; set; }
    public bool IsOfficial { get; set; }
}

public class OwnerJockeyInvitationDetailResponse
{
    public int InvitationId { get; set; }
    public int RegistrationId { get; set; }
    public int JockeyId { get; set; }
    public string JockeyName { get; set; } = null!;
    public string? ProfileImageUrl { get; set; }
    public int? ExperienceYears { get; set; }
    public decimal? WeightKg { get; set; }
    public string? HealthStatus { get; set; }
    public DateTime SentAt { get; set; }
    public DateTime? RespondedAt { get; set; }
    public string Status { get; set; } = null!;
    public string HorseName { get; set; } = null!;
    public string TournamentName { get; set; } = null!;
    public DateTime RaceDate { get; set; }
    public string? Message { get; set; }
    public string? ResponseNote { get; set; }
    public bool CanSign { get; set; }
    public bool IsOfficial { get; set; }
}

public class OfficialJockeySelectionResponse
{
    public string Message { get; set; } = null!;
    public int RegistrationId { get; set; }
    public int JockeyId { get; set; }
    public string JockeyName { get; set; } = null!;
    public string RegistrationStatus { get; set; } = null!;
}
