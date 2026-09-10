using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.HR.DTOs;
using RomaERP.Application.HR.Services;
using RomaERP.Domain.HR;
using RomaERP.Infrastructure.Persistence;
using Xunit;

namespace RomaERP.UnitTests;

public class EmployeeSelfServiceTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static async Task<(ApplicationDbContext ctx, Department dept, Position pos)> SeedAsync()
    {
        var ctx = CreateContext();
        var dept = new Department { Code = "DEP-1", NameAr = "المبيعات", NameEn = "Sales" };
        var pos = new Position { Code = "POS-1", TitleAr = "مندوب", TitleEn = "Rep", Department = dept, DepartmentId = dept.Id };

        ctx.Departments.Add(dept);
        ctx.Positions.Add(pos);
        await ctx.SaveChangesAsync();

        return (ctx, dept, pos);
    }

    private static Employee NewEmployee(Department dept, Position pos, string code, Guid? applicationUserId = null) => new()
    {
        EmployeeCode = code,
        FullNameAr = "موظف تجريبي",
        FullNameEn = "Test Employee",
        HireDate = DateTime.UtcNow.Date,
        DepartmentId = dept.Id,
        PositionId = pos.Id,
        BasicSalary = 5000,
        ApplicationUserId = applicationUserId
    };

    [Fact]
    public async Task GetMyProfile_WhenLinked_ReturnsOwnEmployeeRecord()
    {
        var (ctx, dept, pos) = await SeedAsync();
        var userId = Guid.NewGuid();
        var employee = NewEmployee(dept, pos, "EMP-1", userId);
        ctx.Employees.Add(employee);
        await ctx.SaveChangesAsync();

        var service = new EmployeeService(ctx);
        var profile = await service.GetMyProfileAsync(userId);

        Assert.NotNull(profile);
        Assert.Equal(employee.Id, profile!.Id);
        Assert.Equal("EMP-1", profile.EmployeeCode);
    }

    [Fact]
    public async Task GetMyProfile_WhenNotLinked_ReturnsNull()
    {
        var (ctx, dept, pos) = await SeedAsync();
        ctx.Employees.Add(NewEmployee(dept, pos, "EMP-1"));
        await ctx.SaveChangesAsync();

        var service = new EmployeeService(ctx);
        var profile = await service.GetMyProfileAsync(Guid.NewGuid());

        Assert.Null(profile);
    }

    [Fact]
    public async Task LinkUser_MovingLinkToAnotherEmployee_ClearsThePreviousOne()
    {
        var (ctx, dept, pos) = await SeedAsync();
        var userId = Guid.NewGuid();
        var firstEmployee = NewEmployee(dept, pos, "EMP-1", userId);
        var secondEmployee = NewEmployee(dept, pos, "EMP-2");
        ctx.Employees.AddRange(firstEmployee, secondEmployee);
        await ctx.SaveChangesAsync();

        var service = new EmployeeService(ctx);
        await service.LinkUserAsync(secondEmployee.Id, userId);

        var refreshedFirst = await ctx.Employees.AsNoTracking().FirstAsync(e => e.Id == firstEmployee.Id);
        var refreshedSecond = await ctx.Employees.AsNoTracking().FirstAsync(e => e.Id == secondEmployee.Id);
        Assert.Null(refreshedFirst.ApplicationUserId);
        Assert.Equal(userId, refreshedSecond.ApplicationUserId);
    }

    [Fact]
    public async Task LinkUser_WithNull_UnlinksTheEmployee()
    {
        var (ctx, dept, pos) = await SeedAsync();
        var userId = Guid.NewGuid();
        var employee = NewEmployee(dept, pos, "EMP-1", userId);
        ctx.Employees.Add(employee);
        await ctx.SaveChangesAsync();

        var service = new EmployeeService(ctx);
        await service.LinkUserAsync(employee.Id, null);

        var refreshed = await ctx.Employees.AsNoTracking().FirstAsync(e => e.Id == employee.Id);
        Assert.Null(refreshed.ApplicationUserId);
    }

    [Fact]
    public async Task LinkUser_UnknownEmployee_ThrowsNotFound()
    {
        var (ctx, _, _) = await SeedAsync();
        var service = new EmployeeService(ctx);

        await Assert.ThrowsAsync<NotFoundException>(() => service.LinkUserAsync(Guid.NewGuid(), Guid.NewGuid()));
    }

    [Fact]
    public async Task GetMyPayslips_OnlyReturnsNonDraftRunsForTheOwnEmployee()
    {
        var (ctx, dept, pos) = await SeedAsync();
        var employee = NewEmployee(dept, pos, "EMP-1");
        var otherEmployee = NewEmployee(dept, pos, "EMP-2");
        ctx.Employees.AddRange(employee, otherEmployee);

        var fiscalYear = new RomaERP.Domain.Accounting.FiscalYear { Name = "2026", StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 12, 31) };
        var period = new RomaERP.Domain.Accounting.FiscalPeriod { FiscalYear = fiscalYear, FiscalYearId = fiscalYear.Id, Name = "January", PeriodNumber = 1, StartDate = fiscalYear.StartDate, EndDate = fiscalYear.StartDate.AddDays(30) };
        ctx.FiscalYears.Add(fiscalYear);
        ctx.FiscalPeriods.Add(period);

        var draftRun = new PayrollRun { FiscalPeriodId = period.Id, RunDate = new DateTime(2026, 1, 31), Status = PayrollRunStatus.Draft };
        var postedRun = new PayrollRun { FiscalPeriodId = period.Id, RunDate = new DateTime(2026, 2, 28), Status = PayrollRunStatus.Posted };
        ctx.PayrollRuns.AddRange(draftRun, postedRun);

        ctx.PayrollRunLines.AddRange(
            new PayrollRunLine { PayrollRun = draftRun, PayrollRunId = draftRun.Id, EmployeeId = employee.Id, BasicSalary = 5000, NetSalary = 5000 },
            new PayrollRunLine { PayrollRun = postedRun, PayrollRunId = postedRun.Id, EmployeeId = employee.Id, BasicSalary = 5000, NetSalary = 4800, TotalDeductions = 200 },
            new PayrollRunLine { PayrollRun = postedRun, PayrollRunId = postedRun.Id, EmployeeId = otherEmployee.Id, BasicSalary = 6000, NetSalary = 6000 });

        await ctx.SaveChangesAsync();

        var service = new RomaERP.Application.HR.Services.PayrollService(ctx);
        var payslips = await service.GetMyPayslipsAsync(employee.Id);

        var payslip = Assert.Single(payslips);
        Assert.Equal(PayrollRunStatus.Posted, payslip.Status);
        Assert.Equal(4800, payslip.NetSalary);
    }
}
