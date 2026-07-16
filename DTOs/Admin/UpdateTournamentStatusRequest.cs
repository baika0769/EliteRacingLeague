using System.ComponentModel.DataAnnotations;

namespace Eliteracingleague.API.DTOs.Admin;

public class UpdateTournamentStatusRequest
{
    [Required]
    [StringLength(30)]
    public string Status { get; set; } = string.Empty;
}
