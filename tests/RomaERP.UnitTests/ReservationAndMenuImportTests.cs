using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Restaurant.DTOs;
using RomaERP.Application.Restaurant.Services;
using RomaERP.Domain.Common;
using RomaERP.Domain.Restaurant;
using Xunit;

namespace RomaERP.UnitTests;

public class ReservationAndMenuImportTests
{
    private static SaveReservationDto Booking(Guid? tableId, DateTime at, int size = 2) => new()
    {
        CustomerName = "أحمد", CustomerPhone = "0100", PartySize = size, ReservationAt = at, TableId = tableId
    };

    private static DateTime Tomorrow(int hour) => DateTime.UtcNow.Date.AddDays(1).AddHours(hour);

    [Fact]
    public async Task Reservation_ThenSeat_DineIn_OpensOrderOnTable_ThenBillsIntoInvoice()
    {
        var seed = await RestaurantServiceTests.SeedAsync();
        var restaurant = RestaurantServiceTests.BuildService(seed.Ctx);
        var service = new ReservationService(seed.Ctx, restaurant);

        var reservation = await service.CreateAsync(Booking(seed.Table.Id, Tomorrow(20), 3));
        Assert.StartsWith("RS-", reservation.ReservationNumber);
        Assert.Equal(ReservationStatus.Booked, reservation.Status);

        var seated = await service.SeatAsync(reservation.Id, new SeatReservationDto { OrderType = RestaurantOrderType.DineIn, WarehouseId = seed.Warehouse.Id });
        Assert.Equal(ReservationStatus.Seated, seated.Reservation.Status);
        Assert.Equal(seated.Order.Id, seated.Reservation.RestaurantOrderId);
        Assert.Equal(RestaurantOrderType.DineIn, seated.Order.OrderType);
        Assert.Equal(seed.Table.Id, seated.Order.TableId);

        await restaurant.AddLineAsync(seated.Order.Id, new AddOrderLineDto { ItemId = seed.Water.Id, Quantity = 2 });
        var billed = await restaurant.BillOrderAsync(seated.Order.Id, new BillOrderDto { PaymentTerm = PaymentTerm.Cash, FiscalPeriodId = seed.Period.Id });
        Assert.Equal(RestaurantOrderStatus.Billed, billed.Status);
        Assert.NotNull(billed.SalesInvoiceId);
    }

    [Fact]
    public async Task Seat_AsTakeaway_NeedsNoTable()
    {
        var seed = await RestaurantServiceTests.SeedAsync();
        var service = new ReservationService(seed.Ctx, RestaurantServiceTests.BuildService(seed.Ctx));
        var reservation = await service.CreateAsync(Booking(seed.Table.Id, Tomorrow(19)));

        var seated = await service.SeatAsync(reservation.Id, new SeatReservationDto { OrderType = RestaurantOrderType.Takeaway, WarehouseId = seed.Warehouse.Id });

        Assert.Equal(RestaurantOrderType.Takeaway, seated.Order.OrderType);
        Assert.Null(seated.Order.TableId);
    }

    [Fact]
    public async Task SameTableWithinTwoHours_IsRejected_ButLaterIsFine()
    {
        var seed = await RestaurantServiceTests.SeedAsync();
        var service = new ReservationService(seed.Ctx, RestaurantServiceTests.BuildService(seed.Ctx));
        await service.CreateAsync(Booking(seed.Table.Id, Tomorrow(20)));

        await Assert.ThrowsAsync<ValidationAppException>(() => service.CreateAsync(Booking(seed.Table.Id, Tomorrow(21))));
        var later = await service.CreateAsync(Booking(seed.Table.Id, Tomorrow(22)));
        Assert.Equal(ReservationStatus.Booked, later.Status);
    }

