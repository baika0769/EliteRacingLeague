namespace Eliteracingleague.API.Constants;

public static class PrizePayoutRecipientTypes
{
    public const string Owner = "Owner";
    public const string Jockey = "Jockey";

    public static readonly string[] All = { Owner, Jockey };

    public static bool IsValid(string? value) =>
        !string.IsNullOrWhiteSpace(value) && All.Contains(value);
}
