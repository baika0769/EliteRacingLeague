using System.ComponentModel.DataAnnotations;

namespace Eliteracingleague.API.DTOs.Admin;

public class AdminRejectRegistrationRequest
{
    [Required]
    [StringLength(500, MinimumLength = 5)]
    public string AdminNote { get; set; } = string.Empty;
}
