namespace Eliteracingleague.API.DTOs.Jockey.Calendar;

public class UpdateJockeyAvailabilitiesRequest
{
    public List<UpdateJockeyAvailabilityItemRequest> Items { get; set; } = new();
}
