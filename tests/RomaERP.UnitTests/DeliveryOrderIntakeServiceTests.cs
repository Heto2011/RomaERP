using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting.Services;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.Inventory.Services;
using RomaERP.Application.Restaurant.DTOs;
using RomaERP.Application.Restaurant.Services;
using RomaERP.Application.Sales.Services;
using RomaERP.Domain.Accounting;
using RomaERP.Domain.Common;
using RomaERP.Domain.Inventory;
using RomaERP.Domain.Restaurant;
using RomaERP.Domain.Tenancy;
using RomaERP.Infrastructure.Persistence;
using Xunit;

namespace RomaERP.UnitTests;

/// <summary>Stub IDeliveryPlatformProvider for tests — signature check and parsed payload are both fully
/// controlled by the test instead of doing real HMAC/JSON work.</summary>
public class FakeDeliveryPlatformProvider : IDeliveryPlatformProvider
{
    public string Name { get; init; } = "TestPlatform";
    public bool IsConfigured { get; set; } = true;
    public WebhookVerificationResult VerificationResult { get; set; } = new(true, null);
    public DeliveryOrderPayload? Payload { get; set; }

    public WebhookVerificationResult VerifySignature(string rawBody, string? signatureHeader) => VerificationResult;

    public DeliveryOrderPayload ParseOrderPayload(string rawBody) => Payload ?? JsonSerializer.Deserialize<DeliveryOrderPayload>(rawBody)!;
}

public class DeliveryOrderIntakeServiceTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new ApplicationDbContext(options);
    }

    private record SeedResult(ApplicationDbContext Ctx, Warehouse Warehouse, FiscalPeriod Period, Item Water);

    private static async Task<SeedResult> SeedAsync()
    {
        var ctx = CreateContext();

        var cash = new Account { Code = "1111", NameAr = "الصندوق", NameEn = "Cash", AccountType = AccountType.Asset, Nature = AccountNature.Debit };
        var revenue = new Account { Code = "4100", NameAr = "إيرادات المبيعات", NameEn = "Sales Revenue", AccountType = AccountType.Revenue, Nature = AccountNature.Credit };
        var outputVat = new Account { Code = "2161", NameAr = "ضريبة مخرجات", NameEn = "Output VAT", AccountType = AccountType.Liability, Nature = AccountNature.Credit };
        var cogs = new Account { Code = "5500", NameAr = "تكلفة البضاعة المباعة", NameEn = "COGS", AccountType = AccountType.Expense, Nature = AccountNature.Debit };
        var inventory = new Account { Code = "1160", NameAr = "المخزون", NameEn = "Inventory", AccountType = AccountType.Asset, Nature = AccountNature.Debit };
        var ar = new Account { Code = "1120", NameAr = "العملاء", NameEn = "AR", AccountType = AccountType.Asset, Nature = AccountNature.Debit };

        var today = DateTime.UtcNow.Date;
        var year = new FiscalYear { Name = today.Year.ToString(), StartDate = new DateTime(today.Year, 1, 1), EndDate = new DateTime(today.Year, 12, 31) };
        var period = new FiscalPeriod { FiscalYear = year, FiscalYearId = year.Id, Name = "Current", PeriodNumber = 1, StartDate = today.AddDays(-15), EndDate = today.AddDays(15) };

        var warehouse = new Warehouse { Code = "WH-1", NameAr = "مخزن", NameEn = "Warehouse", IsActive = true };
        var category = new ItemCategory { Code = "CAT-1", NameAr = "تصنيف", NameEn = "Category" };
        var water = new Item { Code = "MENU-WATER", NameAr = "مياه معدنية", NameEn = "Water", UnitOfMeasure = "قطعة", ItemCategoryId = category.Id, IsMenuItem = true, MenuPrice = 10, QuantityOnHand = 50, AverageCost = 5 };

        ctx.Accounts.AddRange(cash, revenue, outputVat, cogs, inventory, ar);
        ctx.FiscalYears.Add(year);
        ctx.FiscalPeriods.Add(period);
        ctx.Warehouses.Add(warehouse);
        ctx.ItemCategories.Add(category);
        ctx.Items.Add(water);
        ctx.CompanySettings.Add(new CompanySettings { CompanyNameAr = "شركة", CompanyNameEn = "Co", Country = Country.Egypt, VatRate = 0.14m, DefaultCurrency = "EGP" });
        await ctx.SaveChangesAsync();

        return new SeedResult(ctx, warehouse, period, water);
    }

    private static DeliveryOrderIntakeService BuildService(ApplicationDbContext ctx, FakeDeliveryPlatformProvider provider)
    {
        var restaurantService = new RestaurantService(
            ctx,
            new SalesService(ctx, new FakeHtmlToPdfRenderer(), new ExchangeRateService(ctx, new FakeExchangeRateProvider())),
            new ItemLotService(ctx),
            new JournalEntryService(ctx));
        return new DeliveryOrderIntakeService(ctx, new[] { provider }, restaurantService);
    }

    private static DeliveryOrderPayload SamplePayload(string externalOrderId = "EXT-1") => new(
        ExternalOrderId: externalOrderId,
        CustomerName: "Ahmed",
        CustomerPhone: "0555555555",
        DeliveryAddress: "شارع النصر",
        Items: new List<DeliveryOrderItemPayload> { new("sku-water", "Water", 2, 10) },
        PlacedAtUtc: DateTime.UtcNow);

    [Fact]
    public async Task ReceiveWebhookAsync_CreatesAndBillsOrder_WhenSignatureValidAndItemMapped()
    {
        var seed = await SeedAsync();
        seed.Ctx.DeliveryPlatformItemMappings.Add(new DeliveryPlatformItemMapping { PlatformName = "TestPlatform", ExternalItemId = "sku-water", ItemId = seed.Water.Id });
        await seed.Ctx.SaveChangesAsync();

        var provider = new FakeDeliveryPlatformProvider { Payload = SamplePayload() };
        var service = BuildService(seed.Ctx, provider);

        var result = await service.ReceiveWebhookAsync("TestPlatform", "{}", "sig", CancellationToken.None);

        Assert.Equal(DeliveryWebhookEventStatus.Processed, result.Status);
        Assert.NotNull(result.CreatedOrderId);

        var order = await seed.Ctx.RestaurantOrders.Include(o => o.Lines).FirstAsync(o => o.Id == result.CreatedOrderId);
        Assert.Equal(RestaurantOrderStatus.Billed, order.Status);
        Assert.Equal("TestPlatform", order.SourcePlatform);
        Assert.Equal("EXT-1", order.ExternalOrderRef);
        Assert.Single(order.Lines);
        Assert.Equal(2, order.Lines.First().Quantity);
    }

    [Fact]
    public async Task ReceiveWebhookAsync_FailsGracefully_WhenSignatureInvalid()
    {
        var seed = await SeedAsync();
        var provider = new FakeDeliveryPlatformProvider { VerificationResult = new WebhookVerificationResult(false, "bad signature"), Payload = SamplePayload() };
        var service = BuildService(seed.Ctx, provider);

        var result = await service.ReceiveWebhookAsync("TestPlatform", "{}", "bad-sig", CancellationToken.None);

        Assert.Equal(DeliveryWebhookEventStatus.Failed, result.Status);
        Assert.Null(result.CreatedOrderId);
        Assert.False(result.IsSignatureVerified);
        Assert.Empty(await seed.Ctx.RestaurantOrders.ToListAsync());
    }

    [Fact]
    public async Task ReceiveWebhookAsync_FailsWithClearError_WhenItemUnmapped()
    {
        var seed = await SeedAsync();
        var provider = new FakeDeliveryPlatformProvider { Payload = SamplePayload() };
        var service = BuildService(seed.Ctx, provider);

        var result = await service.ReceiveWebhookAsync("TestPlatform", "{}", "sig", CancellationToken.None);

        Assert.Equal(DeliveryWebhookEventStatus.Failed, result.Status);
        Assert.Contains("sku-water", result.ErrorMessage);
        Assert.True(result.IsSignatureVerified);
        Assert.Empty(await seed.Ctx.RestaurantOrders.ToListAsync());
    }

    [Fact]
    public async Task ReceiveWebhookAsync_IsIdempotent_OnDuplicateExternalOrderId()
    {
        var seed = await SeedAsync();
        seed.Ctx.DeliveryPlatformItemMappings.Add(new DeliveryPlatformItemMapping { PlatformName = "TestPlatform", ExternalItemId = "sku-water", ItemId = seed.Water.Id });
        await seed.Ctx.SaveChangesAsync();

        var provider = new FakeDeliveryPlatformProvider { Payload = SamplePayload() };
        var service = BuildService(seed.Ctx, provider);

        var first = await service.ReceiveWebhookAsync("TestPlatform", "{}", "sig", CancellationToken.None);
        var second = await service.ReceiveWebhookAsync("TestPlatform", "{}", "sig", CancellationToken.None);

        Assert.Equal(first.CreatedOrderId, second.CreatedOrderId);
        Assert.Single(await seed.Ctx.RestaurantOrders.ToListAsync());
    }

    [Fact]
    public async Task RetryAsync_ReprocessesSuccessfully_AfterMappingIsAdded()
    {
        var seed = await SeedAsync();
        var provider = new FakeDeliveryPlatformProvider { Payload = SamplePayload() };
        var service = BuildService(seed.Ctx, provider);

        var failed = await service.ReceiveWebhookAsync("TestPlatform", "{}", "sig", CancellationToken.None);
        Assert.Equal(DeliveryWebhookEventStatus.Failed, failed.Status);

        seed.Ctx.DeliveryPlatformItemMappings.Add(new DeliveryPlatformItemMapping { PlatformName = "TestPlatform", ExternalItemId = "sku-water", ItemId = seed.Water.Id });
        await seed.Ctx.SaveChangesAsync();

        var retried = await service.RetryAsync(failed.Id, CancellationToken.None);

        Assert.Equal(DeliveryWebhookEventStatus.Processed, retried.Status);
        Assert.NotNull(retried.CreatedOrderId);
    }

    [Fact]
    public async Task RetryAsync_ThrowsWhenSignatureWasNeverVerified()
    {
        var seed = await SeedAsync();
        var provider = new FakeDeliveryPlatformProvider { VerificationResult = new WebhookVerificationResult(false, "bad signature") };
        var service = BuildService(seed.Ctx, provider);

        var failed = await service.ReceiveWebhookAsync("TestPlatform", "{}", "bad-sig", CancellationToken.None);

        await Assert.ThrowsAsync<ValidationAppException>(() => service.RetryAsync(failed.Id, CancellationToken.None));
    }

    [Fact]
    public async Task SetItemMappingAsync_ThenDelete_RoundTrips()
    {
        var seed = await SeedAsync();
        var service = BuildService(seed.Ctx, new FakeDeliveryPlatformProvider());

        var mapping = await service.SetItemMappingAsync(new SaveDeliveryPlatformItemMappingDto
        {
            PlatformName = "TestPlatform",
            ExternalItemId = "sku-water",
            ExternalItemName = "Water",
            ItemId = seed.Water.Id
        });

        Assert.Equal("مياه معدنية", mapping.ItemName);
        Assert.Single(await service.GetItemMappingsAsync("TestPlatform"));

        await service.DeleteItemMappingAsync(mapping.Id);
        Assert.Empty(await service.GetItemMappingsAsync("TestPlatform"));
    }

    [Fact]
    public void GetPlatformStatuses_ReflectsEachProvidersIsConfigured()
    {
        var configured = new FakeDeliveryPlatformProvider { Name = "A", IsConfigured = true };
        var notConfigured = new FakeDeliveryPlatformProvider { Name = "B", IsConfigured = false };
        var service = new DeliveryOrderIntakeService(CreateContext(), new IDeliveryPlatformProvider[] { configured, notConfigured }, restaurantService: null!);

        var statuses = service.GetPlatformStatuses();

        Assert.True(statuses.Single(s => s.Name == "A").IsConfigured);
        Assert.False(statuses.Single(s => s.Name == "B").IsConfigured);
    }
}
