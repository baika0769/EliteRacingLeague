using System;
using System.Collections.Generic;

namespace Eliteracingleague.API.Models;

public partial class RefereeReport
{
    public int ReportId { get; set; }

    public int RaceId { get; set; }

    public int RefereeId { get; set; }

    public string ReportContent { get; set; } = null!;

    public DateTime SubmittedAt { get; set; }

    public virtual Race Race { get; set; } = null!;

    public virtual RaceReferee Referee { get; set; } = null!;
}
