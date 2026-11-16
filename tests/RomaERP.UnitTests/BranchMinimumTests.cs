using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Inventory.DTOs;
using RomaERP.Application.Inventory.Services;
using RomaERP.Domain.Inventory;
using RomaERP.Infrastructure.Persistence;
using Xunit;

namespace RomaERP.UnitTests;

public class BranchMinimumTests
{
    private record S(ApplicationDbContext Ctx, Warehouse Cairo, Warehouse Alex, Warehouse Mans, Item Tomato);

    private static async Task<S> CreateAsync(decimal cairo, decimal alex, decimal mans)
    {
        var ctx = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var cat = new ItemCategory { Code = "C", NameAr = "خامات", NameEn = "Raw" };
        var c = new Warehouse { Code = "CAI", NameAr = "القاهرة", NameEn = "Cairo" };
        var a = new Warehouse { Code = "ALX", NameAr = "اسكندرية", NameEn = "Alex" };
        var m = new Warehouse { Code = "MNS", NameAr = "المنصورة", NameEn = "Mansoura" };
        var t = new Item { Code = "TOM", NameAr = "طماطم", NameEn = "Tomato", UnitOfMeasure = "kg", ItemCategory = cat, AverageCost = 10m };
        ctx.AddRange(cat, c, a, m, t);
        var n = 0;
        foreach (var (w, q) in new[] { (c, cairo), (a, alex), (m, mans) })
            if (q > 0) ctx.StockMovements.Add(new StockMovement { MovementNumber = $"SM-{++n:D6}", MovementDate = DateTime.UtcNow, MovementType = StockMovementType.Receipt, ItemId = t.Id, WarehouseId = w.Id, Quantity = q, UnitCost = 10m, TotalCost = q * 10m });
        await ctx.SaveChangesAsync();
        return new S(ctx, c, a, m, t);
    }

    private static Task Min(BranchMinimumService svc, S s, Warehouse w, decimal q) => svc.SetAsync(new SetBranchMinimumDto { WarehouseId = w.Id, ItemId = s.Tomato.Id, MinimumQuantity = q });

    [Fact]
    public async Task BranchBelowItsMinimum_IsAlerted_AndToldWhichBranchCanCoverIt()
    {
        var s = await CreateAsync(cairo: 80, alex: 8, mans: 2);
        var svc = new BranchMinimumService(s.Ctx);
        await Min(svc, s, s.Alex, 20); await Min(svc, s, s.Cairo, 30);
        var alert = Assert.Single(await svc.GetAlertsAsync());
        Assert.Equal("اسكندرية", alert.WarehouseName);
        Assert.Equal(12m, alert.Shortage);
        var suggestion = Assert.Single(alert.Suggestions);
        Assert.Equal("القاهرة", suggestion.FromWarehouseName);   // 80 - its own minimum 30 = 50 spare
        Assert.Equal(12m, suggestion.Quantity);
        Assert.Equal(0m, alert.ToBuy);
    }

    [Fact]
    public async Task WhenNoBranchHasSpare_TheShortageHasToBeBought()
    {
        var s = await CreateAsync(cairo: 35, alex: 5, mans: 0);
        var svc = new BranchMinimumService(s.Ctx);
        await Min(svc, s, s.Alex, 20); await Min(svc, s, s.Cairo, 30);
        var alert = Assert.Single(await svc.GetAlertsAsync());
        Assert.Equal(15m, alert.Shortage);
        Assert.Equal(5m, alert.Suggestions.Single().Quantity);   // Cairo only has 5 above its minimum
        Assert.Equal(10m, alert.ToBuy);
    }

    [Fact]
    public async Task StockAlreadyOnItsWay_CountsAgainstTheShortage()
    {
        var s = await CreateAsync(cairo: 80, alex: 8, mans: 0);
        var svc = new BranchMinimumService(s.Ctx);
        await Min(svc, s, s.Alex, 20);
        s.Ctx.StockTransfers.Add(new StockTransfer { Number = "TR-000001", FromWarehouseId = s.Cairo.Id, ToWarehouseId = s.Alex.Id, Status = StockTransferStatus.InTransit, Lines = { new StockTransferLine { ItemId = s.Tomato.Id, Quantity = 12, UnitCost = 10m } } });
        await s.Ctx.SaveChangesAsync();
        Assert.Empty(await svc.GetAlertsAsync());                 // 8 + 12 coming = 20
    }

    [Fact]
    public async Task NoMinimum_NoAlert_AndZeroRemovesAMinimum()
    {
        var s = await CreateAsync(cairo: 0, alex: 1, mans: 0);
        var svc = new BranchMinimumService(s.Ctx);
        Assert.Empty(await svc.GetAlertsAsync());
        await Min(svc, s, s.Alex, 20);
        Assert.Single(await svc.GetAlertsAsync());
        await Min(svc, s, s.Alex, 0);
        Assert.Empty(await svc.GetAlertsAsync());
        Assert.Empty(await svc.GetAllAsync());
    }

    [Fact]
    public async Task AUserTiedToABranch_SeesAndSetsOnlyThatBranch()
    {
        var s = await CreateAsync(cairo: 1, alex: 1, mans: 1);
        var svc = new BranchMinimumService(s.Ctx, new FakeBranchAccess(s.Alex.Id));
        await Assert.ThrowsAsync<ValidationAppException>(() => Min(svc, s, s.Cairo, 10));
        await Min(svc, s, s.Alex, 10);
        var admin = new BranchMinimumService(s.Ctx);
        await Min(admin, s, s.Cairo, 10);
        Assert.Single(await svc.GetAlertsAsync());                // only its own branch
        Assert.Equal(2, (await admin.GetAlertsAsync()).Count);
    }
}
