using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Restaurant.DTOs;
using RomaERP.Application.Restaurant.Services;
using RomaERP.Domain.Common;
using RomaERP.Domain.HR;
using RomaERP.Domain.Inventory;
using RomaERP.Domain.Restaurant;
using RomaERP.Domain.Sales;
using RomaERP.Infrastructure.Persistence;
using Xunit;

namespace RomaERP.UnitTests;

public class CashierShiftServiceTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static Employee CreateEmployee()
        => new() { EmployeeCode = "EMP-001", FullNameAr = "كاشير تجريبي", FullNameEn = "Test Cashier", HireDate = new DateTime(2025, 1, 1), BasicSalary = 5000 };

    [Fact]
    public async Task OpenAsync_CreatesShiftVisibleViaGetActiveShift()
    {
        var ctx = CreateContext();
        var employee = CreateEmployee();
        ctx.Employees.Add(employee);
        await ctx.SaveChangesAsync();

        var service = new CashierShiftService(ctx);
        var opened = await service.OpenAsync(new OpenCashierShiftDto { EmployeeId = employee.Id, OpeningFloat = 500 });

        Assert.Equal(500, opened.OpeningFloat);
        Assert.Equal(1, opened.Status); // Open

        var active = await service.GetActiveShiftAsync(employee.Id);
        Assert.NotNull(active);
        Assert.Equal(opened.Id, active!.Id);
    }

    [Fact]
    public async Task OpenAsync_RejectsSecondOpenShiftForSameEmployee()
    {
        var ctx = CreateContext();
        var employee = CreateEmployee();
        ctx.Employees.Add(employee);
        await ctx.SaveChangesAsync();

        var service = new CashierShiftService(ctx);
        await service.OpenAsync(new OpenCashierShiftDto { EmployeeId = employee.Id, OpeningFloat = 500 });

        await Assert.ThrowsAsync<ValidationAppException>(() =>
            service.OpenAsync(new OpenCashierShiftDto { EmployeeId = employee.Id, OpeningFloat = 300 }));
    }

    [Fact]
    public async Task CloseAsync_ComputesExpectedCashFromCashOrdersUnderTheShiftAndReportsVariance()
    {
        var ctx = CreateContext();
        var employee = CreateEmployee();
        var customer = new Customer { Code = "WALK-IN", NameAr = "عميل نقدي", NameEn = "Walk-in Customer" };
        var warehouse = new Warehouse { Code = "WH-MAIN", NameAr = "المخزن الرئيسي", NameEn = "Main Warehouse" };
        ctx.Employees.Add(employee);
        ctx.Customers.Add(customer);
        ctx.Warehouses.Add(warehouse);
        await ctx.SaveChangesAsync();

        var service = new CashierShiftService(ctx);
        var shift = await service.OpenAsync(new OpenCashierShiftDto { EmployeeId = employee.Id, OpeningFloat = 200 });

        var cashInvoice = new SalesInvoice
        {
            InvoiceNumber = "SI-1", InvoiceDate = DateTime.UtcNow.Date, CustomerId = customer.Id,
            PaymentTerm = PaymentTerm.Cash, SubTotal = 100, TotalAmount = 100, PaidAmount = 100
        };
        var cardInvoice = new SalesInvoice
        {
            InvoiceNumber = "SI-2", InvoiceDate = DateTime.UtcNow.Date, CustomerId = customer.Id,
            PaymentTerm = PaymentTerm.Card, SubTotal = 250, TotalAmount = 250, PaidAmount = 250
        };
        ctx.SalesInvoices.AddRange(cashInvoice, cardInvoice);
        await ctx.SaveChangesAsync();

        ctx.RestaurantOrders.AddRange(
            new RestaurantOrder { OrderNumber = "RO-1", OrderType = RestaurantOrderType.DineIn, OrderDate = DateTime.UtcNow.Date, WarehouseId = warehouse.Id, Status = RestaurantOrderStatus.Billed, SalesInvoiceId = cashInvoice.Id, CashierShiftId = shift.Id },
            new RestaurantOrder { OrderNumber = "RO-2", OrderType = RestaurantOrderType.DineIn, OrderDate = DateTime.UtcNow.Date, WarehouseId = warehouse.Id, Status = RestaurantOrderStatus.Billed, SalesInvoiceId = cardInvoice.Id, CashierShiftId = shift.Id }
        );
        await ctx.SaveChangesAsync();

        var closed = await service.CloseAsync(shift.Id, new CloseCashierShiftDto { ClosingCountedCash = 290 });

        // Expected = OpeningFloat (200) + cash orders only (100) = 300; card order (250) is excluded.
        Assert.Equal(300, closed.ExpectedCash);
        Assert.Equal(290, closed.ClosingCountedCash);
        Assert.Equal(-10, closed.CashVariance);
        Assert.Equal(2, closed.Status); // Closed
    }

    [Fact]
    public async Task CloseAsync_RejectsAlreadyClosedShift()
    {
        var ctx = CreateContext();
        var employee = CreateEmployee();
        ctx.Employees.Add(employee);
        await ctx.SaveChangesAsync();

        var service = new CashierShiftService(ctx);
        var shift = await service.OpenAsync(new OpenCashierShiftDto { EmployeeId = employee.Id, OpeningFloat = 100 });
        await service.CloseAsync(shift.Id, new CloseCashierShiftDto { ClosingCountedCash = 100 });

        await Assert.ThrowsAsync<ValidationAppException>(() =>
            service.CloseAsync(shift.Id, new CloseCashierShiftDto { ClosingCountedCash = 100 }));
    }
}
