using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Models;

namespace Eliteracingleague.API.Services.Rewards;

public sealed class PrizePayoutService
{
    public void CreateRecipientPayouts(PrizeAward award, DateTime now)
    {
        var (ownerAmount, jockeyAmount) = PrizeDistributionRules.Split(
            award.PrizeAmount,
            award.JockeyId.HasValue);

        award.Payouts.Add(new PrizePayout
        {
            RecipientUserId = award.OwnerId,
            RecipientType = PrizePayoutRecipientTypes.Owner,
            Amount = ownerAmount,
            Status = PrizeAwardStatuses.ReadyToClaim,
            CreatedAt = now,
            UpdatedAt = now
        });

        if (award.JockeyId.HasValue && jockeyAmount > 0)
        {
            award.Payouts.Add(new PrizePayout
            {
                RecipientUserId = award.JockeyId.Value,
                RecipientType = PrizePayoutRecipientTypes.Jockey,
                Amount = jockeyAmount,
                Status = PrizeAwardStatuses.ReadyToClaim,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
    }

    public static void SynchronizeAggregateStatus(PrizeAward award, DateTime now)
    {
        var payouts = award.Payouts.ToList();
        if (payouts.Count == 0)
            return;

        var allTerminal = payouts.All(item =>
            item.Status is PrizeAwardStatuses.Paid or PrizeAwardStatuses.Rejected);

        if (allTerminal && payouts.Any(item => item.Status == PrizeAwardStatuses.Paid))
        {
            award.Status = PrizeAwardStatuses.Paid;
            award.PaidAt = payouts.Max(item => item.PaidAt) ?? now;
            return;
        }

        award.PaidAt = null;

        if (payouts.All(item => item.Status == PrizeAwardStatuses.Rejected))
        {
            award.Status = PrizeAwardStatuses.Rejected;
        }
        else if (payouts.Any(item => item.Status == PrizeAwardStatuses.UnderReview))
        {
            award.Status = PrizeAwardStatuses.UnderReview;
        }
        else
        {
            award.Status = PrizeAwardStatuses.ReadyToClaim;
        }
    }
}