    [Fact]
    public async Task PartyLargerThanTable_AndBadInput_AreRejected()
    {
        var seed = await RestaurantServiceTests.SeedAsync();
        var service = new ReservationService(seed.Ctx, RestaurantServiceTests.BuildService(seed.Ctx));

        await Assert.ThrowsAsync<ValidationAppException>(() => service.CreateAsync(Booking(seed.Table.Id, Tomorrow(20), 9)));
        await Assert.ThrowsAsync<ValidationAppException>(() => service.CreateAsync(Booking(null, Tomorrow(20), 0)));
        await Assert.ThrowsAsync<ValidationAppException>(() => service.CreateAsync(new SaveReservationDto { CustomerName = " ", CustomerPhone = "1", PartySize = 2, ReservationAt = Tomorrow(20) }));
        await Assert.ThrowsAsync<ValidationAppException>(() => service.CreateAsync(Booking(null, DateTime.UtcNow.AddYears(3))));
    }

    [Fact]
    public async Task CancelledOrSeatedReservation_CannotBeChangedOrSeatedAgain()
    {
        var seed = await RestaurantServiceTests.SeedAsync();
        var service = new ReservationService(seed.Ctx, RestaurantServiceTests.BuildService(seed.Ctx));
        var cancelled = await service.CreateAsync(Booking(null, Tomorrow(18)));
        await service.CancelAsync(cancelled.Id);

        await Assert.ThrowsAsync<ValidationAppException>(() => service.SeatAsync(cancelled.Id, new SeatReservationDto { OrderType = RestaurantOrderType.Takeaway, WarehouseId = seed.Warehouse.Id }));
        await Assert.ThrowsAsync<ValidationAppException>(() => service.UpdateAsync(cancelled.Id, Booking(null, Tomorrow(19))));

        // A cancelled booking frees its table for someone else.
        var again = await service.CreateAsync(Booking(seed.Table.Id, Tomorrow(18)));
        var other = await service.CreateAsync(Booking(null, Tomorrow(17)));
        await service.SeatAsync(other.Id, new SeatReservationDto { OrderType = RestaurantOrderType.Takeaway, WarehouseId = seed.Warehouse.Id });
        await Assert.ThrowsAsync<ValidationAppException>(() => service.MarkNoShowAsync(other.Id));
        Assert.Equal(ReservationStatus.Booked, again.Status);
    }

    [Fact]
    public async Task Seat_RejectsDeliveryType_AndOccupiedTable()
    {
        var seed = await RestaurantServiceTests.SeedAsync();
        var restaurant = RestaurantServiceTests.BuildService(seed.Ctx);
        var service = new ReservationService(seed.Ctx, restaurant);
        var reservation = await service.CreateAsync(Booking(seed.Table.Id, Tomorrow(20)));

        await Assert.ThrowsAsync<ValidationAppException>(() => service.SeatAsync(reservation.Id, new SeatReservationDto { OrderType = RestaurantOrderType.Delivery, WarehouseId = seed.Warehouse.Id }));

        await restaurant.CreateOrderAsync(new CreateRestaurantOrderDto { OrderType = RestaurantOrderType.DineIn, TableId = seed.Table.Id, WarehouseId = seed.Warehouse.Id });
        await Assert.ThrowsAsync<ValidationAppException>(() => service.SeatAsync(reservation.Id, new SeatReservationDto { OrderType = RestaurantOrderType.DineIn, WarehouseId = seed.Warehouse.Id }));
        Assert.Equal(ReservationStatus.Booked, (await service.GetAsync(null, null)).Single().Status);
    }

    // ---------- menu import ----------

    private static MenuImportRowDto Row(string cat, string name, decimal price) => new() { Category = cat, NameAr = name, Price = price };

