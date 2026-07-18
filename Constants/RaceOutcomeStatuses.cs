namespace Eliteracingleague.API.Constants;

public static class RaceOutcomeStatuses
{
    public const string Finished = "Finished";
    public const string DidNotStart = "DNS";
    public const string DidNotFinish = "DNF";
    public const string Disqualified = "DSQ";
    public const string Withdrawn = "Withdrawn";

    public static readonly string[] All =
    {
        Finished,
        DidNotStart,
        DidNotFinish,
        Disqualified,
        Withdrawn
    };

    public static bool IsValid(string? status) =>
        !string.IsNullOrWhiteSpace(status) && All.Contains(status);

    public static bool RequiresFinishPosition(string? status) => status == Finished;
    public static bool IsRankable(string? status) => status == Finished;
}
