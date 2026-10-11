using System.Text;
using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Assistant.Services;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Domain.Accounting;
using RomaERP.Domain.Purchasing;

namespace RomaERP.Application.Purchasing.Services;

public class InvoiceScanLineDto
{
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
}

/// <summary>What was read off a supplier invoice photo, already matched to the company's own vendor list and
/// chart of accounts. Nothing here is saved — the purchase invoice form is pre-filled and the user reviews and saves it.</summary>
public class InvoiceScanResultDto
{
    public string? VendorName { get; set; }
    public string? VendorTaxNumber { get; set; }
    public Guid? MatchedVendorId { get; set; }
    public string? MatchedVendorName { get; set; }
    public string? InvoiceNumber { get; set; }
    public DateTime? InvoiceDate { get; set; }
    public string? Currency { get; set; }
    public decimal? SubTotal { get; set; }
    public decimal? VatAmount { get; set; }
    public decimal? VatRatePercent { get; set; }
    public decimal? Total { get; set; }
    public Guid? SuggestedAccountId { get; set; }
    public string? SuggestedAccountCode { get; set; }
    public string? SuggestedAccountName { get; set; }
    public List<InvoiceScanLineDto> Lines { get; set; } = new();
    public bool NeedsClarification { get; set; }
    public string? Message { get; set; }
}

public interface IInvoiceScanService
{
    Task<InvoiceScanResultDto> ScanAsync(byte[] imageBytes, string mediaType, CancellationToken ct = default);
}

public class InvoiceScanService : IInvoiceScanService
{
    private readonly IApplicationDbContext _context;
    private readonly IClaudeExpenseParser _parser;

    public InvoiceScanService(IApplicationDbContext context, IClaudeExpenseParser parser)
    {
        _context = context;
        _parser = parser;
    }

    public async Task<InvoiceScanResultDto> ScanAsync(byte[] imageBytes, string mediaType, CancellationToken ct = default)
    {
        var accounts = await _context.Accounts
            .AsNoTracking()
            .Where(a => a.AccountType == AccountType.Expense && !a.IsControlAccount && a.IsActive && !a.IsDeleted)
            .ToListAsync(ct);
        var candidates = accounts.Select(a => new ExpenseAccountCandidate(a.Code, a.NameAr)).ToList();

        var extraction = await _parser.ExtractInvoiceFromImageAsync(imageBytes, mediaType, candidates, ct);

        var vendors = await _context.Vendors.AsNoTracking().Where(v => !v.IsDeleted && v.IsActive).ToListAsync(ct);
        var vendor = VendorMatcher.Match(vendors, extraction.VendorName, extraction.VendorTaxNumber);
        var account = extraction.SuggestedAccountCode is null ? null : accounts.FirstOrDefault(a => a.Code == extraction.SuggestedAccountCode);

        // Net amount is what the form needs; derive it when only total and VAT were legible.
        var subTotal = extraction.SubTotal;
        if (subTotal is null && extraction.Total is { } total && extraction.VatAmount is { } vat)
            subTotal = total - vat;

        var lines = extraction.Lines
            .Select(l => new InvoiceScanLineDto
            {
                Description = l.Description,
                Quantity = l.Quantity is > 0 ? l.Quantity.Value : 1,
                UnitPrice = l.UnitPrice ?? 0
            })
            .ToList();

        // No usable lines: fall back to a single line carrying the net amount, so the form still adds up.
        if ((lines.Count == 0 || lines.All(l => l.UnitPrice == 0)) && subTotal is > 0)
        {
            lines = new List<InvoiceScanLineDto>
            {
                new() { Description = extraction.VendorName ?? "فاتورة مشتريات", Quantity = 1, UnitPrice = subTotal.Value }
            };
        }

        return new InvoiceScanResultDto
        {
            VendorName = extraction.VendorName,
            VendorTaxNumber = extraction.VendorTaxNumber,
            MatchedVendorId = vendor?.Id,
            MatchedVendorName = vendor?.NameAr,
            InvoiceNumber = extraction.InvoiceNumber,
            InvoiceDate = extraction.InvoiceDate,
            Currency = extraction.Currency,
            SubTotal = subTotal,
            VatAmount = extraction.VatAmount,
            VatRatePercent = extraction.VatRatePercent,
            Total = extraction.Total,
            SuggestedAccountId = account?.Id,
            SuggestedAccountCode = account?.Code,
            SuggestedAccountName = account?.NameAr,
            Lines = lines,
            NeedsClarification = extraction.NeedsClarification,
            Message = extraction.ClarifyingQuestion
        };
    }
}

/// <summary>Matches a scanned vendor name / tax number to an existing Vendor: tax number first (exact on digits),
/// then name (normalized exact, then containment either way). Returns null when nothing matches confidently.</summary>
public static class VendorMatcher
{
    public static Vendor? Match(IReadOnlyList<Vendor> vendors, string? scannedName, string? scannedTaxNumber)
    {
        var scannedDigits = DigitsOnly(scannedTaxNumber);
        if (scannedDigits.Length >= 6)
        {
            var byTax = vendors.FirstOrDefault(v => DigitsOnly(v.TaxRegistrationNumber) == scannedDigits);
            if (byTax is not null) return byTax;
        }

        var name = Normalize(scannedName);
        if (name.Length < 3) return null;

        var exact = vendors.FirstOrDefault(v => Normalize(v.NameAr) == name || Normalize(v.NameEn) == name);
        if (exact is not null) return exact;

        var contained = vendors
            .Where(v => Contains(name, Normalize(v.NameAr)) || Contains(name, Normalize(v.NameEn)))
            .ToList();
        return contained.Count == 1 ? contained[0] : null;
    }

    private static bool Contains(string scanned, string vendor)
        => vendor.Length >= 3 && (scanned.Contains(vendor, StringComparison.Ordinal) || vendor.Contains(scanned, StringComparison.Ordinal));

    private static string DigitsOnly(string? value)
        => value is null ? string.Empty : new string(value.Where(char.IsDigit).ToArray());

    /// <summary>Lower-case, letters/digits only, with Arabic letter variants folded (أإآ→ا، ة→ه، ى→ي) and diacritics dropped.</summary>
    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var sb = new StringBuilder();
        foreach (var ch in value.ToLowerInvariant())
        {
            var c = ch switch { 'أ' or 'إ' or 'آ' => 'ا', 'ة' => 'ه', 'ى' => 'ي', _ => ch };
            if (char.IsLetterOrDigit(c)) sb.Append(c);
        }
        return sb.ToString();
    }
}
