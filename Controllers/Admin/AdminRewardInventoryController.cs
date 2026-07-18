using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Admin;
using Eliteracingleague.API.Extensions;
using Eliteracingleague.API.Models;
using Eliteracingleague.API.Services.Auditing;
using Eliteracingleague.API.Services.Rewards;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Controllers.Admin;

[ApiController]
[Route("api/admin/reward-inventory")]
[Authorize(Roles = UserRoles.Admin)]
public class AdminRewardInventoryController : ControllerBase
{
    private readonly EliteRacingLeagueContext _context;
    private readonly RewardInventoryService _inventoryService;
    private readonly IAuditService _auditService;

    public AdminRewardInventoryController(
        EliteRacingLeagueContext context,
        RewardInventoryService inventoryService,
        IAuditService auditService)
    {
        _context = context;
        _inventoryService = inventoryService;
        _auditService = auditService;
    }

    [HttpGet]
    public async Task<IActionResult> GetItems(CancellationToken cancellationToken)
    {
        var items = await _context.RewardItems.AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
        return Ok(items.Select(x => new
        {
            x.RewardItemId,
            x.Name,
            x.Sku,
            x.Description,
            x.ImageUrl,
            x.StockQuantity,
            x.ReservedQuantity,
            x.DeliveredQuantity,
            AvailableQuantity = x.StockQuantity - x.ReservedQuantity - x.DeliveredQuantity,
            x.IsActive,
            x.CreatedAt,
            x.UpdatedAt,
            RowVersion = Convert.ToBase64String(x.RowVersion)
        }));
    }

    [HttpPost]
    public async Task<IActionResult> CreateItem(
        CreateRewardItemRequest request,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var adminId)) return Unauthorized();
        var sku = request.Sku.Trim().ToUpperInvariant();
        if (await _context.RewardItems.AnyAsync(x => x.Sku == sku, cancellationToken))
            return Conflict(new { message = "Reward SKU already exists." });

        var now = DateTime.UtcNow;
        var item = new RewardItem
        {
            Name = request.Name.Trim(),
            Sku = sku,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            ImageUrl = string.IsNullOrWhiteSpace(request.ImageUrl) ? null : request.ImageUrl.Trim(),
            StockQuantity = request.InitialStock,
            ReservedQuantity = 0,
            DeliveredQuantity = 0,
            IsActive = true,
            CreatedAt = now
        };
        _context.RewardItems.Add(item);
        await _context.SaveChangesAsync(cancellationToken);
        await _auditService.WriteAsync(adminId, AuditActionTypes.Create,
            "RewardItem", item.RewardItemId.ToString(), null, item, null, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Reward item created.", item.RewardItemId });
    }

    [HttpPut("{id:int}/stock")]
    public async Task<IActionResult> AdjustStock(
        int id,
        AdjustRewardInventoryRequest request,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var adminId)) return Unauthorized();
        if (request.QuantityDelta == 0) return BadRequest(new { message = "Quantity delta cannot be zero." });
        var item = await _context.RewardItems.FirstOrDefaultAsync(x => x.RewardItemId == id, cancellationToken);
        if (item == null) return NotFound(new { message = "Reward item not found." });
        var before = new { item.StockQuantity, item.ReservedQuantity, item.DeliveredQuantity };
        await _inventoryService.AdjustStockAsync(item, request.QuantityDelta, adminId,
            request.Note, DateTime.UtcNow, cancellationToken);
        await _auditService.WriteAsync(adminId, AuditActionTypes.InventoryAdjustment,
            "RewardItem", id.ToString(), before,
            new { item.StockQuantity, item.ReservedQuantity, item.DeliveredQuantity },
            request.Note, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(new
        {
            message = "Inventory adjusted.",
            item.StockQuantity,
            item.ReservedQuantity,
            item.DeliveredQuantity,
            availableQuantity = RewardInventoryService.Available(item)
        });
    }

    [HttpPut("{id:int}/active")]
    public async Task<IActionResult> SetActive(int id, [FromQuery] bool value, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var adminId)) return Unauthorized();
        var item = await _context.RewardItems.FirstOrDefaultAsync(x => x.RewardItemId == id, cancellationToken);
        if (item == null) return NotFound(new { message = "Reward item not found." });
        var old = item.IsActive;
        item.IsActive = value;
        item.UpdatedAt = DateTime.UtcNow;
        await _auditService.WriteAsync(adminId, AuditActionTypes.StatusChange,
            "RewardItem", id.ToString(), new { IsActive = old }, new { IsActive = value }, null, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Reward item status updated.", item.IsActive });
    }
    [HttpPost("expire-overdue")]
    public async Task<IActionResult> ExpireOverdueRewards(CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var adminId)) return Unauthorized();
        var now = DateTime.UtcNow;
        var rewards = await _context.SeasonRewards
            .Include(x => x.RewardItem)
            .Where(x => x.Status == SeasonRewardStatuses.Eligible &&
                        x.ClaimDeadline.HasValue && x.ClaimDeadline.Value < now)
            .ToListAsync(cancellationToken);

        foreach (var reward in rewards)
        {
            if (reward.RewardItem != null && reward.InventoryReserved)
            {
                await _inventoryService.ReleaseAsync(
                    reward.RewardItem, reward, adminId, now,
                    "Claim deadline expired.", cancellationToken);
            }

            reward.Status = SeasonRewardStatuses.Expired;
            reward.AdminNote = "Automatically expired after the claim deadline.";
            await _auditService.WriteAsync(adminId, AuditActionTypes.StatusChange,
                "SeasonReward", reward.SeasonRewardId.ToString(),
                new { Status = SeasonRewardStatuses.Eligible },
                new { Status = SeasonRewardStatuses.Expired },
                "Claim deadline expired.", cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Overdue rewards were processed.", expiredCount = rewards.Count });
    }

}
