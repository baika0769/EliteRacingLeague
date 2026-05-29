using System;
using System.Collections.Generic;

namespace Eliteracingleague.API.Models;

public partial class PrizeRule
{
    public int PrizeRuleId { get; set; }

    public int RaceId { get; set; }

    public int RankPosition { get; set; }

    public decimal PrizeAmount { get; set; }

    public string? Note { get; set; }

    public virtual Race Race { get; set; } = null!;
}
