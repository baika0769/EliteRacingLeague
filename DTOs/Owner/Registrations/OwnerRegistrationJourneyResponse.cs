namespace Eliteracingleague.API.DTOs.Owner.Registrations;

public class OwnerRegistrationJourneyResponse
{
    public int RegistrationId { get; set; }
    public string CurrentStatus { get; set; } = null!;
    public int CurrentStep { get; set; }

    public List<RegistrationJourneyStepResponse> Steps { get; set; } = new();
}

public class RegistrationJourneyStepResponse
{
    public int StepNumber { get; set; }
    public string Key { get; set; } = null!;
    public string Label { get; set; } = null!;
    public string Description { get; set; } = null!;
    public bool IsCompleted { get; set; }
    public bool IsCurrent { get; set; }
}