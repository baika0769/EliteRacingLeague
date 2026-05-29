using System;
using System.Collections.Generic;

namespace Eliteracingleague.API.Models;

public partial class PreRaceInspection
{
    public int InspectionId { get; set; }

    public int RaceId { get; set; }

    public int RegistrationId { get; set; }

    public int RefereeId { get; set; }

    public string Status { get; set; } = null!;

    public string? Note { get; set; }

    public DateTime InspectedAt { get; set; }

    public virtual Race Race { get; set; } = null!;

    public virtual RaceReferee Referee { get; set; } = null!;

    public virtual RaceRegistration Registration { get; set; } = null!;
}
