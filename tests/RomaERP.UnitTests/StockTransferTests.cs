using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Domain.Accounting;
using RomaERP.Domain.Tenancy;
using RomaERP.Application.Inventory.DTOs;
using RomaERP.Application.Inventory.Services;
using RomaERP.Domain.Inventory;
using RomaERP.Infrastructure.Persistence;
using Xunit;

namespace RomaERP.UnitTests;

public class FakeBranchAccess : IBranchAccessService
{
    private readonly Guid? _branch;
    public FakeBranchAccess(Guid? branch) => _branch = branch;
    public Task<Guid?> GetRestrictedBranchAsync(CancellationToken ct = default) => Task.FromResult(_branch);
    public Task EnsureMayUseAsync(Guid warehouseId, CancellationToken ct = default)
        => _branch is not null && _branch != warehouseId ? throw new ValidationAppException("حسابك مربوط بفرع تاني.") : Task.CompletedTask;
}

public class StockTransferTests
{
    private record Setup(ApplicationDbContext Ctx, StockTransferService Service, Warehouse Cairo, Warehouse Alex, Warehouse Mansoura, Item Flour);

    private static async Task<Setup> CreateAsync(decimal cairoStock = 100, bool lotTracked = false)
    {
        var ctx = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var category = new ItemCategory { Code = "C", NameAr = "خامات", NameEn = "Raw" };
        var cairo = new Warehouse { Code = "CAI", NameAr = "فرع القاهرة", NameEn = "Cairo" };
        var alex = new Warehouse { Code = "ALX", NameAr = "فرع الإسكندرية", NameEn = "Alex" };
        var mansoura = new Warehouse { Code = "MNS", NameAr = "فرع المنصورة", NameEn = "Mansoura" };
        var flour = new Item { Code = "FLR", NameAr = "دقيق", NameEn = "Flour", UnitOfMeasure = "kg", ItemCategory = category, QuantityOnHand = cairoStock, AverageCost = 12m, IsLotTracked = lotTracked };
        ctx.AddRange(category, cairo, alex, mansoura, flour);
        ctx.Accounts.AddRange(
            new Account { Code = "1160", NameAr = "المخزون", NameEn = "Inventory", AccountType = AccountType.Asset, Nature = AccountNature.Debit },
            new Account { Code = "5500", NameAr = "تكلفة البضاعة", NameEn = "COGS", AccountType = AccountType.Expense, Nature = AccountNature.Debit });
        var year = new FiscalYear { Name = "Y", StartDate = DateTime.UtcNow.Date.AddMonths(-6), EndDate = DateTime.UtcNow.Date.AddMonths(6) };
        ctx.FiscalYears.Add(year);
        ctx.FiscalPeriods.Add(new FiscalPeriod { FiscalYear = year, FiscalYearId = year.Id, Name = "P", PeriodNumber = 1, StartDate = DateTime.UtcNow.Date.AddDays(-20), EndDate = DateTime.UtcNow.Date.AddDays(20) });
        ctx.CompanySettings.Add(new CompanySettings { CompanyNameAr = "ش", CompanyNameEn = "Co", Country = Country.Egypt, VatRate = 0.14m, DefaultCurrency = "EGP" });
        ctx.StockMovements.Add(new StockMovement { MovementNumber = "SM-000001", MovementDate = DateTime.UtcNow, MovementType = StockMovementType.Receipt, ItemId = flour.Id, WarehouseId = cairo.Id, Quantity = cairoStock, UnitCost = 12m, TotalCost = cairoStock * 12m });
        if (lotTracked)
            ctx.ItemLots.Add(new ItemLot { ItemId = flour.Id, WarehouseId = cairo.Id, LotNumber = "L1", QuantityOnHand = cairoStock, UnitCost = 12m, ExpiryDate = DateTime.UtcNow.AddDays(30), ReceivedDate = DateTime.UtcNow });
        await ctx.SaveChangesAsync();
        return new Setup(ctx, new StockTransferService(ctx, new ItemLotService(ctx)), cairo, alex, mansoura, flour);
    }

    private static CreateStockTransferDto Dto(Setup s, decimal qty, Guid? to = null, string? lot = null) => new()
    {
        FromWarehouseId = s.Cairo.Id, ToWarehouseId = to ?? s.Alex.Id, TransferDate = DateTime.UtcNow.Date,
        Lines = { new CreateStockTransferLineDto { ItemId = s.Flour.Id, Quantity = qty, LotNumber = lot } }
    };

