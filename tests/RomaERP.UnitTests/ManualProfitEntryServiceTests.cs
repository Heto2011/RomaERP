using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting.DTOs;
using RomaERP.Application.Accounting.Services;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Domain.Accounting;
using RomaERP.Infrastructure.Persistence;
using Xunit;

namespace RomaERP.UnitTests;

public class ManualProfitEntryServiceTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task CreateAsync_ComputesGrossProfitAndMarginFromEnteredNumbers()
    {
        var ctx = CreateContext();
        var service = new ManualProfitEntryService(ctx);

        var dto = new CreateManualProfitEntryDto
        {
            Dimension = (int)ManualProfitDimension.Branch,
            Name = "فرع المعادي",
            PeriodMonth = new DateTime(2026, 8, 1),
            Revenue = 100000,
            Cost = 60000
        };

        var result = await service.CreateAsync(dto);

        Assert.Equal("فرع المعادي", result.Name);
        Assert.Equal(40000, result.GrossProfit);
        Assert.Equal(40, result.MarginPercent);
    }

    [Fact]
    public async Task CreateAsync_RejectsBlankName()
    {
        var ctx = CreateContext();
        var service = new ManualProfitEntryService(ctx);

        var dto = new CreateManualProfitEntryDto
        {
            Dimension = (int)ManualProfitDimension.Channel,
            Name = "   ",
            PeriodMonth = new DateTime(2026, 8, 1),
            Revenue = 1000,
            Cost = 500
        };

        await Assert.ThrowsAsync<ValidationAppException>(() => service.CreateAsync(dto));
    }

    [Fact]
    public async Task GetAllAsync_FiltersByDimensionAndOrdersByPeriodDescending()
    {
        var ctx = CreateContext();
        var service = new ManualProfitEntryService(ctx);

        await service.CreateAsync(new CreateManualProfitEntryDto { Dimension = (int)ManualProfitDimension.Branch, Name = "فرع المعادي", PeriodMonth = new DateTime(2026, 7, 1), Revenue = 100, Cost = 50 });
        await service.CreateAsync(new CreateManualProfitEntryDto { Dimension = (int)ManualProfitDimension.Branch, Name = "فرع مصر الجديدة", PeriodMonth = new DateTime(2026, 8, 1), Revenue = 200, Cost = 80 });
        await service.CreateAsync(new CreateManualProfitEntryDto { Dimension = (int)ManualProfitDimension.Channel, Name = "أونلاين", PeriodMonth = new DateTime(2026, 8, 1), Revenue = 300, Cost = 100 });

        var branches = await service.GetAllAsync(ManualProfitDimension.Branch);

        Assert.Equal(2, branches.Count);
        Assert.Equal("فرع مصر الجديدة", branches[0].Name);
        Assert.Equal("فرع المعادي", branches[1].Name);
    }

    [Fact]
    public async Task UpdateAsync_RecalculatesFromNewNumbers()
    {
        var ctx = CreateContext();
        var service = new ManualProfitEntryService(ctx);

        var created = await service.CreateAsync(new CreateManualProfitEntryDto
        {
            Dimension = (int)ManualProfitDimension.Channel,
            Name = "أونلاين",
            PeriodMonth = new DateTime(2026, 8, 1),
            Revenue = 1000,
            Cost = 400
        });

        var updated = await service.UpdateAsync(created.Id, new UpdateManualProfitEntryDto
        {
            Name = "أونلاين",
            PeriodMonth = new DateTime(2026, 8, 1),
            Revenue = 1000,
            Cost = 900
        });

        Assert.Equal(100, updated.GrossProfit);
        Assert.Equal(10, updated.MarginPercent);
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletesAndExcludesFromGetAll()
    {
        var ctx = CreateContext();
        var service = new ManualProfitEntryService(ctx);

        var created = await service.CreateAsync(new CreateManualProfitEntryDto
        {
            Dimension = (int)ManualProfitDimension.Branch,
            Name = "فرع المعادي",
            PeriodMonth = new DateTime(2026, 8, 1),
            Revenue = 100,
            Cost = 50
        });

        await service.DeleteAsync(created.Id);

        var branches = await service.GetAllAsync(ManualProfitDimension.Branch);
        Assert.Empty(branches);
    }
}
