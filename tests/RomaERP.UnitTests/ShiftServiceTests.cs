using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.HR.Services;
using RomaERP.Domain.HR;
using RomaERP.Infrastructure.Persistence;
using Xunit;

namespace RomaERP.UnitTests;

public class ShiftServiceTests
{
    private static ApplicationDbContext CreateContext()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<(ApplicationDbContext ctx, ShiftService service, Employee a, Employee b)> SeedAsync()
    {
        var ctx = CreateContext();
        var a = new Employee { EmployeeCode = "E1", FullNameAr = "أحمد", FullNameEn = "Ahmed", HireDate = new DateTime(2025, 1, 1) };
        var b = new Employee { EmployeeCode = "E2", FullNameAr = "منى", FullNameEn = "Mona", HireDate = new DateTime(2025, 1, 1) };
        ctx.Employees.AddRange(a, b);
        await ctx.SaveChangesAsync();
        return (ctx, new ShiftService(ctx), a, b);
    }

    [Fact]
    public async Task Shift_ParsesTimes_FlagsOvernight_AndRejectsBadInput()
    {
        var (_, service, _, _) = await SeedAsync();
        var night = await service.CreateShiftAsync(new ShiftDto { Name = "ليلي", StartTime = "22:00", EndTime = "06:00" });
        Assert.True(night.IsOvernight);
        Assert.Equal("22:00", night.StartTime);
        var day = await service.CreateShiftAsync(new ShiftDto { Name = "صباحي", StartTime = "8:00", EndTime = "16:30" });
        Assert.False(day.IsOvernight);
        Assert.Equal("08:00", day.StartTime);

        await Assert.ThrowsAsync<ValidationAppException>(() => service.CreateShiftAsync(new ShiftDto { Name = "x", StartTime = "25:00", EndTime = "06:00" }));
        await Assert.ThrowsAsync<ValidationAppException>(() => service.CreateShiftAsync(new ShiftDto { Name = "x", StartTime = "08:00", EndTime = "08:00" }));
        await Assert.ThrowsAsync<ValidationAppException>(() => service.CreateShiftAsync(new ShiftDto { Name = " ", StartTime = "08:00", EndTime = "09:00" }));
    }

    [Fact]
    public async Task Roster_AssignChangeClear_CopyWeek_AndDeleteProtection()
    {
        var (ctx, service, a, b) = await SeedAsync();
        var morning = await service.CreateShiftAsync(new ShiftDto { Name = "صباحي", StartTime = "08:00", EndTime = "16:00" });
        var evening = await service.CreateShiftAsync(new ShiftDto { Name = "مسائي", StartTime = "16:00", EndTime = "00:00" });
        var monday = new DateTime(2026, 10, 12);

        await service.SetAssignmentAsync(new SetShiftAssignmentDto { EmployeeId = a.Id, Date = monday, ShiftId = morning.Id });
        await service.SetAssignmentAsync(new SetShiftAssignmentDto { EmployeeId = a.Id, Date = monday.AddDays(1), ShiftId = morning.Id });
        await service.SetAssignmentAsync(new SetShiftAssignmentDto { EmployeeId = b.Id, Date = monday, ShiftId = evening.Id });
        // changing a day replaces it instead of adding a second shift
        await service.SetAssignmentAsync(new SetShiftAssignmentDto { EmployeeId = a.Id, Date = monday, ShiftId = evening.Id });
        Assert.Equal(3, await ctx.ShiftAssignments.CountAsync());
        Assert.Equal(evening.Id, (await ctx.ShiftAssignments.SingleAsync(x => x.EmployeeId == a.Id && x.Date == monday)).WorkShiftId);

        // clearing a day makes the employee off again
        await service.SetAssignmentAsync(new SetShiftAssignmentDto { EmployeeId = a.Id, Date = monday.AddDays(1), ShiftId = null });
        Assert.Equal(2, await ctx.ShiftAssignments.CountAsync());

        var copied = await service.CopyWeekAsync(new CopyRosterWeekDto { FromStart = monday, ToStart = monday.AddDays(7) });
        Assert.Equal(2, copied);
        var next = await service.GetRosterAsync(monday.AddDays(7), monday.AddDays(13), null);
        Assert.Equal(2, next.Count);
        Assert.Contains(next, r => r.EmployeeId == b.Id && r.ShiftName == "مسائي" && r.Date == monday.AddDays(7));

        // copying again replaces, it never doubles
        await service.CopyWeekAsync(new CopyRosterWeekDto { FromStart = monday, ToStart = monday.AddDays(7) });
        Assert.Equal(4, await ctx.ShiftAssignments.CountAsync());

        // my schedule = only that employee
        var mine = await service.GetRosterAsync(monday, monday.AddDays(13), a.Id);
        Assert.All(mine, r => Assert.Equal(a.Id, r.EmployeeId));

        await Assert.ThrowsAsync<ValidationAppException>(() => service.DeleteShiftAsync(evening.Id));
        var unused = await service.CreateShiftAsync(new ShiftDto { Name = "تجريبي", StartTime = "10:00", EndTime = "11:00" });
        await service.DeleteShiftAsync(unused.Id);
        Assert.DoesNotContain(await service.GetShiftsAsync(), s => s.Id == unused.Id);
    }

    [Fact]
    public async Task Roster_RejectsInactiveShiftAndHugeRange()
    {
        var (_, service, a, _) = await SeedAsync();
        var off = await service.CreateShiftAsync(new ShiftDto { Name = "قديم", StartTime = "08:00", EndTime = "16:00", IsActive = false });
        await Assert.ThrowsAsync<ValidationAppException>(() => service.SetAssignmentAsync(new SetShiftAssignmentDto { EmployeeId = a.Id, Date = DateTime.Today, ShiftId = off.Id }));
        await Assert.ThrowsAsync<ValidationAppException>(() => service.GetRosterAsync(DateTime.Today, DateTime.Today.AddDays(120), null));
    }
}