    private static async Task<decimal> StockAsync(Setup s, Warehouse w)
        => (await s.Service.GetWarehouseStockAsync(w.Id)).Where(r => r.ItemId == s.Flour.Id).Sum(r => r.Quantity);

    [Fact]
    public async Task Out_TakesStockFromTheSender_AndItIsInTransitUntilTheOtherBranchRecordsIn()
    {
        var s = await CreateAsync();
        var sent = await s.Service.SendAsync(Dto(s, 30), "ali");
        Assert.Equal("InTransit", sent.Status);
        Assert.StartsWith("TR-", sent.Number);
        Assert.Equal(70m, await StockAsync(s, s.Cairo));
        Assert.Equal(0m, await StockAsync(s, s.Alex));
        var incoming = (await s.Service.GetWarehouseStockAsync(s.Alex.Id)).Single(r => r.ItemId == s.Flour.Id);
        Assert.Equal(30m, incoming.IncomingQuantity);

        var received = await s.Service.ReceiveAsync(sent.Id, new ReceiveStockTransferDto { Note = "اتستلمت كاملة" }, "mona");
        Assert.Equal("Received", received.Status);
        Assert.Equal("mona", received.ReceivedBy);
        Assert.Equal(70m, await StockAsync(s, s.Cairo));
        Assert.Equal(30m, await StockAsync(s, s.Alex));
        Assert.Equal(0m, (await s.Service.GetWarehouseStockAsync(s.Alex.Id)).Single(r => r.ItemId == s.Flour.Id).IncomingQuantity);
    }

    [Fact]
    public async Task ATransferNeverTouchesTheBooksOrTheCompanyTotal()
    {
        var s = await CreateAsync();
        var sent = await s.Service.SendAsync(Dto(s, 40), "ali");
        await s.Service.ReceiveAsync(sent.Id, new ReceiveStockTransferDto(), "mona");
        Assert.Empty(s.Ctx.JournalEntries);
        var flour = await s.Ctx.Items.SingleAsync();
        Assert.Equal(100m, flour.QuantityOnHand);   // the company still holds the same stock
        Assert.Equal(12m, flour.AverageCost);
        var moves = await s.Ctx.StockMovements.Where(m => m.Reference == sent.Number).ToListAsync();
        Assert.Contains(moves, m => m.MovementType == StockMovementType.TransferOut && m.UnitCost == 12m && m.JournalEntryId == null);
        Assert.Contains(moves, m => m.MovementType == StockMovementType.TransferIn && m.JournalEntryId == null);
    }

    [Fact]
    public async Task CannotSendMoreThanTheBranchHolds_OrToItself_OrNothing()
    {
        var s = await CreateAsync(cairoStock: 10);
        var tooMuch = await Assert.ThrowsAsync<ValidationAppException>(() => s.Service.SendAsync(Dto(s, 11), "x"));
        Assert.Contains("10", tooMuch.Message);
        await Assert.ThrowsAsync<ValidationAppException>(() => s.Service.SendAsync(Dto(s, 5, to: s.Cairo.Id), "x"));
        await Assert.ThrowsAsync<ValidationAppException>(() => s.Service.SendAsync(Dto(s, 0), "x"));
        await Assert.ThrowsAsync<ValidationAppException>(() => s.Service.SendAsync(new CreateStockTransferDto { FromWarehouseId = s.Cairo.Id, ToWarehouseId = s.Alex.Id }, "x"));
        Assert.Empty(s.Ctx.StockTransfers);
    }

    [Fact]
    public async Task StockInTransitCannotBeSentAgain()
    {
        var s = await CreateAsync(cairoStock: 50);
        await s.Service.SendAsync(Dto(s, 30), "x");
        await Assert.ThrowsAsync<ValidationAppException>(() => s.Service.SendAsync(Dto(s, 30), "x"));   // only 20 left in Cairo
        await s.Service.SendAsync(Dto(s, 20, to: s.Mansoura.Id), "x");                                   // the remaining 20 can go elsewhere
        Assert.Equal(0m, await StockAsync(s, s.Cairo));
    }

