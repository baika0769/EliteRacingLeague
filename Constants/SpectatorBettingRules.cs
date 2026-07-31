namespace Eliteracingleague.API.Constants;

public static class SpectatorBettingRules
{
    public const int InitialBettingPoints = 1000;
    public const int MinimumStakePoints = 10;
    public const int WinProfitMultiplier = 2;
    public const int WinGrossPayoutMultiplier = 1 + WinProfitMultiplier;

    public static int CalculateWinGrossPayout(int stakePoints) =>
        checked(stakePoints * WinGrossPayoutMultiplier);

    public static int CalculateWinScoreDelta(int stakePoints) =>
        checked(stakePoints * WinProfitMultiplier);

    public static int CalculateLossScoreDelta(int stakePoints) =>
        checked(-stakePoints);
}
