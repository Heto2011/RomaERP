using System.Reflection;
using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.Restaurant.DTOs;
using RomaERP.Application.Restaurant.Services;
using RomaERP.Domain.Restaurant;
using RomaERP.Domain.Tenancy;
using RomaERP.Infrastructure.Persistence;
using Xunit;

namespace RomaERP.UnitTests;

public class QrOrderingServiceTests
{
    /// <summary>A stand-in for the restaurant service that serves a fixed menu and remembers what was sent to it.</summary>
    public class FakeRestaurant : DispatchProxy
    {
        public List<MenuItemDto> Menu { get; set; } = new();
        public List<string> Calls { get; } = new();
        public Guid OrderId { get; } = Guid.NewGuid();

        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            Calls.Add(method!.Name);
            var ret = method.ReturnType;
            if (method.Name == "GetMenuAsync") return Task.FromResult(Menu);
            if (method.Name is "CreateOrderAsync" or "AddLineAsync")
                return Task.FromResult(new RestaurantOrderDto { Id = OrderId });
            var inner = ret.GetGenericArguments().FirstOrDefault();
            return inner is null ? Task.CompletedTask : typeof(Task).GetMethod("FromResult")!.MakeGenericMethod(inner).Invoke(null, new[] { inner.IsValueType ? Activator.CreateInstance(inner) : null });
        }
    }

    private class User : ICurrentUserService { public string? UserId => "u1"; public string? UserName => "u"; }

    private record Rig(QrOrderingService Svc, ApplicationDbContext Ctx, RestaurantTable Table, Guid ItemId, FakeRestaurant Fake, string Code);

    private static async Task<Rig> BuildAsync(bool enabled = true)
    {
        var ctx = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var table = new RestaurantTable { Number = "5", Capacity = 4 };
        ctx.RestaurantTables.Add(table);
        ctx.CompanySettings.Add(new CompanySettings { CompanyNameAr = "ش", CompanyNameEn = "Co", Country = Country.Egypt, DefaultCurrency = "EGP" });
        await ctx.SaveChangesAsync();
        var item = new MenuItemDto { Id = Guid.NewGuid(), NameAr = "قهوة", MenuPrice = 30 };
        var proxy = DispatchProxy.Create<IRestaurantService, FakeRestaurant>();
        var fake = (FakeRestaurant)(object)proxy;
        fake.Menu.Add(item);
        var svc = new QrOrderingService(ctx, proxy, new User());
        await svc.SetLocationAsync(30.0, 31.0);
        var wh = new RomaERP.Domain.Inventory.Warehouse { Code = "W", NameAr = "م", NameEn = "W", IsActive = true };
        ctx.Warehouses.Add(wh);
        await ctx.SaveChangesAsync();
        await svc.SaveSettingsAsync(new QrOrderingSettingsDto { Enabled = enabled, WarehouseId = wh.Id, RadiusMeters = 50, Latitude = 30.0, Longitude = 31.0 });
        var code = (await svc.GetTableCodesAsync()).Single().Code;
        return new Rig(svc, ctx, table, item.Id, fake, code);
    }

    private static QrOrderSubmitInput Input(Rig r, string? code = null, double lat = 30.0, double lon = 31.0, double acc = 10) =>
        new("5", code ?? r.Code, lat, lon, acc, null, new() { new QrOrderLineInput(r.ItemId, 2, null) });

    [Fact]
    public void DistanceMeters_IsAboutRight()
    {
        // 0.001 degree of latitude is ~111 m.
        var d = QrOrderingService.DistanceMeters(30.0, 31.0, 30.001, 31.0);
        Assert.InRange(d, 105, 117);
    }

    [Fact]
    public async Task Codes_AreStable_UniquePerTable_AndChangeWhenRotated()
    {
        var r = await BuildAsync();
        r.Ctx.RestaurantTables.Add(new RestaurantTable { Number = "6", Capacity = 2 });
        await r.Ctx.SaveChangesAsync();
        var first = await r.Svc.GetTableCodesAsync();
        Assert.Equal(first.Select(c => c.Code), (await r.Svc.GetTableCodesAsync()).Select(c => c.Code));
        Assert.Equal(2, first.Select(c => c.Code).Distinct().Count());
        var rotated = await r.Svc.RotateCodesAsync();
        Assert.NotEqual(first[0].Code, rotated[0].Code);
        Assert.False(await r.Svc.CanOrderAsync("5", first[0].Code));
        Assert.True(await r.Svc.CanOrderAsync("5", rotated[0].Code));
    }

    [Fact]
    public async Task Submit_InsideRadius_SavesPendingRequest()
    {
        var r = await BuildAsync();
        var id = await r.Svc.SubmitAsync(Input(r), "dev1");
        var pending = await r.Svc.GetPendingAsync();
        Assert.Equal(id, Assert.Single(pending).Id);
        Assert.Equal(30m, pending[0].Lines[0].UnitPrice);
    }

    [Fact]
    public async Task Submit_OutsideRadius_IsRefused()
    {
        var r = await BuildAsync();
        await Assert.ThrowsAsync<ValidationAppException>(() => r.Svc.SubmitAsync(Input(r, lat: 30.001), "dev1")); // ~111 m away
    }

    [Fact]
    public async Task Submit_WrongCode_NoLocation_OrVagueLocation_AreRefused()
    {
        var r = await BuildAsync();
        await Assert.ThrowsAsync<ValidationAppException>(() => r.Svc.SubmitAsync(Input(r, code: "wrong-code-xxxxxx"), "d"));
        await Assert.ThrowsAsync<ValidationAppException>(() => r.Svc.SubmitAsync(Input(r) with { Latitude = null }, "d"));
        await Assert.ThrowsAsync<ValidationAppException>(() => r.Svc.SubmitAsync(Input(r, acc: 400), "d"));
    }

    [Fact]
    public async Task Submit_WhenSwitchedOff_IsRefused()
    {
        var r = await BuildAsync(enabled: false);
        await Assert.ThrowsAsync<ValidationAppException>(() => r.Svc.SubmitAsync(Input(r), "d"));
    }

    [Fact]
    public async Task Submit_OneDeviceCannotPileUpRequests()
    {
        var r = await BuildAsync();
        for (var i = 0; i < QrOrderingService.MaxPendingPerDevice; i++) await r.Svc.SubmitAsync(Input(r), "dev1");
        await Assert.ThrowsAsync<ValidationAppException>(() => r.Svc.SubmitAsync(Input(r), "dev1"));
    }

    [Fact]
    public async Task Submit_PriceComesFromMenu_NotFromTheGuest()
    {
        var r = await BuildAsync();
        r.Fake.Menu[0].MenuPrice = 45;
        await r.Svc.SubmitAsync(Input(r), "d");
        Assert.Equal(45m, (await r.Svc.GetPendingAsync())[0].Lines[0].UnitPrice);
    }

    [Fact]
    public async Task Approve_SendsLinesToTheKitchenOrderOnce_AndRejectWorks()
    {
        var r = await BuildAsync();
        var id = await r.Svc.SubmitAsync(Input(r), "d1");
        var orderId = await r.Svc.ApproveAsync(id);
        Assert.Equal(r.Fake.OrderId, orderId);
        Assert.Contains("AddLineAsync", r.Fake.Calls);
        Assert.Equal(QrOrderStatus.Approved, (await r.Svc.GetStatusAsync(id))!.Status);
        await Assert.ThrowsAsync<ValidationAppException>(() => r.Svc.ApproveAsync(id));

        var id2 = await r.Svc.SubmitAsync(Input(r), "d2");
        await r.Svc.RejectAsync(id2);
        Assert.Equal(QrOrderStatus.Rejected, (await r.Svc.GetStatusAsync(id2))!.Status);
        Assert.Empty(await r.Svc.GetPendingAsync());
    }
}
