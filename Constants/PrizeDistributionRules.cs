namespace Eliteracingleague.API.Constants;

/// <summary>
/// Default race-prize split. PrizeAward.PrizeAmount remains the total amount for
/// the finishing position; recipient payouts are derived from that total.
/// Change these two values together if the business rule changes.
/// </summary>
public static class PrizeDistributionRules
{
    public const decimal OwnerSharePercent = 80m;
    public const decimal JockeySharePercent = 20m;

    public static (decimal OwnerAmount, decimal JockeyAmount) Split(
        decimal totalAmount,
        bool hasJockey)
    {
        if (totalAmount < 0)
            throw new ArgumentOutOfRangeException(nameof(totalAmount));

        if (!hasJockey)
            return (totalAmount, 0m);

        var ownerAmount = Math.Round(
            totalAmount * OwnerSharePercent / 100m,
            2,
            MidpointRounding.AwayFromZero);
        var jockeyAmount = totalAmount - ownerAmount;
        return (ownerAmount, jockeyAmount);
    }
}
