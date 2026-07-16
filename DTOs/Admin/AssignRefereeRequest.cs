using System.ComponentModel.DataAnnotations;

namespace Eliteracingleague.API.DTOs.Admin;

public class AssignRefereeRequest
{
    [Range(1, int.MaxValue)]
    public int RefereeId { get; set; }
}
