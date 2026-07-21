using System.ComponentModel.DataAnnotations;

namespace Eliteracingleague.API.DTOs.Admin;

public class CreateRewardItemRequest
{
    [Required, StringLength(200)]
    public string Name { get; set; } = null!;

    [Required, StringLength(80)]
    public string Sku { get; set; } = null!;

    [StringLength(1000)]
    public string? Description { get; set; }

    [StringLength(1000)]
    public string? ImageUrl { get; set; }

    [Range(0, 1_000_000)]
    public int InitialStock { get; set; }
}

public class UpdateRewardItemRequest
{
    [Required, StringLength(200)]
    public string Name { get; set; } = null!;

    [Required, StringLength(80)]
    public string Sku { get; set; } = null!;

    [StringLength(1000)]
    public string? Description { get; set; }

    [StringLength(1000)]
    public string? ImageUrl { get; set; }

    public string? RowVersion { get; set; }
}

public class AdjustRewardInventoryRequest
{
    [Range(-1_000_000, 1_000_000)]
    public int QuantityDelta { get; set; }

    [Required, StringLength(500, MinimumLength = 3)]
    public string Note { get; set; } = null!;
}
