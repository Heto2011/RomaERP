using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.HR.DTOs;
using RomaERP.Application.HR.Services;
using RomaERP.Domain.HR;
using RomaERP.Infrastructure.Persistence;
using Xunit;

namespace RomaERP.UnitTests;

public class PayrollServiceTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static Employee CreateEmployee(decimal basicSalary = 10000)
    {
        return new Employee
        {
            EmployeeCode = "EMP-001",
            FullNameAr = "إيهاب صلاح",
            FullNameEn = "Ehab Salah",
            HireDate = new DateTime(2025, 1, 1),
            BasicSalary = basicSalary
        };
    }

    private static async Task<(ApplicationDbContext ctx, PayrollRun run, Employee employee)> SeedDraftRunAsync()
    {
        var ctx = CreateContext();
        var employee = CreateEmployee();
        ctx.Employees.Add(employee);

        var run = new PayrollRun
        {
            RunDate = new DateTime(2026, 8, 29),
            Description = "دورة أغسطس",
            Status = PayrollRunStatus.Draft,
            Lines =
            {
                new PayrollRunLine { EmployeeId = employee.Id, BasicSalary = 10000, TotalAllowances = 0, TotalDeductions = 0, NetSalary = 10000 }
            }
        };
        ctx.PayrollRuns.Add(run);
        await ctx.SaveChangesAsync();

        return (ctx, run, employee);
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletesDraftRun_AndHidesItFromSubsequentQueries()
    {
        var (ctx, run, _) = await SeedDraftRunAsync();
        var service = new PayrollService(ctx);

        await service.DeleteAsync(run.Id);

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetByIdAsync(run.Id));
    }

    [Fact]
    public async Task DeleteAsync_ThrowsWhenRunIsNotDraft()
    {
        var (ctx, run, _) = await SeedDraftRunAsync();
        run.Status = PayrollRunStatus.Approved;
        await ctx.SaveChangesAsync();
        var service = new PayrollService(ctx);

        await Assert.ThrowsAsync<ValidationAppException>(() => service.DeleteAsync(run.Id));
    }

    [Fact]
    public async Task RevertToDraftAsync_MovesApprovedRunBackToDraft()
    {
        var (ctx, run, _) = await SeedDraftRunAsync();
        run.Status = PayrollRunStatus.Approved;
        await ctx.SaveChangesAsync();
        var service = new PayrollService(ctx);

        var result = await service.RevertToDraftAsync(run.Id);

        Assert.Equal(PayrollRunStatus.Draft, result.Status);
    }

    [Fact]
    public async Task RevertToDraftAsync_ThrowsWhenRunIsAlreadyDraft()
    {
        var (ctx, run, _) = await SeedDraftRunAsync();
        var service = new PayrollService(ctx);

        await Assert.ThrowsAsync<ValidationAppException>(() => service.RevertToDraftAsync(run.Id));
    }

    [Fact]
    public async Task UpdateLineAsync_RecalculatesNetSalary_WhenRunIsDraft()
    {
        var (ctx, run, employee) = await SeedDraftRunAsync();
        var service = new PayrollService(ctx);

        var result = await service.UpdateLineAsync(run.Id, employee.Id, new UpdatePayrollLineDto { TotalAllowances = 1500, TotalDeductions = 200 });

        var line = Assert.Single(result.Lines);
        Assert.Equal(1500, line.TotalAllowances);
        Assert.Equal(200, line.TotalDeductions);
        Assert.Equal(11300, line.NetSalary); // 10000 + 1500 - 200
    }

    [Fact]
    public async Task UpdateLineAsync_ThrowsWhenRunIsNotDraft()
    {
        var (ctx, run, employee) = await SeedDraftRunAsync();
        run.Status = PayrollRunStatus.Approved;
        await ctx.SaveChangesAsync();
        var service = new PayrollService(ctx);

        await Assert.ThrowsAsync<ValidationAppException>(
            () => service.UpdateLineAsync(run.Id, employee.Id, new UpdatePayrollLineDto { TotalAllowances = 100, TotalDeductions = 0 }));
    }
}
