using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;
using RomaERP.Infrastructure.Persistence;
using Xunit;

namespace RomaERP.UnitTests;

/// <summary>The migrations are written by hand, so this proves the snapshot still matches the real model:
/// any entity/column added without a matching migration shows up here instead of failing on a customer's database.</summary>
public class MigrationModelTests
{
    [Fact]
    public void Snapshot_MatchesCurrentModel_NoPendingChanges()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=.;Database=never_connected;TrustServerCertificate=True")
            .Options;
        using var ctx = new ApplicationDbContext(options);

        var services = ((IInfrastructure<IServiceProvider>)ctx).Instance;
        var assembly = ctx.GetService<IMigrationsAssembly>();
        var snapshotModel = assembly.ModelSnapshot!.Model;
        var designTime = ctx.GetService<IDesignTimeModel>().Model;
        var differ = ctx.GetService<IMigrationsModelDiffer>();

        var initializer = ctx.GetService<IModelRuntimeInitializer>();
        var snapshot = initializer.Initialize(snapshotModel);
        var ops = differ.GetDifferences(snapshot.GetRelationalModel(), designTime.GetRelationalModel());

        Assert.True(ops.Count == 0, "Pending model changes:\n" + string.Join("\n", ops.Select(o => o.GetType().Name + " " + Describe(o))));
    }

    private static string Describe(MigrationOperation o) => o switch
    {
        CreateTableOperation c => c.Name,
        AddColumnOperation a => $"{a.Table}.{a.Name}",
        AlterColumnOperation a => $"{a.Table}.{a.Name}",
        CreateIndexOperation i => $"{i.Table}.{i.Name}",
        AddForeignKeyOperation f => $"{f.Table}.{f.Name}",
        _ => o.ToString() ?? ""
    };

    [Fact]
    public void AllMigrations_GenerateSqlScript_IncludingLatestTables()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=.;Database=never_connected;TrustServerCertificate=True")
            .Options;
        using var ctx = new ApplicationDbContext(options);

        var script = ctx.GetService<IMigrator>().GenerateScript();

        foreach (var table in new[] { "ExpensePolicies", "WorkShifts", "Candidates", "ReviewCycles", "SurveyResponses", "LoyaltyMembers", "OfflineClientId" })
            Assert.Contains(table, script);
    }
}
