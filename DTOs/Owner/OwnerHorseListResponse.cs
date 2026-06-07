namespace Eliteracingleague.API.DTOs.Owner;

public class OwnerHorseListResponse
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }

    public List<OwnerHorseResponse> Items { get; set; } = new();
}