using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.DTOs.Admin;
using Eliteracingleague.API.Extensions;
using Eliteracingleague.API.Models;
using Eliteracingleague.API.Services.Auditing;
using Eliteracingleague.API.Services.Rewards;
using Eliteracingleague.API.Services.SystemTime;
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
    private readonly IDateTimeProvider _dateTimeProvider;

    public AdminRewardInventoryController(
        EliteRacingLeagueContext context,
        RewardInventoryService inventoryService,
        IAuditService auditService,
        IDateTimeProvider dateTimeProvider)
    {
        _context = context;
        _inventoryService = inventoryService;
        _auditService = auditService;
        _dateTimeProvider = dateTimeProvider;
    }

    [HttpGet]
    public async Task<IActionResult> GetItems(CancellationToken cancellationToken)
    {
        var items = await _context.RewardItems.AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return Ok(items.Select(ToResponse));
    }

    [HttpPost]
    public async Task<IActionResult> CreateItem(
        CreateRewardItemRequest request,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var adminId)) return Unauthorized();

        var sku = request.Sku.Trim().ToUpperInvariant();
        if (await _context.RewardItems.AnyAsync(x => x.Sku == sku, cancellationToken))
        {
            return Conflict(new { message = "Reward SKU already exists." });
        }

        var now = _dateTimeProvider.UtcNow;
        var item = new RewardItem
        {
            Name = request.Name.Trim(),
            Sku = sku,
            Description = NormalizeOptional(request.Description),
            ImageUrl = NormalizeOptional(request.ImageUrl),
            StockQuantity = request.InitialStock,
            ReservedQuantity = 0,
            DeliveredQuantity = 0,
            IsActive = true,
            CreatedAt = now
        };

        _context.RewardItems.Add(item);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.WriteAsync(
            adminId,
            AuditActionTypes.Create,
            "RewardItem",
            item.RewardItemId.ToString(),
            null,
            new
            {
                item.Name,
                item.Sku,
                item.Description,
                item.ImageUrl,
                item.StockQuantity,
                item.IsActive
            },
            null,
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "Reward item created.",
            item = ToResponse(item)
        });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateItem(
        int id,
        UpdateRewardItemRequest request,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var adminId)) return Unauthorized();

        var item = await _context.RewardItems
            .FirstOrDefaultAsync(x => x.RewardItemId == id, cancellationToken);

        if (item == null)
        {
            return NotFound(new { message = "Reward item not found." });
        }

        var sku = request.Sku.Trim().ToUpperInvariant();
        var duplicateSku = await _context.RewardItems.AnyAsync(
            x => x.RewardItemId != id && x.Sku == sku,
            cancellationToken);

        if (duplicateSku)
        {
            return Conflict(new { message = "Reward SKU already exists." });
        }

        if (!string.IsNullOrWhiteSpace(request.RowVersion))
        {
            if (!TryDecodeRowVersion(request.RowVersion, out var originalRowVersion))
            {
                return BadRequest(new { message = "The reward item version is invalid. Open the edit form again and try once more." });
            }

            _context.Entry(item).Property(x => x.RowVersion).OriginalValue = originalRowVersion;
        }
        else
        {
            _context.Entry(item).Property(x => x.RowVersion).OriginalValue = item.RowVersion;
        }

        var before = new
        {
            item.Name,
            item.Sku,
            item.Description,
            item.ImageUrl,
            item.IsActive
        };

        item.Name = request.Name.Trim();
        item.Sku = sku;
        item.Description = NormalizeOptional(request.Description);
        item.ImageUrl = NormalizeOptional(request.ImageUrl);
        item.UpdatedAt = _dateTimeProvider.UtcNow;

        await _auditService.WriteAsync(
            adminId,
            AuditActionTypes.Update,
            "RewardItem",
            item.RewardItemId.ToString(),
            before,
            new
            {
                item.Name,
                item.Sku,
                item.Description,
                item.ImageUrl,
                item.IsActive
            },
            null,
            cancellationToken);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new
            {
                message = "This reward item was changed by another admin. Refresh the inventory and try again."
            });
        }

        return Ok(new
        {
            message = "Reward item information updated.",
            item = ToResponse(item)
        });
    }

    [HttpPut("{id:int}/stock")]
    public async Task<IActionResult> AdjustStock(
        int id,
        AdjustRewardInventoryRequest request,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var adminId)) return Unauthorized();
        if (request.QuantityDelta == 0)
        {
            return BadRequest(new { message = "Quantity delta cannot be zero." });
        }

        var item = await _context.RewardItems
            .FirstOrDefaultAsync(x => x.RewardItemId == id, cancellationToken);

        if (item == null)
        {
            return NotFound(new { message = "Reward item not found." });
        }

        var before = new
        {
            item.StockQuantity,
            item.ReservedQuantity,
            item.DeliveredQuantity
        };

        var now = _dateTimeProvider.UtcNow;
        await _inventoryService.AdjustStockAsync(
            item,
            request.QuantityDelta,
            adminId,
            request.Note,
            now,
            cancellationToken);

        await _auditService.WriteAsync(
            adminId,
            AuditActionTypes.InventoryAdjustment,
            "RewardItem",
            id.ToString(),
            before,
            new
            {
                item.StockQuantity,
                item.ReservedQuantity,
                item.DeliveredQuantity
            },
            request.Note,
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "Inventory adjusted.",
            item.StockQuantity,
            item.ReservedQuantity,
            item.DeliveredQuantity,
            availableQuantity = RewardInventoryService.Available(item),
            RowVersion = Convert.ToBase64String(item.RowVersion)
        });
    }

    [HttpPut("{id:int}/active")]
    public async Task<IActionResult> SetActive(
        int id,
        [FromQuery] bool value,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var adminId)) return Unauthorized();

        var item = await _context.RewardItems
            .FirstOrDefaultAsync(x => x.RewardItemId == id, cancellationToken);

        if (item == null)
        {
            return NotFound(new { message = "Reward item not found." });
        }

        var old = item.IsActive;
        item.IsActive = value;
        item.UpdatedAt = _dateTimeProvider.UtcNow;

        await _auditService.WriteAsync(
            adminId,
            AuditActionTypes.StatusChange,
            "RewardItem",
            id.ToString(),
            new { IsActive = old },
            new { IsActive = value },
            null,
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "Reward item status updated.",
            item.IsActive,
            RowVersion = Convert.ToBase64String(item.RowVersion)
        });
    }

    [HttpPost("expire-overdue")]
    public async Task<IActionResult> ExpireOverdueRewards(CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var adminId)) return Unauthorized();

        // Use the effective system time so the Admin System Time override works
        // consistently during testing and demonstrations.
        var now = _dateTimeProvider.UtcNow;
        var rewards = await _context.SeasonRewards
            .Include(x => x.RewardItem)
            .Where(x => x.Status == SeasonRewardStatuses.Eligible &&
                        x.ClaimDeadline.HasValue &&
                        x.ClaimDeadline.Value < now)
            .ToListAsync(cancellationToken);

        foreach (var reward in rewards)
        {
            if (reward.RewardItem != null && reward.InventoryReserved)
            {
                await _inventoryService.ReleaseAsync(
                    reward.RewardItem,
                    reward,
                    adminId,
                    now,
                    "Claim deadline expired.",
                    cancellationToken);
            }

            reward.Status = SeasonRewardStatuses.Expired;
            reward.AdminNote = "Automatically expired after the claim deadline.";

            await _auditService.WriteAsync(
                adminId,
                AuditActionTypes.StatusChange,
                "SeasonReward",
                reward.SeasonRewardId.ToString(),
                new { Status = SeasonRewardStatuses.Eligible },
                new { Status = SeasonRewardStatuses.Expired },
                "Claim deadline expired.",
                cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "Overdue rewards were processed.",
            expiredCount = rewards.Count,
            effectiveUtcNow = now
        });
    }

    private static object ToResponse(RewardItem item)
    {
        return new
        {
            item.RewardItemId,
            item.Name,
            item.Sku,
            item.Description,
            item.ImageUrl,
            item.StockQuantity,
            item.ReservedQuantity,
            item.DeliveredQuantity,
            AvailableQuantity = RewardInventoryService.Available(item),
            item.IsActive,
            item.CreatedAt,
            item.UpdatedAt,
            RowVersion = Convert.ToBase64String(item.RowVersion)
        };
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool TryDecodeRowVersion(string? value, out byte[] rowVersion)
    {
        rowVersion = Array.Empty<byte>();

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            rowVersion = Convert.FromBase64String(value.Trim());
            return rowVersion.Length > 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
