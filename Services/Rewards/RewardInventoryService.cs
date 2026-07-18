using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Services.Rewards;

public class RewardInventoryService
{
    private readonly EliteRacingLeagueContext _context;

    public RewardInventoryService(EliteRacingLeagueContext context)
    {
        _context = context;
    }

    public static int Available(RewardItem item) =>
        Math.Max(0, item.StockQuantity - item.ReservedQuantity - item.DeliveredQuantity);

    public async Task<bool> ReserveAsync(
        RewardItem item,
        SeasonReward reward,
        int quantity,
        int actorUserId,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        if (quantity <= 0) throw new InvalidOperationException("Reward quantity must be positive.");
        var key = $"SEASON_REWARD_RESERVE_{reward.SeasonId}_{reward.SpectatorId}_{item.RewardItemId}";
        if (await ExistsAsync(key, cancellationToken)) return false;
        if (!item.IsActive) throw new InvalidOperationException($"Reward item '{item.Name}' is inactive.");
        if (Available(item) < quantity) throw new InvalidOperationException($"Insufficient stock for reward item '{item.Name}'.");

        item.ReservedQuantity += quantity;
        item.UpdatedAt = now;
        reward.RewardItemId = item.RewardItemId;
        reward.Quantity = quantity;
        reward.InventoryReserved = true;
        AddTransaction(item, 0, quantity, 0, RewardInventoryTransactionTypes.Reserve,
            "SeasonReward", reward.SeasonRewardId == 0 ? null : reward.SeasonRewardId,
            key, "Reserve stock for season reward.", actorUserId, now);
        return true;
    }

    public async Task DeliverAsync(
        RewardItem item,
        SeasonReward reward,
        int actorUserId,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        if (!reward.InventoryReserved) throw new InvalidOperationException("Reward inventory was not reserved.");
        var key = $"SEASON_REWARD_DELIVER_{reward.SeasonRewardId}";
        if (await ExistsAsync(key, cancellationToken)) return;
        if (item.ReservedQuantity < reward.Quantity) throw new InvalidOperationException("Reserved inventory is inconsistent.");

        item.ReservedQuantity -= reward.Quantity;
        item.DeliveredQuantity += reward.Quantity;
        item.UpdatedAt = now;
        reward.InventoryReserved = false;
        AddTransaction(item, 0, -reward.Quantity, reward.Quantity,
            RewardInventoryTransactionTypes.Deliver, "SeasonReward", reward.SeasonRewardId,
            key, "Reward delivered to spectator.", actorUserId, now);
    }

    public async Task ReleaseAsync(
        RewardItem item,
        SeasonReward reward,
        int actorUserId,
        DateTime now,
        string note,
        CancellationToken cancellationToken = default)
    {
        if (!reward.InventoryReserved) return;
        var key = $"SEASON_REWARD_RELEASE_{reward.SeasonRewardId}";
        if (await ExistsAsync(key, cancellationToken)) return;
        if (item.ReservedQuantity < reward.Quantity) throw new InvalidOperationException("Reserved inventory is inconsistent.");

        item.ReservedQuantity -= reward.Quantity;
        item.UpdatedAt = now;
        reward.InventoryReserved = false;
        AddTransaction(item, 0, -reward.Quantity, 0,
            RewardInventoryTransactionTypes.Release, "SeasonReward", reward.SeasonRewardId,
            key, note, actorUserId, now);
    }

    public async Task AdjustStockAsync(
        RewardItem item,
        int quantityDelta,
        int actorUserId,
        string note,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        var after = checked(item.StockQuantity + quantityDelta);
        if (after < item.ReservedQuantity + item.DeliveredQuantity)
            throw new InvalidOperationException("Stock cannot be lower than reserved plus delivered quantity.");

        item.StockQuantity = after;
        item.UpdatedAt = now;
        AddTransaction(item, quantityDelta, 0, 0,
            RewardInventoryTransactionTypes.ManualAdjustment, "RewardItem", item.RewardItemId,
            $"REWARD_STOCK_ADJUST_{item.RewardItemId}_{Guid.NewGuid():N}", note, actorUserId, now);
        await Task.CompletedTask;
    }

    private async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken) =>
        _context.RewardInventoryTransactions.Local.Any(x => x.IdempotencyKey == key) ||
        await _context.RewardInventoryTransactions.AsNoTracking()
            .AnyAsync(x => x.IdempotencyKey == key, cancellationToken);

    private void AddTransaction(
        RewardItem item,
        int quantityDelta,
        int reservedDelta,
        int deliveredDelta,
        string type,
        string? referenceType,
        int? referenceId,
        string key,
        string? note,
        int actorUserId,
        DateTime now)
    {
        _context.RewardInventoryTransactions.Add(new RewardInventoryTransaction
        {
            RewardItem = item,
            QuantityDelta = quantityDelta,
            ReservedDelta = reservedDelta,
            DeliveredDelta = deliveredDelta,
            TransactionType = type,
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            IdempotencyKey = key,
            Note = note,
            CreatedByUserId = actorUserId,
            CreatedAt = now
        });
    }
}
