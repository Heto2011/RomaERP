using Microsoft.EntityFrameworkCore;
using RomaERP.Domain.Audit;
using RomaERP.Domain.Inventory;
using RomaERP.Infrastructure.Persistence;
using Xunit;

namespace RomaERP.UnitTests;

public class AuditLogTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task SaveChanges_OnInsert_RecordsCreatedAuditLog()
    {
        var ctx = CreateContext();
        var warehouse = new Warehouse { Code = "WH1", NameAr = "الفرع الرئيسي", NameEn = "Main Branch" };
        ctx.Warehouses.Add(warehouse);

        await ctx.SaveChangesAsync();

        var log = Assert.Single(ctx.AuditLogs.AsNoTracking().ToList());
        Assert.Equal(nameof(Warehouse), log.EntityName);
        Assert.Equal(warehouse.Id.ToString(), log.EntityId);
        Assert.Equal(AuditAction.Created, log.Action);
        Assert.Contains("WH1", log.Changes);
    }

    [Fact]
    public async Task SaveChanges_OnUpdate_RecordsUpdatedAuditLogWithOldAndNewValues()
    {
        var ctx = CreateContext();
        var warehouse = new Warehouse { Code = "WH1", NameAr = "الفرع الرئيسي", NameEn = "Main Branch" };
        ctx.Warehouses.Add(warehouse);
        await ctx.SaveChangesAsync();

        warehouse.NameEn = "Main Warehouse";
        await ctx.SaveChangesAsync();

        var log = ctx.AuditLogs.AsNoTracking().Single(a => a.Action == AuditAction.Updated);
        Assert.Equal(nameof(Warehouse), log.EntityName);
        Assert.Contains("Main Branch", log.Changes);
        Assert.Contains("Main Warehouse", log.Changes);
    }

    [Fact]
    public async Task SaveChanges_OnSoftDelete_RecordsDeletedAuditLogNotUpdated()
    {
        var ctx = CreateContext();
        var warehouse = new Warehouse { Code = "WH1", NameAr = "الفرع الرئيسي", NameEn = "Main Branch" };
        ctx.Warehouses.Add(warehouse);
        await ctx.SaveChangesAsync();

        warehouse.IsDeleted = true;
        await ctx.SaveChangesAsync();

        var log = ctx.AuditLogs.AsNoTracking().Single(a => a.Action == AuditAction.Deleted);
        Assert.Equal(nameof(Warehouse), log.EntityName);
    }

    [Fact]
    public async Task SaveChanges_DoesNotRecursivelyAuditItsOwnAuditLogRows()
    {
        var ctx = CreateContext();
        var warehouse = new Warehouse { Code = "WH1", NameAr = "الفرع الرئيسي", NameEn = "Main Branch" };
        ctx.Warehouses.Add(warehouse);
        await ctx.SaveChangesAsync();

        warehouse.NameEn = "Main Warehouse";
        await ctx.SaveChangesAsync();

        // One Created log for the warehouse insert, one Updated log for the rename — never more.
        Assert.Equal(2, ctx.AuditLogs.AsNoTracking().Count());
    }
}
