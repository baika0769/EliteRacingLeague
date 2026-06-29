using System;
using System.Collections.Generic;

namespace Eliteracingleague.API.Models;

public partial class Notification
{
    public int NotificationId { get; set; }

    public int UserId { get; set; }

    public string Title { get; set; } = null!;

    public string Message { get; set; } = null!;

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; }

    public string? RelatedType { get; set; }

    public int? RelatedId { get; set; }

    public string? ActionType { get; set; }

    public string? ActionUrl { get; set; }

    public virtual User User { get; set; } = null!;
}