    [Fact]
    public async Task MenuImport_Preview_SavesNothing_ThenApply_CreatesMenuBilledWithoutStock()
    {
        var seed = await RestaurantServiceTests.SeedAsync();
        var import = new MenuImportService(seed.Ctx);
        var rows = new List<MenuImportRowDto> { Row("مشروبات", "شاي", 15), Row("مشروبات", "قهوة", 25), Row("حلويات", "كنافة", 60) };
        var itemsBefore = await seed.Ctx.Items.CountAsync();

        var preview = await import.ImportAsync(new MenuImportRequestDto { Rows = rows });
        Assert.False(preview.Applied);
        Assert.Equal(3, preview.Created);
        Assert.Equal(2, preview.NewCategories);
        Assert.Equal(itemsBefore, await seed.Ctx.Items.CountAsync());

        var applied = await import.ImportAsync(new MenuImportRequestDto { Rows = rows, Apply = true });
        Assert.Equal(3, applied.Created);
        Assert.Equal(itemsBefore + 3, await seed.Ctx.Items.CountAsync());

        // An imported dish has no stock, yet it must still bill.
        var restaurant = RestaurantServiceTests.BuildService(seed.Ctx);
        var tea = await seed.Ctx.Items.FirstAsync(i => i.NameAr == "شاي");
        Assert.True(tea.IsMenuItem);
        var order = await restaurant.CreateOrderAsync(new CreateRestaurantOrderDto { OrderType = RestaurantOrderType.Takeaway, WarehouseId = seed.Warehouse.Id });
        await restaurant.AddLineAsync(order.Id, new AddOrderLineDto { ItemId = tea.Id, Quantity = 4 });
        var billed = await restaurant.BillOrderAsync(order.Id, new BillOrderDto { PaymentTerm = PaymentTerm.Cash, FiscalPeriodId = seed.Period.Id });
        Assert.Equal(RestaurantOrderStatus.Billed, billed.Status);
        Assert.Equal(60, billed.SubTotal);
    }

    [Fact]
    public async Task MenuImport_ReimportingIsSafe_AndPriceUpdateIsOptIn()
    {
        var seed = await RestaurantServiceTests.SeedAsync();
        var import = new MenuImportService(seed.Ctx);
        await import.ImportAsync(new MenuImportRequestDto { Rows = new() { Row("مشروبات", "شاي", 15) }, Apply = true });

        var same = await import.ImportAsync(new MenuImportRequestDto { Rows = new() { Row("مشروبات", "شاي", 18) }, Apply = true });
        Assert.Equal(0, same.Created);
        Assert.Equal(15, (await seed.Ctx.Items.FirstAsync(i => i.NameAr == "شاي")).MenuPrice);

        var updated = await import.ImportAsync(new MenuImportRequestDto { Rows = new() { Row("مشروبات", "شاي", 18) }, Apply = true, UpdateExistingPrices = true });
        Assert.Equal(1, updated.Updated);
        Assert.Equal(18, (await seed.Ctx.Items.FirstAsync(i => i.NameAr == "شاي")).MenuPrice);
        Assert.Equal(1, await seed.Ctx.Items.CountAsync(i => i.NameAr == "شاي"));
    }

    [Fact]
    public async Task MenuImport_AnyBadRow_BlocksTheWholeSave()
    {
        var seed = await RestaurantServiceTests.SeedAsync();
        var import = new MenuImportService(seed.Ctx);
        var rows = new List<MenuImportRowDto> { Row("مشروبات", "شاي", 15), Row("", "بدون قسم", 5), Row("مشروبات", "شاي", 9), Row("مشروبات", "سالب", -1), Row("مخزون", "دقيق", 3) };
        var itemsBefore = await seed.Ctx.Items.CountAsync();

        var preview = await import.ImportAsync(new MenuImportRequestDto { Rows = rows });
        Assert.Equal(4, preview.Errors);
        Assert.Equal("create", preview.Lines[0].Action);

        await Assert.ThrowsAsync<ValidationAppException>(() => import.ImportAsync(new MenuImportRequestDto { Rows = rows, Apply = true }));
        Assert.Equal(itemsBefore, await seed.Ctx.Items.CountAsync());
        await Assert.ThrowsAsync<ValidationAppException>(() => import.ImportAsync(new MenuImportRequestDto()));
    }
}
