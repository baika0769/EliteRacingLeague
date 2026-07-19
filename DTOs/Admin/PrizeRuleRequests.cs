using System.ComponentModel.DataAnnotations;

namespace Eliteracingleague.API.DTOs.Admin;

public sealed class UpsertPrizeRulesRequest
{
    /// <summary>
    /// Complete prize-rule list for the race. Sending an empty list removes all rules,
    /// provided the race has not started and no prize awards have been generated.
    /// </summary>
    [Required]
    [MaxLength(100)]
    public List<PrizeRuleItemRequest> Rules { get; set; } = new();
}

public sealed class PrizeRuleItemRequest
{
    [Range(1, 100)]
    public int RankPosition { get; set; }

    public decimal PrizeAmount { get; set; }

    [StringLength(255)]
    public string? Note { get; set; }
}
