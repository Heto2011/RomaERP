using System.Text;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.EInvoicing.DTOs;
using RomaERP.Application.EInvoicing.Services;
using RomaERP.Application.EInvoicing.Services.Eta;
using RomaERP.Application.EInvoicing.Services.Zatca;
using RomaERP.Domain.Accounting;
using RomaERP.Domain.EInvoicing;
using RomaERP.Domain.Sales;
using RomaERP.Domain.Tenancy;
using RomaERP.Infrastructure.Persistence;
using Xunit;

namespace RomaERP.UnitTests;

/// <summary>No-op stand-in for the real Data Protection-backed ISecretProtector — good enough for unit tests
/// that only need round-tripping, not real encryption.</summary>
public class PlainTextSecretProtector : ISecretProtector
{
    public string Protect(string plaintext) => $"protected:{plaintext}";
    public string Unprotect(string protectedText) => protectedText.Replace("protected:", "");
}

public class EInvoicingTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static Customer B2BCustomer() => new() { Code = "CUST-1", NameAr = "شركة تجريبية", NameEn = "Test Co", TaxRegistrationNumber = "300123456700003" };
    private static Customer B2CCustomer() => new() { Code = "CUST-2", NameAr = "عميل فردي", NameEn = "Individual" };

    private static SalesInvoice NewInvoice(Customer customer, decimal subTotal = 1000, decimal vatRate = 0.15m)
    {
        var vat = subTotal * vatRate;
        return new SalesInvoice
        {
            InvoiceNumber = "SI-000001",
            InvoiceDate = DateTime.UtcNow.Date,
            Customer = customer,
            CustomerId = customer.Id,
            SubTotal = subTotal,
            VatRate = vatRate,
            VatAmount = vat,
            TotalAmount = subTotal + vat,
            Lines = new List<SalesInvoiceLine>
            {
                new() { LineNumber = 1, Description = "منتج تجريبي", Quantity = 2, UnitPrice = 500, LineTotal = subTotal }
            }
        };
    }

    private static CompanySettings SaudiSettings() => new()
    {
        CompanyNameAr = "شركة روما التجريبية",
        CompanyNameEn = "Roma Test Co",
        Country = Country.SaudiArabia,
        TaxRegistrationNumber = "300987654300003",
        VatRate = 0.15m,
        DefaultCurrency = "SAR"
    };

    // ---------- ZATCA invoice-type routing ----------

    [Fact]
    public void ZatcaDetermineInvoiceType_CustomerWithTaxNumber_IsStandard()
    {
        Assert.Equal(ZatcaInvoiceType.Standard, ZatcaInvoiceDocumentBuilder.DetermineInvoiceType(B2BCustomer()));
    }

    [Fact]
    public void ZatcaDetermineInvoiceType_CustomerWithoutTaxNumber_IsSimplified()
    {
        Assert.Equal(ZatcaInvoiceType.Simplified, ZatcaInvoiceDocumentBuilder.DetermineInvoiceType(B2CCustomer()));
    }

    // ---------- ZATCA document + QR building ----------

    [Fact]
    public void ZatcaInvoiceDocumentBuilder_Build_IncludesIcvAndPihAndTotals()
    {
        var settings = SaudiSettings();
        var customer = B2BCustomer();
        var invoice = NewInvoice(customer);

        var (document, uuid) = ZatcaInvoiceDocumentBuilder.Build(invoice, customer, settings, invoiceCounterValue: 7, previousInvoiceHash: "PREV-HASH", qrCodeBase64: "QR-DATA");

        Assert.False(string.IsNullOrWhiteSpace(uuid));
        var xml = document.ToString();
        Assert.Contains("7", xml);
        Assert.Contains("PREV-HASH", xml);
        Assert.Contains("QR-DATA", xml);
        Assert.Contains(invoice.TotalAmount.ToString(), xml);
    }

    [Fact]
    public void ZatcaQrCodeBuilder_Build_RoundTripsAllFiveTlvFields()
    {
        var qr = ZatcaQrCodeBuilder.Build("شركة روما", "300987654300003", new DateTime(2026, 8, 23, 10, 0, 0, DateTimeKind.Utc), 1150.00m, 150.00m);
        var bytes = Convert.FromBase64String(qr);

        var fields = new Dictionary<byte, string>();
        var i = 0;
        while (i < bytes.Length)
        {
            var tag = bytes[i];
            var len = bytes[i + 1];
            var value = Encoding.UTF8.GetString(bytes, i + 2, len);
            fields[tag] = value;
            i += 2 + len;
        }

        Assert.Equal("شركة روما", fields[1]);
        Assert.Equal("300987654300003", fields[2]);
        Assert.Equal("1150.00", fields[4]);
        Assert.Equal("150.00", fields[5]);
    }

    // ---------- ETA document building ----------

    [Fact]
    public void EtaInvoiceDocumentBuilder_Build_MarksReceiverTypeByTaxNumberPresence()
    {
        var settings = SaudiSettings();
        settings.Country = Country.Egypt;
        settings.DefaultCurrency = "EGP";

        var b2b = EtaInvoiceDocumentBuilder.Build(NewInvoice(B2BCustomer()), B2BCustomer(), settings);
        var b2c = EtaInvoiceDocumentBuilder.Build(NewInvoice(B2CCustomer()), B2CCustomer(), settings);

        Assert.Equal("B", b2b["receiver"]!["type"]!.ToString());
        Assert.Equal("P", b2c["receiver"]!["type"]!.ToString());
    }

    // ---------- End-to-end submission via EInvoicingService (mock providers) ----------

    private static EInvoicingService BuildService(ApplicationDbContext ctx) => new(
        ctx,
        new PlainTextSecretProtector(),
        new IEInvoicingProvider[]
        {
            new EtaEInvoicingProvider(new MockEtaDocumentSigner(), new MockEtaApiClient()),
            new ZatcaEInvoicingProvider(new MockZatcaDocumentSigner(), new MockZatcaApiClient())
        });

    [Fact]
    public async Task SubmitInvoice_WhenProviderNotConfigured_Throws()
    {
        var ctx = CreateContext();
        var settings = SaudiSettings();
        settings.EInvoicingProvider = EInvoicingProvider.None;
        var customer = B2BCustomer();
        var invoice = NewInvoice(customer);
        ctx.CompanySettings.Add(settings);
        ctx.Customers.Add(customer);
        ctx.SalesInvoices.Add(invoice);
        await ctx.SaveChangesAsync();

        var service = BuildService(ctx);
        await Assert.ThrowsAsync<ValidationAppException>(() => service.SubmitInvoiceAsync(invoice.Id));
    }

    [Fact]
    public async Task SubmitInvoice_Zatca_UpdatesInvoiceStatusAndChainsHash()
    {
        var ctx = CreateContext();
        var settings = SaudiSettings();
        settings.EInvoicingProvider = EInvoicingProvider.Zatca;
        var customer = B2BCustomer();
        var invoice1 = NewInvoice(customer);
        ctx.CompanySettings.Add(settings);
        ctx.Customers.Add(customer);
        ctx.SalesInvoices.Add(invoice1);
        await ctx.SaveChangesAsync();

        var service = BuildService(ctx);
        var result1 = await service.SubmitInvoiceAsync(invoice1.Id);

        Assert.Equal(EInvoiceStatus.Accepted, result1.Status);
        Assert.NotNull(result1.ExternalUuid);

        var settingsAfterFirst = await ctx.CompanySettings.AsNoTracking().FirstAsync();
        Assert.Equal(1, settingsAfterFirst.EInvoicingSubmittedCount);
        Assert.False(string.IsNullOrEmpty(settingsAfterFirst.EInvoicingLastInvoiceHash));

        // Second invoice should chain onto the first one's hash and bump the counter again.
        var invoice2 = NewInvoice(customer);
        invoice2.InvoiceNumber = "SI-000002";
        ctx.SalesInvoices.Add(invoice2);
        await ctx.SaveChangesAsync();

        await service.SubmitInvoiceAsync(invoice2.Id);
        var settingsAfterSecond = await ctx.CompanySettings.AsNoTracking().FirstAsync();
        Assert.Equal(2, settingsAfterSecond.EInvoicingSubmittedCount);
        Assert.NotEqual(settingsAfterFirst.EInvoicingLastInvoiceHash, settingsAfterSecond.EInvoicingLastInvoiceHash);
    }

    [Fact]
    public async Task SubmitInvoice_Eta_UpdatesInvoiceStatus()
    {
        var ctx = CreateContext();
        var settings = SaudiSettings();
        settings.Country = Country.Egypt;
        settings.EInvoicingProvider = EInvoicingProvider.Eta;
        var customer = B2CCustomer();
        var invoice = NewInvoice(customer);
        ctx.CompanySettings.Add(settings);
        ctx.Customers.Add(customer);
        ctx.SalesInvoices.Add(invoice);
        await ctx.SaveChangesAsync();

        var service = BuildService(ctx);
        var result = await service.SubmitInvoiceAsync(invoice.Id);

        Assert.Equal(EInvoiceStatus.Accepted, result.Status);
        var refreshedInvoice = await ctx.SalesInvoices.AsNoTracking().FirstAsync(i => i.Id == invoice.Id);
        Assert.Equal(EInvoiceStatus.Accepted, refreshedInvoice.EInvoiceStatus);
        Assert.NotNull(refreshedInvoice.EInvoiceSubmittedAtUtc);
    }

    [Fact]
    public async Task UpdateSettings_EncryptsSecretsAndReportsHasCredentials()
    {
        var ctx = CreateContext();
        var settings = SaudiSettings();
        ctx.CompanySettings.Add(settings);
        await ctx.SaveChangesAsync();

        var service = BuildService(ctx);
        var updated = await service.UpdateSettingsAsync(new UpdateEInvoicingSettingsDto
        {
            Provider = EInvoicingProvider.Zatca,
            Environment = EInvoicingEnvironment.Sandbox,
            ClientId = "client-123",
            ClientSecret = "super-secret"
        });

        Assert.True(updated.HasClientCredentials);
        var stored = await ctx.CompanySettings.AsNoTracking().FirstAsync();
        Assert.NotEqual("super-secret", stored.EInvoicingClientSecretEncrypted);
        Assert.StartsWith("protected:", stored.EInvoicingClientSecretEncrypted);
    }
}
