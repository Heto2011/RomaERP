using Microsoft.EntityFrameworkCore;
using RomaERP.Application.HR.DTOs;
using RomaERP.Application.HR.Services;
using RomaERP.Domain.HR;
using RomaERP.Infrastructure.Persistence;
using Xunit;

namespace RomaERP.UnitTests;

/// <summary>Nationality is chosen from a list; GOSI eligibility (IsSaudiNational) follows it automatically.</summary>
public class EmployeeNationalityTests
{
    private static async Task<(EmployeeService svc, Department dept, Position pos)> BuildAsync()
    {
        var ctx = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var dept = new Department { Code = "D1", NameAr = "أ", NameEn = "A" };
        var pos = new Position { Code = "P1", TitleAr = "ب", TitleEn = "B", Department = dept, DepartmentId = dept.Id };
        ctx.Departments.Add(dept);
        ctx.Positions.Add(pos);
        await ctx.SaveChangesAsync();
        return (new EmployeeService(ctx), dept, pos);
    }

    private static CreateEmployeeDto Dto(Department d, Position p, string code, string? nationality, bool legacySaudi = false) => new()
    {
        EmployeeCode = code, FullNameAr = "موظف", FullNameEn = "Emp", HireDate = DateTime.UtcNow.Date,
        DepartmentId = d.Id, PositionId = p.Id, BasicSalary = 1000, Nationality = nationality, IsSaudiNational = legacySaudi
    };

    [Theory]
    [InlineData("SA", true)]
    [InlineData("sa", true)]
    [InlineData("EG", false)]
    public async Task Create_StoresNationality_AndDerivesSaudiFlag(string nationality, bool expectSaudi)
    {
        var (svc, d, p) = await BuildAsync();
        var created = await svc.CreateAsync(Dto(d, p, "E1", nationality));
        Assert.Equal(nationality.ToUpperInvariant(), created.Nationality);
        Assert.Equal(expectSaudi, created.IsSaudiNational);
    }

    [Fact]
    public async Task Create_NationalityWinsOverTheLegacyFlag()
    {
        var (svc, d, p) = await BuildAsync();
        var created = await svc.CreateAsync(Dto(d, p, "E2", "EG", legacySaudi: true));
        Assert.False(created.IsSaudiNational);
    }

    [Fact]
    public async Task Create_WithoutNationality_KeepsTheLegacyFlag()
    {
        var (svc, d, p) = await BuildAsync();
        var created = await svc.CreateAsync(Dto(d, p, "E3", null, legacySaudi: true));
        Assert.Null(created.Nationality);
        Assert.True(created.IsSaudiNational);
    }
}
