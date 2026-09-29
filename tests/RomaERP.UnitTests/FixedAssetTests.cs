using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting.DTOs;
using RomaERP.Application.Accounting.Services;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Domain.Accounting;
using RomaERP.Infrastructure.Persistence;
using Xunit;

namespace RomaERP.UnitTests;

public class FixedAssetTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static async Task<(ApplicationDbContext ctx, Account buildings, Account accDeprBuildings, Account depreciationExpense, FiscalPeriod period)> SeedAsync()
    {
        var ctx = CreateContext();

        var buildings = new Account { Code = "1210", NameAr = "أراضي ومباني", NameEn = "Land & Buildings", AccountType = AccountType.Asset, Nature = AccountNature.Debit };
        var accDeprBuildings = new Account { Code = "1211", NameAr = "مجمع إهلاك المباني", NameEn = "Accumulated Depreciation - Buildings", AccountType = AccountType.Asset, Nature = AccountNature.Credit };
        var depreciationExpense = new Account { Code = "5400", NameAr = "مصروف الإهلاك", NameEn = "Depreciation Expense", AccountType = AccountType.Expense, Nature = AccountNature.Debit };

        var today = DateTime.UtcNow.Date;
        var year = new FiscalYear { Name = today.Year.ToString(), StartDate = new DateTime(today.Year, 1, 1), EndDate = new DateTime(today.Year, 12, 31) };
        var period = new FiscalPeriod { FiscalYear = year, FiscalYearId = year.Id, Name = "Current", PeriodNumber = 1, StartDate = today.AddDays(-15), EndDate = today.AddDays(15) };

        ctx.Accounts.AddRange(buildings, accDeprBuildings, depreciationExpense);
        ctx.FiscalYears.Add(year);
        ctx.FiscalPeriods.Add(period);
        await ctx.SaveChangesAsync();

        return (ctx, buildings, accDeprBuildings, depreciationExpense, period);
    }

    [Fact]
    public async Task CreateAsset_WithValidData_Succeeds()
    {
        var (ctx, buildings, accDeprBuildings, _, _) = await SeedAsync();
        var service = new FixedAssetService(ctx);

        var result = await service.CreateAsync(new CreateFixedAssetDto
        {
            Code = "FA-1",
            NameAr = "مبنى المصنع",
            NameEn = "Factory Building",
            AssetAccountId = buildings.Id,
            AccumulatedDepreciationAccountId = accDeprBuildings.Id,
            AcquisitionCost = 120_000,
            AcquisitionDate = new DateTime(2020, 1, 1),
            UsefulLifeYears = 10,
            SalvageValue = 0,
            DepreciationMethod = DepreciationMethod.StraightLine
        });

        Assert.Equal("FA-1", result.Code);
        Assert.Equal(0, result.AccumulatedDepreciation);
        Assert.Equal(120_000, result.BookValue);
    }

    [Fact]
    public async Task CreateAsset_WithAssetAccountOfWrongType_Throws()
    {
        var (ctx, _, accDeprBuildings, depreciationExpense, _) = await SeedAsync();
        var service = new FixedAssetService(ctx);

        await Assert.ThrowsAsync<ValidationAppException>(() => service.CreateAsync(new CreateFixedAssetDto
        {
            Code = "FA-1",
            NameAr = "أصل",
            NameEn = "Asset",
            AssetAccountId = depreciationExpense.Id, // wrong: expense account, not asset/debit
            AccumulatedDepreciationAccountId = accDeprBuildings.Id,
            AcquisitionCost = 10_000,
            AcquisitionDate = DateTime.UtcNow,
            UsefulLifeYears = 5,
            DepreciationMethod = DepreciationMethod.StraightLine
        }));
    }

    [Fact]
    public async Task CreateAsset_DecliningBalanceWithoutRate_Throws()
    {
        var (ctx, buildings, accDeprBuildings, _, _) = await SeedAsync();
        var service = new FixedAssetService(ctx);

        await Assert.ThrowsAsync<ValidationAppException>(() => service.CreateAsync(new CreateFixedAssetDto
        {
            Code = "FA-1",
            NameAr = "أصل",
            NameEn = "Asset",
            AssetAccountId = buildings.Id,
            AccumulatedDepreciationAccountId = accDeprBuildings.Id,
            AcquisitionCost = 10_000,
            AcquisitionDate = DateTime.UtcNow,
            UsefulLifeYears = 5,
            DepreciationMethod = DepreciationMethod.DecliningBalance,
            DecliningBalanceRate = null
        }));
    }

    [Fact]
    public async Task CalculateRun_StraightLine_ComputesMonthlyAmountCorrectly()
    {
        var (ctx, buildings, accDeprBuildings, _, period) = await SeedAsync();
        var assetService = new FixedAssetService(ctx);
        var asset = await assetService.CreateAsync(new CreateFixedAssetDto
        {
            Code = "FA-1",
            NameAr = "مبنى",
            NameEn = "Building",
            AssetAccountId = buildings.Id,
            AccumulatedDepreciationAccountId = accDeprBuildings.Id,
            AcquisitionCost = 120_000,
            AcquisitionDate = new DateTime(2020, 1, 1),
            UsefulLifeYears = 10,
            SalvageValue = 0,
            DepreciationMethod = DepreciationMethod.StraightLine
        });

        var depreciationService = new DepreciationService(ctx);
        var run = await depreciationService.CreateAndCalculateAsync(new CreateDepreciationRunDto
        {
            FiscalPeriodId = period.Id,
            RunDate = DateTime.UtcNow.Date
        });

        // (120,000 / 10 years) / 12 months = 1,000
        var line = Assert.Single(run.Lines);
        Assert.Equal(asset.Id, line.FixedAssetId);
        Assert.Equal(1_000m, line.Amount);
    }

    [Fact]
    public async Task CalculateRun_DecliningBalance_ComputesFromBookValue()
    {
        var (ctx, buildings, accDeprBuildings, _, period) = await SeedAsync();
        var assetService = new FixedAssetService(ctx);
        await assetService.CreateAsync(new CreateFixedAssetDto
        {
            Code = "FA-1",
            NameAr = "آلة",
            NameEn = "Machine",
            AssetAccountId = buildings.Id,
            AccumulatedDepreciationAccountId = accDeprBuildings.Id,
            AcquisitionCost = 100_000,
            AcquisitionDate = new DateTime(2020, 1, 1),
            UsefulLifeYears = 5,
            SalvageValue = 0,
            DepreciationMethod = DepreciationMethod.DecliningBalance,
            DecliningBalanceRate = 24 // 24% annually
        });

        var depreciationService = new DepreciationService(ctx);
        var run = await depreciationService.CreateAndCalculateAsync(new CreateDepreciationRunDto
        {
            FiscalPeriodId = period.Id,
            RunDate = DateTime.UtcNow.Date
        });

        // 100,000 * 24% / 12 = 2,000
        var line = Assert.Single(run.Lines);
        Assert.Equal(2_000m, line.Amount);
    }

    [Fact]
    public async Task CalculateRun_WithNoEligibleAssets_Throws()
    {
        var (ctx, _, _, _, period) = await SeedAsync();
        var depreciationService = new DepreciationService(ctx);

        await Assert.ThrowsAsync<ValidationAppException>(() => depreciationService.CreateAndCalculateAsync(new CreateDepreciationRunDto
        {
            FiscalPeriodId = period.Id,
            RunDate = DateTime.UtcNow.Date
        }));
    }

    [Fact]
    public async Task PostRun_CreatesBalancedJournalEntryAndUpdatesAccumulatedDepreciation()
    {
        var (ctx, buildings, accDeprBuildings, depreciationExpense, period) = await SeedAsync();
        var assetService = new FixedAssetService(ctx);
        var asset = await assetService.CreateAsync(new CreateFixedAssetDto
        {
            Code = "FA-1",
            NameAr = "مبنى",
            NameEn = "Building",
            AssetAccountId = buildings.Id,
            AccumulatedDepreciationAccountId = accDeprBuildings.Id,
            AcquisitionCost = 120_000,
            AcquisitionDate = new DateTime(2020, 1, 1),
            UsefulLifeYears = 10,
            SalvageValue = 0,
            DepreciationMethod = DepreciationMethod.StraightLine
        });

        var depreciationService = new DepreciationService(ctx);
        var draft = await depreciationService.CreateAndCalculateAsync(new CreateDepreciationRunDto
        {
            FiscalPeriodId = period.Id,
            RunDate = DateTime.UtcNow.Date
        });

        var posted = await depreciationService.PostAsync(draft.Id);

        Assert.Equal(DepreciationRunStatus.Posted, posted.Status);
        Assert.NotNull(posted.JournalEntryId);

        var journalEntry = await ctx.JournalEntries.Include(e => e.Lines).FirstAsync(e => e.Id == posted.JournalEntryId);
        Assert.Equal(journalEntry.TotalDebit, journalEntry.TotalCredit);
        Assert.Equal(1_000m, journalEntry.TotalDebit);

        var refreshedAsset = await assetService.GetByIdAsync(asset.Id);
        Assert.Equal(1_000m, refreshedAsset.AccumulatedDepreciation);
        Assert.Equal(119_000m, refreshedAsset.BookValue);
    }

    [Fact]
    public async Task PostRun_WhenAlreadyPosted_Throws()
    {
        var (ctx, buildings, accDeprBuildings, _, period) = await SeedAsync();
        var assetService = new FixedAssetService(ctx);
        await assetService.CreateAsync(new CreateFixedAssetDto
        {
            Code = "FA-1",
            NameAr = "مبنى",
            NameEn = "Building",
            AssetAccountId = buildings.Id,
            AccumulatedDepreciationAccountId = accDeprBuildings.Id,
            AcquisitionCost = 120_000,
            AcquisitionDate = new DateTime(2020, 1, 1),
            UsefulLifeYears = 10,
            DepreciationMethod = DepreciationMethod.StraightLine
        });

        var depreciationService = new DepreciationService(ctx);
        var draft = await depreciationService.CreateAndCalculateAsync(new CreateDepreciationRunDto
        {
            FiscalPeriodId = period.Id,
            RunDate = DateTime.UtcNow.Date
        });
        await depreciationService.PostAsync(draft.Id);

        await Assert.ThrowsAsync<ValidationAppException>(() => depreciationService.PostAsync(draft.Id));
    }
}
