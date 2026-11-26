using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting.Services;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.Sales.DTOs;
using RomaERP.Domain.Sales;

namespace RomaERP.Application.Sales.Services;

public interface ICustomerPortalService
{
    Task<PortalLinkStatusDto> GetStatusAsync(Guid customerId, CancellationToken ct = default);
    Task<CreatedPortalLinkDto> CreateLinkAsync(Guid customerId, int? days, CancellationToken ct = default);
    Task RevokeAsync(Guid customerId, CancellationToken ct = default);
    Task<CustomerPortalDto> GetPortalAsync(string token, CancellationToken ct = default);
    /// <summary>The PDF of one of the link owner's own invoices. Any other invoice is refused.</summary>
    Task<byte[]> GetInvoicePdfAsync(string token, Guid invoiceId, CancellationToken ct = default);
}

/// <summary>The customer's private link. The link opens only that customer's invoices and statement; an invalid, expired or switched-off link all look the same from outside.</summary>
public class CustomerPortalService : ICustomerPortalService
{
    public const int DefaultDays = 180;
    public const int MaxDays = 365;
    private const string Refused = "الرابط غير صالح أو انتهى. اطلب رابط جديد من الشركة.";

    private readonly IApplicationDbContext _c;
    private readonly ILedgerReportService _ledger;
    private readonly ISalesService _sales;
    public CustomerPortalService(IApplicationDbContext c, ILedgerReportService ledger, ISalesService sales) { _c = c; _ledger = ledger; _sales = sales; }

    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    public async Task<PortalLinkStatusDto> GetStatusAsync(Guid customerId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var link = await _c.CustomerPortalLinks.AsNoTracking().Where(l => l.CustomerId == customerId && !l.IsDeleted && l.RevokedAtUtc == null && l.ExpiresAtUtc > now).OrderByDescending(l => l.CreatedAtUtc).FirstOrDefaultAsync(ct);
        return link is null ? new PortalLinkStatusDto() : new PortalLinkStatusDto { HasActiveLink = true, ExpiresAtUtc = link.ExpiresAtUtc, LastViewedAtUtc = link.LastViewedAtUtc, ViewCount = link.ViewCount };
    }

    public async Task<CreatedPortalLinkDto> CreateLinkAsync(Guid customerId, int? days, CancellationToken ct = default)
    {
        if (!await _c.Customers.AnyAsync(x => x.Id == customerId && !x.IsDeleted, ct)) throw new NotFoundException(nameof(Customer), customerId);
        var d = days ?? DefaultDays;
        if (d is < 1 or > MaxDays) throw new ValidationAppException($"مدة الرابط لازم تكون من 1 لـ {MaxDays} يوم.");
        var now = DateTime.UtcNow;
        foreach (var old in await _c.CustomerPortalLinks.Where(l => l.CustomerId == customerId && l.RevokedAtUtc == null).ToListAsync(ct)) old.RevokedAtUtc = now;
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var link = new CustomerPortalLink { CustomerId = customerId, TokenHash = Hash(token), ExpiresAtUtc = now.AddDays(d) };
        _c.CustomerPortalLinks.Add(link);
        await _c.SaveChangesAsync(ct);
        return new CreatedPortalLinkDto { Token = token, ExpiresAtUtc = link.ExpiresAtUtc };
    }

    public async Task RevokeAsync(Guid customerId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        foreach (var l in await _c.CustomerPortalLinks.Where(x => x.CustomerId == customerId && x.RevokedAtUtc == null).ToListAsync(ct)) l.RevokedAtUtc = now;
        await _c.SaveChangesAsync(ct);
    }

    private async Task<CustomerPortalLink> ResolveAsync(string token, bool count, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length is < 20 or > 100) throw new ValidationAppException(Refused);
        var hash = Hash(token.Trim());
        var now = DateTime.UtcNow;
        var link = await _c.CustomerPortalLinks.Include(l => l.Customer).FirstOrDefaultAsync(l => l.TokenHash == hash && !l.IsDeleted && l.RevokedAtUtc == null && l.ExpiresAtUtc > now, ct);
        if (link is null || link.Customer is null || link.Customer.IsDeleted) throw new ValidationAppException(Refused);
        if (count) { link.LastViewedAtUtc = now; link.ViewCount++; await _c.SaveChangesAsync(ct); }
        return link;
    }

    public async Task<CustomerPortalDto> GetPortalAsync(string token, CancellationToken ct = default)
    {
        var link = await ResolveAsync(token, true, ct);
        var customer = link.Customer!;
        var settings = await _c.CompanySettings.AsNoTracking().FirstOrDefaultAsync(ct);
        var invoices = await _c.SalesInvoices.AsNoTracking().Where(i => i.CustomerId == customer.Id && !i.IsDeleted).OrderByDescending(i => i.InvoiceDate).ThenByDescending(i => i.InvoiceNumber).Take(200).ToListAsync(ct);
        var to = DateTime.UtcNow.Date; var from = to.AddMonths(-12);
        var statement = await _ledger.GetCustomerStatementAsync(customer.Id, from, to, ct);
        return new CustomerPortalDto
        {
            CompanyName = string.IsNullOrWhiteSpace(settings?.CompanyNameAr) ? settings?.CompanyNameEn ?? string.Empty : settings!.CompanyNameAr,
            CustomerName = customer.NameAr, Balance = statement.ClosingBalance, Currency = settings?.DefaultCurrency ?? "EGP", Statement = statement,
            Invoices = invoices.Select(i => new PortalInvoiceDto { Id = i.Id, InvoiceNumber = i.InvoiceNumber, InvoiceDate = i.InvoiceDate, TotalAmount = i.TotalAmount, PaidAmount = i.PaidAmount, OutstandingAmount = i.TotalAmount - i.PaidAmount, CurrencyCode = i.CurrencyCode }).ToList(),
        };
    }

    public async Task<byte[]> GetInvoicePdfAsync(string token, Guid invoiceId, CancellationToken ct = default)
    {
        var link = await ResolveAsync(token, false, ct);
        var mine = await _c.SalesInvoices.AsNoTracking().AnyAsync(i => i.Id == invoiceId && i.CustomerId == link.CustomerId && !i.IsDeleted, ct);
        if (!mine) throw new ValidationAppException(Refused);
        return await _sales.GetInvoicePdfAsync(invoiceId, ct);
    }
}
