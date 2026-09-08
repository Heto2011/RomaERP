using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.HR.DTOs;
using RomaERP.Application.HR.Services;
using RomaERP.Domain.HR;
using RomaERP.Infrastructure.Persistence;
using Xunit;

namespace RomaERP.UnitTests;

/// <summary>Stub <see cref="IFaceVerificationProvider"/> for tests — mirrors FakeBankFeedProvider's role.</summary>
public class FakeFaceVerificationProvider : IFaceVerificationProvider
{
    public string Name => "Fake";
    public bool IsConfigured { get; set; }
    public FaceVerificationResult Result { get; set; } = new(true, true, 99m, null);

    public Task<FaceVerificationResult> CompareFacesAsync(byte[] referenceImageBytes, byte[] candidateImageBytes, CancellationToken ct = default)
        => Task.FromResult(Result);
}

public class WorkLocationServiceTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task CreateAsync_RejectsOutOfRangeLatitude()
    {
        var service = new WorkLocationService(CreateContext());

        await Assert.ThrowsAsync<ValidationAppException>(() =>
            service.CreateAsync(new SaveWorkLocationDto { Name = "الفرع الرئيسي", Latitude = 999, Longitude = 31, GeofenceRadiusMeters = 20 }));
    }

    [Fact]
    public async Task DeleteAsync_RejectsWhenEmployeesStillLinked()
    {
        var ctx = CreateContext();
        var service = new WorkLocationService(ctx);
        var location = await service.CreateAsync(new SaveWorkLocationDto { Name = "الفرع الرئيسي", Latitude = 30.05m, Longitude = 31.23m, GeofenceRadiusMeters = 20 });

        ctx.Employees.Add(new Employee { EmployeeCode = "E1", FullNameAr = "أ", FullNameEn = "A", HireDate = DateTime.UtcNow, WorkLocationId = location.Id });
        await ctx.SaveChangesAsync();

        await Assert.ThrowsAsync<ValidationAppException>(() => service.DeleteAsync(location.Id));
    }
}

public class AttendanceServiceTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new ApplicationDbContext(options);
    }

    private static async Task<(ApplicationDbContext ctx, Employee employee, WorkLocation location)> SeedAsync()
    {
        var ctx = CreateContext();
        var location = new WorkLocation { Name = "المقر", Latitude = 30.0444m, Longitude = 31.2357m, GeofenceRadiusMeters = 20 };
        var employee = new Employee { EmployeeCode = "E1", FullNameAr = "أحمد", FullNameEn = "Ahmed", HireDate = DateTime.UtcNow, WorkLocation = location };
        ctx.WorkLocations.Add(location);
        ctx.Employees.Add(employee);
        await ctx.SaveChangesAsync();
        return (ctx, employee, location);
    }

    [Fact]
    public async Task CheckInAsync_MarksWithinGeofenceWhenAtTheSameCoordinates()
    {
        var (ctx, employee, location) = await SeedAsync();
        var service = new AttendanceService(ctx, new FakeFaceVerificationProvider());

        var record = await service.CheckInAsync(employee.Id, location.Latitude, location.Longitude, null, null);

        Assert.True(record.CheckInWithinGeofence);
        Assert.Null(record.CheckInFaceVerified);
    }

    [Fact]
    public async Task CheckInAsync_MarksOutsideGeofenceWhenFarAway()
    {
        var (ctx, employee, _) = await SeedAsync();
        var service = new AttendanceService(ctx, new FakeFaceVerificationProvider());

        var record = await service.CheckInAsync(employee.Id, 31.2001m, 29.9187m, null, null);

        Assert.False(record.CheckInWithinGeofence);
    }

    [Fact]
    public async Task CheckInAsync_ThrowsWhenAnAttendanceIsAlreadyOpen()
    {
        var (ctx, employee, location) = await SeedAsync();
        var service = new AttendanceService(ctx, new FakeFaceVerificationProvider());
        await service.CheckInAsync(employee.Id, location.Latitude, location.Longitude, null, null);

        await Assert.ThrowsAsync<ValidationAppException>(() =>
            service.CheckInAsync(employee.Id, location.Latitude, location.Longitude, null, null));
    }

    [Fact]
    public async Task CheckOutAsync_ThrowsWhenNoOpenAttendanceExists()
    {
        var (ctx, employee, location) = await SeedAsync();
        var service = new AttendanceService(ctx, new FakeFaceVerificationProvider());

        await Assert.ThrowsAsync<ValidationAppException>(() =>
            service.CheckOutAsync(employee.Id, location.Latitude, location.Longitude, null, null));
    }

    [Fact]
    public async Task CheckInAsync_UsesFaceProviderOnlyWhenConfiguredAndBothPhotosPresent()
    {
        var (ctx, employee, location) = await SeedAsync();
        var provider = new FakeFaceVerificationProvider { IsConfigured = true, Result = new FaceVerificationResult(true, true, 97.5m, null) };
        var service = new AttendanceService(ctx, provider);

        var record = await service.CheckInAsync(employee.Id, location.Latitude, location.Longitude, [1, 2, 3], [4, 5, 6]);

        Assert.True(record.CheckInFaceVerified);
        Assert.Equal(97.5m, record.CheckInFaceSimilarityPercent);
    }

    [Fact]
    public async Task CheckInThenCheckOut_ProducesAClosedRecordWithBothStamps()
    {
        var (ctx, employee, location) = await SeedAsync();
        var service = new AttendanceService(ctx, new FakeFaceVerificationProvider());
        await service.CheckInAsync(employee.Id, location.Latitude, location.Longitude, null, null);

        var record = await service.CheckOutAsync(employee.Id, location.Latitude, location.Longitude, null, null);

        Assert.NotNull(record.CheckOutAtUtc);
        Assert.True(record.CheckOutWithinGeofence);
    }
}

