namespace Eliteracingleague.API.Constants;


[Obsolete("Use RacePredictionStatuses instead.")]
public static class PredictionStatuses
{
    public const string Pending = RacePredictionStatuses.Pending;
    public const string Locked = RacePredictionStatuses.Locked;
    public const string Evaluated = RacePredictionStatuses.Evaluated;
    public const string Cancelled = RacePredictionStatuses.Cancelled;

    public static readonly string[] All = RacePredictionStatuses.All;
}
