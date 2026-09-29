using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.HR.DTOs;
using RomaERP.Application.HR.Services;
using RomaERP.Domain.HR;
using RomaERP.Infrastructure.Persistence;
using Xunit;

namespace RomaERP.UnitTests;

public class EmployeeContractServiceTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new ApplicationDbContext(options);
    }

    private static async Task<(ApplicationDbContext ctx, Employee employee)> SeedAsync()
    {
        var ctx = CreateContext();
        var employee = new Employee { EmployeeCode = "E1", FullNameAr = "سارة", FullNameEn = "Sara", HireDate = DateTime.UtcNow };
        ctx.Employees.Add(employee);
        await ctx.SaveChangesAsync();
        return (ctx, employee);
    }

    [Fact]
    public async Task CreateAsync_RejectsFixedTermWithoutAnEndDate()
    {
        var (ctx, employee) = await SeedAsync();
        var service = new EmployeeContractService(ctx);

        await Assert.ThrowsAsync<ValidationAppException>(() => service.CreateAsync(new CreateEmployeeContractDto
        {
            EmployeeId = employee.Id,
            ContractType = ContractType.FixedTerm,
            StartDate = new DateTime(2026, 1, 1)
        }));
    }

    [Fact]
    public async Task CreateAsync_RejectsPermanentWithAnEndDate()
    {
        var (ctx, employee) = await SeedAsync();
        var service = new EmployeeContractService(ctx);

        await Assert.ThrowsAsync<ValidationAppException>(() => service.CreateAsync(new CreateEmployeeContractDto
        {
            EmployeeId = employee.Id,
            ContractType = ContractType.Permanent,
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2027, 1, 1)
        }));
    }

    [Fact]
    public async Task CreateAsync_RenewingAnEmployee_MarksThePreviousContractRenewed()
    {
        var (ctx, employee) = await SeedAsync();
        var service = new EmployeeContractService(ctx);

        var first = await service.CreateAsync(new CreateEmployeeContractDto
        {
            EmployeeId = employee.Id,
            ContractType = ContractType.FixedTerm,
            StartDate = new DateTime(2025, 1, 1),
            EndDate = new DateTime(2026, 1, 1)
        });

        var second = await service.CreateAsync(new CreateEmployeeContractDto
        {
            EmployeeId = employee.Id,
            ContractType = ContractType.FixedTerm,
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2027, 1, 1)
        });

        var history = await service.GetForEmployeeAsync(employee.Id);
        Assert.Equal(2, history.Count);
        Assert.Equal(EmployeeContractStatus.Renewed, history.Single(c => c.Id == first.Id).Status);
        Assert.Equal(EmployeeContractStatus.Active, history.Single(c => c.Id == second.Id).Status);
    }

    [Fact]
    public async Task GetExpiringAsync_ReturnsActiveContractsWithinTheWindow_ButNotRenewedOrFarOutOnes()
    {
        var (ctx, employee) = await SeedAsync();
        var service = new EmployeeContractService(ctx);
        var soon = DateTime.UtcNow.Date.AddDays(10);
        var farOut = DateTime.UtcNow.Date.AddDays(300);

        await service.CreateAsync(new CreateEmployeeContractDto
        {
            EmployeeId = employee.Id,
            ContractType = ContractType.FixedTerm,
            StartDate = DateTime.UtcNow.Date.AddDays(-300),
            EndDate = soon
        });

        var other = new Employee { EmployeeCode = "E2", FullNameAr = "محمد", FullNameEn = "Mohamed", HireDate = DateTime.UtcNow };
        ctx.Employees.Add(other);
        await ctx.SaveChangesAsync();
        await service.CreateAsync(new CreateEmployeeContractDto
        {
            EmployeeId = other.Id,
            ContractType = ContractType.FixedTerm,
            StartDate = DateTime.UtcNow.Date,
            EndDate = farOut
        });

        var expiring = await service.GetExpiringAsync(30);

        var expiringContract = Assert.Single(expiring);
        Assert.Equal(employee.Id, expiringContract.EmployeeId);
        Assert.True(expiringContract.DaysUntilExpiry is >= 9 and <= 10);
    }
}