public class EmployeeRequestServiceTests
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
    public async Task CreateAsync_RejectsDateToBeforeDateFrom()
    {
        var (ctx, employee) = await SeedAsync();
        var service = new EmployeeRequestService(ctx);

        await Assert.ThrowsAsync<ValidationAppException>(() =>
            service.CreateAsync(employee.Id, new CreateEmployeeRequestDto { Type = EmployeeRequestType.Leave, DateFrom = new DateTime(2026, 9, 10), DateTo = new DateTime(2026, 9, 5) }));
    }

    [Fact]
    public async Task DecideAsync_ApprovesAPendingRequestThenRejectsASecondDecision()
    {
        var (ctx, employee) = await SeedAsync();
        var service = new EmployeeRequestService(ctx);
        var request = await service.CreateAsync(employee.Id, new CreateEmployeeRequestDto { Type = EmployeeRequestType.Leave, DateFrom = new DateTime(2026, 9, 10), DateTo = new DateTime(2026, 9, 11) });

        var decided = await service.DecideAsync(request.Id, Guid.NewGuid(), new DecideEmployeeRequestDto { Approve = true });
        Assert.Equal(EmployeeRequestStatus.Approved, decided.Status);

        await Assert.ThrowsAsync<ValidationAppException>(() =>
            service.DecideAsync(request.Id, Guid.NewGuid(), new DecideEmployeeRequestDto { Approve = false }));
    }

    [Fact]
    public async Task GetPendingAsync_OnlyReturnsPendingRequests()
    {
        var (ctx, employee) = await SeedAsync();
        var service = new EmployeeRequestService(ctx);
        var approved = await service.CreateAsync(employee.Id, new CreateEmployeeRequestDto { Type = EmployeeRequestType.Permission, DateFrom = new DateTime(2026, 9, 1) });
        await service.CreateAsync(employee.Id, new CreateEmployeeRequestDto { Type = EmployeeRequestType.Leave, DateFrom = new DateTime(2026, 9, 5) });
        await service.DecideAsync(approved.Id, Guid.NewGuid(), new DecideEmployeeRequestDto { Approve = true });

        var pending = await service.GetPendingAsync();

        Assert.Single(pending);
    }
}
