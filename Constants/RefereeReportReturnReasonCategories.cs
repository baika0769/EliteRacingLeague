namespace Eliteracingleague.API.Constants;

public static class RefereeReportReturnReasonCategories
{
    public const string MissingInformation = "MissingInformation";
    public const string DataMismatch = "DataMismatch";
    public const string MissingViolationDetails = "MissingViolationDetails";
    public const string IncorrectRaceInformation = "IncorrectRaceInformation";
    public const string MissingEvidence = "MissingEvidence";
    public const string UnclearConclusion = "UnclearConclusion";
    public const string Other = "Other";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        MissingInformation,
        DataMismatch,
        MissingViolationDetails,
        IncorrectRaceInformation,
        MissingEvidence,
        UnclearConclusion,
        Other
    };

    public static bool IsValid(string? category) =>
        !string.IsNullOrWhiteSpace(category) && All.Contains(category.Trim());
}
