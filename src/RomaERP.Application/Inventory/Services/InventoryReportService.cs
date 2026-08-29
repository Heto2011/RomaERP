using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.Inventory.DTOs;
using RomaERP.Domain.Inventory;

namespace RomaERP.Application.Inventory.Services;

public class InventoryReportService : IInventoryReportService
{
    private const decimal ExcessStockDaysThreshold = 90m;

    private readonly IApplicationDbContext _context;

    public InventoryReportService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<StockValuationReportDto> GetStockValuationAsync(CancellationToken ct = default)
    {
        var items = await _context.Items
            .AsNoTracking()
            .Include(i => i.ItemCategory)
            .Where(i => !i.IsDeleted)
            .Select(i => new StockValuationLineDto
            {
                ItemId = i.Id,
                ItemCode = i.Code,
                ItemName = i.NameAr,
                CategoryName = i.ItemCategory!.NameAr,
                QuantityOnHand = i.QuantityOnHand,
                AverageCost = i.AverageCost,
                Value = Math.Round(i.QuantityOnHand * i.AverageCost, 2)
            })
            .Where(l => l.QuantityOnHand != 0)
            .OrderByDescending(l => l.Value)
            .ToListAsync(ct);

        return new StockValuationReportDto
        {
            AsOfDate = DateTime.UtcNow,
            Items = items,
            TotalValue = items.Sum(i => i.Value)
        };
    }

    public async Task<InventoryMovementReportDto> GetInventoryMovementAsync(DateTime fromDate, DateTime toDate, CancellationToken ct = default)
    {
        var items = await _context.Items
            .AsNoTracking()
            .Where(i => !i.IsDeleted && i.IsActive)
            .ToListAsync(ct);

        var issueMovements = await _context.StockMovements
            .AsNoTracking()
            .Where(m => m.MovementType == StockMovementType.Issue
                        && m.MovementDate >= fromDate
                        && m.MovementDate <= toDate)
            .ToListAsync(ct);

        var issuedByItem = issueMovements
            .GroupBy(m => m.ItemId)
            .ToDictionary(g => g.Key, g => (Quantity: g.Sum(m => m.Quantity), Cogs: g.Sum(m => m.TotalCost)));

        var periodDays = Math.Max(1, (toDate.Date - fromDate.Date).Days + 1);

        var lines = items.Select(item =>
        {
            issuedByItem.TryGetValue(item.Id, out var issued);
            var stockValue = Math.Round(item.QuantityOnHand * item.AverageCost, 2);
            var dailyVelocity = issued.Quantity / periodDays;
            decimal? daysOfStockRemaining = dailyVelocity > 0 ? Math.Round(item.QuantityOnHand / dailyVelocity, 1) : null;
            var turnoverRate = stockValue > 0 ? Math.Round(issued.Cogs / stockValue, 2) : 0;

            return new InventoryMovementLineDto
            {
                ItemId = item.Id,
                ItemCode = item.Code,
                ItemName = item.NameAr,
                QuantityOnHand = item.QuantityOnHand,
                ReorderLevel = item.ReorderLevel,
                StockValue = stockValue,
                QuantityIssuedInPeriod = issued.Quantity,
                CogsInPeriod = issued.Cogs,
                DaysOfStockRemaining = daysOfStockRemaining,
                TurnoverRate = turnoverRate,
                IsAtRiskOfStockout = item.ReorderLevel > 0 && item.QuantityOnHand <= item.ReorderLevel,
                IsDeadStock = item.QuantityOnHand > 0 && issued.Quantity == 0,
                IsExcessStock = daysOfStockRemaining.HasValue && daysOfStockRemaining.Value > ExcessStockDaysThreshold
            };
        })
        .Where(l => l.QuantityOnHand != 0 || l.QuantityIssuedInPeriod != 0)
        .ToList();

        return new InventoryMovementReportDto { FromDate = fromDate, ToDate = toDate, Items = lines };
    }

    public async Task<PurchasePriceVarianceReportDto> GetPurchasePriceVarianceAsync(DateTime fromDate, DateTime toDate, CancellationToken ct = default)
    {
        var receipts = await _context.StockMovements
            .AsNoTracking()
            .Include(m => m.Item)
            .Where(m => m.MovementType == StockMovementType.Receipt)
            .OrderBy(m => m.MovementDate)
            .ToListAsync(ct);

        var lines = new List<PurchasePriceVarianceLineDto>();
        foreach (var group in receipts.GroupBy(r => r.ItemId))
        {
            var ordered = group.OrderBy(r => r.MovementDate).ToList();
            for (var i = 1; i < ordered.Count; i++)
            {
                var latest = ordered[i];
                if (latest.MovementDate < fromDate || latest.MovementDate > toDate) continue;

                var previous = ordered[i - 1];
                var changeAmount = latest.UnitCost - previous.UnitCost;
                lines.Add(new PurchasePriceVarianceLineDto
                {
                    ItemId = latest.ItemId,
                    ItemCode = latest.Item!.Code,
                    ItemName = latest.Item.NameAr,
                    PreviousReceiptDate = previous.MovementDate,
                    PreviousUnitCost = previous.UnitCost,
                    LatestReceiptDate = latest.MovementDate,
                    LatestUnitCost = latest.UnitCost,
                    ChangeAmount = changeAmount,
                    ChangePercent = previous.UnitCost != 0 ? Math.Round(changeAmount / previous.UnitCost * 100, 2) : 0
                });
            }
        }

        return new PurchasePriceVarianceReportDto
        {
            FromDate = fromDate,
            ToDate = toDate,
            Items = lines.OrderByDescending(l => Math.Abs(l.ChangePercent)).ToList()
        };
    }

    public async Task<RecipeCostReportDto> GetRecipeCostAsync(CancellationToken ct = default)
    {
        var menuItems = await _context.Items
            .AsNoTracking()
            .Where(i => !i.IsDeleted && i.IsMenuItem)
            .ToListAsync(ct);
        var menuItemIds = menuItems.Select(i => i.Id).ToList();

        var recipeLines = await _context.MenuRecipeLines
            .AsNoTracking()
            .Include(r => r.RawMaterialItem)
            .Where(r => menuItemIds.Contains(r.MenuItemId))
            .ToListAsync(ct);
        var recipeCostByMenuItem = recipeLines
            .GroupBy(r => r.MenuItemId)
            .ToDictionary(g => g.Key, g => g.Sum(r => r.QuantityPerUnit * r.RawMaterialItem!.AverageCost));

        var lines = menuItems.Select(item =>
        {
            var hasRecipe = recipeCostByMenuItem.TryGetValue(item.Id, out var recipeCost);
            var cost = hasRecipe ? recipeCost : item.AverageCost;
            var grossProfit = item.MenuPrice - cost;

            return new RecipeCostLineDto
            {
                ItemId = item.Id,
                ItemCode = item.Code,
                ItemName = item.NameAr,
                HasRecipe = hasRecipe,
                RecipeCost = Math.Round(cost, 2),
                MenuPrice = item.MenuPrice,
                GrossProfit = Math.Round(grossProfit, 2),
                MarginPercent = item.MenuPrice != 0 ? Math.Round(grossProfit / item.MenuPrice * 100, 2) : 0
            };
        })
        .OrderBy(l => l.ItemName)
        .ToList();

        return new RecipeCostReportDto { Items = lines };
    }
}