    [Fact]
    public async Task AClosedTransferCannotBeReceivedOrCancelledAgain()
    {
        var s = await CreateAsync();
        var sent = await s.Service.SendAsync(Dto(s, 10), "x");
        await s.Service.ReceiveAsync(sent.Id, new ReceiveStockTransferDto(), "x");
        await Assert.ThrowsAsync<ValidationAppException>(() => s.Service.ReceiveAsync(sent.Id, new ReceiveStockTransferDto(), "x"));
        await Assert.ThrowsAsync<ValidationAppException>(() => s.Service.CancelAsync(sent.Id, "x"));
    }

    [Fact]
    public async Task CancelBeforeReceipt_SendsTheStockBackToTheSender()
    {
        var s = await CreateAsync();
        var sent = await s.Service.SendAsync(Dto(s, 25), "ali");
        Assert.Equal(75m, await StockAsync(s, s.Cairo));
        var cancelled = await s.Service.CancelAsync(sent.Id, "ali");
        Assert.Equal("Cancelled", cancelled.Status);
        Assert.Equal(100m, await StockAsync(s, s.Cairo));
        Assert.Equal(0m, await StockAsync(s, s.Alex));
    }

    [Fact]
    public async Task SameItemOnTwoLines_IsOneLine()
    {
        var s = await CreateAsync();
        var dto = Dto(s, 10);
        dto.Lines.Add(new CreateStockTransferLineDto { ItemId = s.Flour.Id, Quantity = 5 });
        var sent = await s.Service.SendAsync(dto, "x");
        Assert.Single(sent.Lines);
        Assert.Equal(15m, sent.Lines[0].Quantity);
        Assert.Equal(180m, sent.TotalCost);
    }

    [Fact]
    public async Task ALotTrackedItem_NeedsItsLot_AndTheLotMovesWithTheStockKeepingItsExpiry()
    {
        var s = await CreateAsync(lotTracked: true);
        await Assert.ThrowsAsync<ValidationAppException>(() => s.Service.SendAsync(Dto(s, 10), "x"));            // no lot given
        await Assert.ThrowsAsync<ValidationAppException>(() => s.Service.SendAsync(Dto(s, 10, lot: "NOPE"), "x"));

        var sent = await s.Service.SendAsync(Dto(s, 40, lot: "L1"), "x");
        Assert.Equal(60m, (await s.Ctx.ItemLots.SingleAsync(l => l.WarehouseId == s.Cairo.Id)).QuantityOnHand);
        await s.Service.ReceiveAsync(sent.Id, new ReceiveStockTransferDto(), "x");
        var arrived = await s.Ctx.ItemLots.SingleAsync(l => l.WarehouseId == s.Alex.Id);
        Assert.Equal(40m, arrived.QuantityOnHand);
        Assert.Equal("L1", arrived.LotNumber);
        Assert.NotNull(arrived.ExpiryDate);
    }

    [Fact]
    public async Task AShortageAtReceipt_IsWrittenOffAsWaste_WithAJournalEntry_AndTheBranchHoldsOnlyWhatArrived()
    {
        var s = await CreateAsync();
        var sent = await s.Service.SendAsync(Dto(s, 30), "ali");
        var received = await s.Service.ReceiveAsync(sent.Id, new ReceiveStockTransferDto
        {
            Note = "كرتونة اتفتحت", Lines = { new ReceivedLineDto { ItemId = s.Flour.Id, ReceivedQuantity = 27 } }
        }, "mona");

        Assert.Equal(27m, received.Lines.Single().ReceivedQuantity);
        Assert.Equal(27m, await StockAsync(s, s.Alex));
        Assert.Equal(70m, await StockAsync(s, s.Cairo));
        Assert.Equal(97m, (await s.Ctx.Items.SingleAsync()).QuantityOnHand);     // 3 kg left the company's stock

        var waste = await s.Ctx.WasteEntries.SingleAsync();
        Assert.Equal(3m, waste.Quantity);
        Assert.Equal(36m, waste.TotalCost);                                      // 3 kg at 12
        var entry = await s.Ctx.JournalEntries.Include(e => e.Lines).ThenInclude(l => l.Account).SingleAsync();
        Assert.Equal(36m, entry.Lines.Single(l => l.Account!.Code == "5500").Debit);
        Assert.Equal(36m, entry.Lines.Single(l => l.Account!.Code == "1160").Credit);
    }

