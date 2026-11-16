using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Inventory.DTOs;
using RomaERP.Application.Inventory.Services;
using RomaERP.Domain.Accounting;
using RomaERP.Domain.Inventory;
using RomaERP.Infrastructure.Persistence;
using Xunit;

namespace RomaERP.UnitTests;

public class StockAdjustmentTests
{
    private record Setup(ApplicationDbContext Ctx, Warehouse Cairo, Warehouse Alex, Item Tomato);

    private static async Task<Setup> CreateAsync(decimal stock = 0, decimal cost = 0, bool periodOpen = true)
    {
        var ctx = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var cat = new ItemCategory { Code = "C", NameAr = "خامات", NameEn = "Raw" };
        var cairo = new Warehouse { Code = "CAI", NameAr = "القاهرة", NameEn = "Cairo" };
        var alex = new Warehouse { Code = "ALX", NameAr = "اسكندرية", NameEn = "Alex" };
        var item = new Item { Code = "TOM", NameAr = "طماطم", NameEn = "Tomato", UnitOfMeasure = "kg", ItemCategory = cat, QuantityOnHand = stock, AverageCost = cost };
        ctx.AddRange(cat, cairo, alex, item);
        ctx.Accounts.AddRange(
            new Account { Code = "1160", NameAr = "المخزون", NameEn = "Inventory", AccountType = AccountType.Asset, Nature = AccountNature.Debit },
            new Account { Code = "3200", NameAr = "الأرباح المرحّلة", NameEn = "RE", AccountType = AccountType.Equity, Nature = AccountNature.Credit },
            new Account { Code = "5500", NameAr = "تكلفة البضاعة", NameEn = "COGS", AccountType = AccountType.Expense, Nature = AccountNature.Debit });
        var year = new FiscalYear { Name = "Y", StartDate = DateTime.UtcNow.Date.AddMonths(-6), EndDate = DateTime.UtcNow.Date.AddMonths(6) };
        ctx.FiscalYears.Add(year);
        ctx.FiscalPeriods.Add(new FiscalPeriod { FiscalYear = year, FiscalYearId = year.Id, Name = "P", PeriodNumber = 1, StartDate = DateTime.UtcNow.Date.AddDays(-20), EndDate = DateTime.UtcNow.Date.AddDays(20), IsClosed = !periodOpen });
        await ctx.SaveChangesAsync();
        return new Setup(ctx, cairo, alex, item);
    }

    private static StockAdjustmentService Svc(Setup s, Guid? branch = null)
        => new(s.Ctx, new ItemLotService(s.Ctx), branch is null ? null : new FakeBranchAccess(branch));

    private static CreateStockAdjustmentDto Dto(Setup s, bool up, decimal qty, int reason, decimal? cost = null) => new()
    {
        WarehouseId = s.Cairo.Id, ItemId = s.Tomato.Id, IsIncrease = up, Quantity = qty, Reason = reason, UnitCost = cost, AdjustmentDate = DateTime.UtcNow.Date
    };

    private static async Task<(string, decimal)[]> LinesAsync(Setup s)
        => (await s.Ctx.JournalEntryLines.Include(l => l.Account).ToListAsync())
            .Select(l => (l.Account!.Code, l.Debit > 0 ? l.Debit : -l.Credit)).ToArray();

    [Fact]
    public async Task OpeningStock_AddsStockAgainstEquity_NotASupplier()
    {
        var s = await CreateAsync();
        var r = await Svc(s).CreateAsync(Dto(s, true, 50, 1, 8m), "ali");
        Assert.StartsWith("ADJ-", r.Number);
        var item = await s.Ctx.Items.SingleAsync();
        Assert.Equal(50m, item.QuantityOnHand);
        Assert.Equal(8m, item.AverageCost);
        var lines = await LinesAsync(s);
        Assert.Contains(("1160", 400m), lines);
        Assert.Contains(("3200", -400m), lines);
        var move = await s.Ctx.StockMovements.SingleAsync();
        Assert.Equal(StockMovementType.Receipt, move.MovementType);
        Assert.Equal(s.Cairo.Id, move.WarehouseId);
    }

    [Fact]
    public async Task CountGain_CreditsCostOfGoods_AndBlendsTheAverageCost()
    {
        var s = await CreateAsync(stock: 10, cost: 10m);
        await Svc(s).CreateAsync(Dto(s, true, 10, 2, 20m), "ali");
        var item = await s.Ctx.Items.SingleAsync();
        Assert.Equal(20m, item.QuantityOnHand);
        Assert.Equal(15m, item.AverageCost);
        Assert.Contains(("5500", -200m), await LinesAsync(s));
    }

    [Fact]
    public async Task Decrease_WritesOffToCostOfGoods_AndReducesTheStock()
    {
        var s = await CreateAsync(stock: 30, cost: 5m);
        await Svc(s).CreateAsync(Dto(s, false, 4, 3), "ali");
        Assert.Equal(26m, (await s.Ctx.Items.SingleAsync()).QuantityOnHand);
        var lines = await LinesAsync(s);
        Assert.Contains(("5500", 20m), lines);
        Assert.Contains(("1160", -20m), lines);
        Assert.Equal(StockMovementType.Issue, (await s.Ctx.StockMovements.SingleAsync()).MovementType);
    }

    [Fact]
    public async Task Decrease_MoreThanTheCompanyHolds_IsRefused()
    {
        var s = await CreateAsync(stock: 3, cost: 5m);
        await Assert.ThrowsAsync<ValidationAppException>(() => Svc(s).CreateAsync(Dto(s, false, 4, 3), "ali"));
        Assert.Empty(s.Ctx.StockAdjustments);
    }

    [Fact]
    public async Task Increase_NeedsACost_WhenTheItemHasNone()
        => await Assert.ThrowsAsync<ValidationAppException>(async () => { var s = await CreateAsync(); await Svc(s).CreateAsync(Dto(s, true, 5, 1), "ali"); });

    [Theory]
    [InlineData(true, 3)]
    [InlineData(false, 1)]
    [InlineData(false, 2)]
    [InlineData(true, 9)]
    public async Task ReasonThatDoesNotFitTheDirection_IsRefused(bool up, int reason)
    {
        var s = await CreateAsync(stock: 10, cost: 5m);
        await Assert.ThrowsAsync<ValidationAppException>(() => Svc(s).CreateAsync(Dto(s, up, 1, reason, 5m), "ali"));
    }

    [Fact]
    public async Task ClosedPeriod_IsRefused()
    {
        var s = await CreateAsync(periodOpen: false);
        await Assert.ThrowsAsync<ValidationAppException>(() => Svc(s).CreateAsync(Dto(s, true, 1, 1, 5m), "ali"));
    }

    [Fact]
    public async Task AUserTiedToAnotherBranch_CannotAdjustThisOne()
    {
        var s = await CreateAsync(stock: 10, cost: 5m);
        await Assert.ThrowsAsync<ValidationAppException>(() => Svc(s, s.Alex.Id).CreateAsync(Dto(s, true, 1, 1, 5m), "ali"));
    }
}