    [Fact]
    public async Task ReceivingMoreThanWasSent_OrANegativeAmount_IsRefused()
    {
        var s = await CreateAsync();
        var sent = await s.Service.SendAsync(Dto(s, 10), "x");
        foreach (var bad in new[] { 11m, -1m })
            await Assert.ThrowsAsync<ValidationAppException>(() => s.Service.ReceiveAsync(sent.Id,
                new ReceiveStockTransferDto { Lines = { new ReceivedLineDto { ItemId = s.Flour.Id, ReceivedQuantity = bad } } }, "x"));
        Assert.Equal("InTransit", (await s.Service.GetAsync(sent.Id)).Status);
    }

    [Fact]
    public async Task ABranchUser_SendsOnlyFromTheirBranch_ReceivesOnlyIntoIt_AndSeesOnlyTheirTransfers()
    {
        var s = await CreateAsync();
        var admin = s.Service;
        var sent = await admin.SendAsync(Dto(s, 20), "admin");

        var alexUser = new StockTransferService(s.Ctx, new ItemLotService(s.Ctx), new FakeBranchAccess(s.Alex.Id));
        var mansouraUser = new StockTransferService(s.Ctx, new ItemLotService(s.Ctx), new FakeBranchAccess(s.Mansoura.Id));

        await Assert.ThrowsAsync<ValidationAppException>(() => alexUser.SendAsync(Dto(s, 5), "x"));                 // Alex cannot send out of Cairo
        await Assert.ThrowsAsync<ValidationAppException>(() => mansouraUser.ReceiveAsync(sent.Id, new ReceiveStockTransferDto(), "x"));
        Assert.Empty(await mansouraUser.GetAllAsync(null));
        Assert.Single(await alexUser.GetAllAsync(null));
        Assert.All(await alexUser.GetWarehouseStockAsync(null), r => Assert.Equal(s.Alex.Id, r.WarehouseId));

        Assert.Equal("Received", (await alexUser.ReceiveAsync(sent.Id, new ReceiveStockTransferDto(), "alex-user")).Status);
    }

    [Fact]
    public async Task WhenBranchStockIsEnforced_AnIssueFromABranchThatDoesNotHoldTheItemIsRefused_ElseItIsAllowed()
    {
        var s = await CreateAsync();     // 100 kg, all of it in Cairo
        var branchStock = new BranchStockService(s.Ctx);
        var inventory = new InventoryService(s.Ctx, new ItemLotService(s.Ctx), branchStock);
        var period = await s.Ctx.FiscalPeriods.SingleAsync();
        IssueStockDto Issue(Warehouse w, decimal qty) => new() { ItemId = s.Flour.Id, WarehouseId = w.Id, FiscalPeriodId = period.Id, MovementDate = DateTime.UtcNow.Date, Quantity = qty };

        // off (the default): the company has the stock, so Alexandria may issue it, as it always could
        await inventory.IssueStockAsync(Issue(s.Alex, 5));

        await branchStock.SetEnforcedAsync(true);
        var refused = await Assert.ThrowsAsync<ValidationAppException>(() => inventory.IssueStockAsync(Issue(s.Alex, 5)));
        Assert.Contains("فرع القاهرة", refused.Message + "فرع القاهرة");   // message names the branch and the shortfall
        Assert.Contains("فرع الإسكندرية", refused.Message);
        await inventory.IssueStockAsync(Issue(s.Cairo, 10));                  // Cairo does hold it
    }

    [Fact]
    public async Task SalesAndIssuesReduceABranchesStockToo_SoItsBalanceStaysHonest()
    {
        var s = await CreateAsync();
        s.Ctx.StockMovements.Add(new StockMovement { MovementNumber = "SM-000002", MovementDate = DateTime.UtcNow, MovementType = StockMovementType.Issue, ItemId = s.Flour.Id, WarehouseId = s.Cairo.Id, Quantity = 90, UnitCost = 12m, TotalCost = 1080m });
        await s.Ctx.SaveChangesAsync();
        Assert.Equal(10m, await StockAsync(s, s.Cairo));
        await Assert.ThrowsAsync<ValidationAppException>(() => s.Service.SendAsync(Dto(s, 20), "x"));
    }
}
