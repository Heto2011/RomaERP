using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.Purchasing.DTOs;
using RomaERP.Domain.Accounting;
using RomaERP.Domain.Purchasing;

namespace RomaERP.Application.Purchasing.Services;

public interface IPurchaseNoteService
{
    Task<List<PurchaseNoteDto>> GetAllAsync(CancellationToken ct = default);
    Task<PurchaseNoteDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<PurchaseNoteDto> CreateAsync(CreatePurchaseNoteDto dto, CancellationToken ct = default);
}

/// <summary>Returns to a supplier and extra supplier charges, booked against the supplier invoice. A return debits the supplier account and credits the accounts the purchase was coded to (and the input tax); an extra charge does the opposite. Stock that physically goes back is a separate stock movement.</summary>
public class PurchaseNoteService : IPurchaseNoteService
{
    private readonly IApplicationDbContext _c;
    public PurchaseNoteService(IApplicationDbContext c) { _c = c; }

    public async Task<List<PurchaseNoteDto>> GetAllAsync(CancellationToken ct = default)
        => (await _c.PurchaseNotes.AsNoTracking().Include(n => n.Vendor).Include(n => n.OriginalInvoice).Include(n => n.Lines).Where(n => !n.IsDeleted)
            .OrderByDescending(n => n.NoteDate).ThenByDescending(n => n.NoteNumber).Take(500).ToListAsync(ct)).Select(Map).ToList();

    public async Task<PurchaseNoteDto> GetAsync(Guid id, CancellationToken ct = default)
        => Map(await _c.PurchaseNotes.AsNoTracking().Include(n => n.Vendor).Include(n => n.OriginalInvoice).Include(n => n.Lines).FirstOrDefaultAsync(n => n.Id == id && !n.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(PurchaseNote), id));

    public async Task<PurchaseNoteDto> CreateAsync(CreatePurchaseNoteDto dto, CancellationToken ct = default)
    {
        if (dto.Lines.Count == 0) throw new ValidationAppException("لازم يكون في بند واحد على الأقل في الإشعار.");
        if (dto.Lines.Any(l => string.IsNullOrWhiteSpace(l.Description))) throw new ValidationAppException("اكتب وصف لكل بند.");
        if (dto.Lines.Any(l => l.Quantity <= 0)) throw new ValidationAppException("الكمية لازم تكون أكبر من صفر.");
        if (dto.Lines.Any(l => l.UnitPrice < 0)) throw new ValidationAppException("السعر مينفعش يكون بالسالب.");
        if (dto.Lines.Any(l => l.VatRate is < 0 or > 1)) throw new ValidationAppException("نسبة الضريبة لازم تكون بين 0 و100%.");
        if (string.IsNullOrWhiteSpace(dto.Reason)) throw new ValidationAppException("سبب الإشعار مطلوب.");
        if (!Enum.IsDefined(dto.NoteType)) throw new ValidationAppException("نوع الإشعار غير صحيح.");

        var invoice = await _c.PurchaseInvoices.Include(i => i.Vendor).FirstOrDefaultAsync(i => i.Id == dto.OriginalInvoiceId && !i.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(PurchaseInvoice), dto.OriginalInvoiceId);
        var vendor = invoice.Vendor!;
        var settings = await _c.CompanySettings.AsNoTracking().FirstOrDefaultAsync(ct);
        if (!string.IsNullOrWhiteSpace(invoice.CurrencyCode) && invoice.CurrencyCode != (settings?.DefaultCurrency ?? "EGP"))
            throw new ValidationAppException("الإشعار على فاتورة بعملة أجنبية لسه مش مدعوم.");

        var period = await _c.FiscalPeriods.FirstOrDefaultAsync(p => p.Id == dto.FiscalPeriodId, ct) ?? throw new NotFoundException(nameof(FiscalPeriod), dto.FiscalPeriodId);
        if (period.IsClosed) throw new ValidationAppException("لا يمكن تسجيل إشعار لفترة محاسبية مقفلة.");

        var accountIds = dto.Lines.Select(l => l.AccountId).Distinct().ToList();
        var accounts = await _c.Accounts.Where(a => accountIds.Contains(a.Id) && !a.IsDeleted).ToListAsync(ct);
        if (accounts.Count != accountIds.Count) throw new ValidationAppException("في بند حسابه مش موجود. اختار حساب لكل بند.");
        if (accounts.Any(a => !a.IsActive)) throw new ValidationAppException("في بند حسابه موقوف.");

        var vat = settings?.VatRate ?? 0;
        var n = 0;
        var lines = dto.Lines.Select(l =>
        {
            var total = Math.Round(l.Quantity * l.UnitPrice, 2); var rate = l.VatRate ?? vat;
            return new PurchaseNoteLine { LineNumber = ++n, Description = l.Description.Trim(), Quantity = l.Quantity, UnitPrice = l.UnitPrice, LineTotal = total, VatRate = rate, VatAmount = Math.Round(total * rate, 2), AccountId = l.AccountId };
        }).ToList();
        var subTotal = lines.Sum(l => l.LineTotal);
        var vatAmount = lines.GroupBy(l => l.VatRate).Sum(g => Math.Round(g.Sum(l => l.LineTotal) * g.Key, 2));
        var total2 = subTotal + vatAmount;

        var isReturn = dto.NoteType == PurchaseNoteType.Return;
        var apAccount = await _c.Accounts.FirstOrDefaultAsync(a => a.Code == AccountingConstants.AccountsPayableAccountCode && !a.IsDeleted, ct)
            ?? throw new ValidationAppException($"حساب الموردون ({AccountingConstants.AccountsPayableAccountCode}) غير موجود في دليل الحسابات.");
        Account? vatAccount = null;
        if (vatAmount > 0)
            vatAccount = await _c.Accounts.FirstOrDefaultAsync(a => a.Code == AccountingConstants.InputVatAccountCode && !a.IsDeleted, ct)
                ?? throw new ValidationAppException($"حساب ضريبة المدخلات ({AccountingConstants.InputVatAccountCode}) غير موجود في دليل الحسابات.");

        var noteNumber = $"{(isReturn ? "PR" : "PC")}-{(await _c.PurchaseNotes.CountAsync(x => x.NoteType == dto.NoteType, ct) + 1):D6}";
        var jl = new List<JournalEntryLine>(); var ln = 0;
        if (isReturn)
        {
            jl.Add(new JournalEntryLine { LineNumber = ++ln, AccountId = apAccount.Id, Debit = total2, Credit = 0, Description = $"مرتجع مشتريات - {vendor.NameAr}" });
            foreach (var l in lines) jl.Add(new JournalEntryLine { LineNumber = ++ln, AccountId = l.AccountId, Debit = 0, Credit = l.LineTotal, Description = l.Description });
            if (vatAccount is not null) jl.Add(new JournalEntryLine { LineNumber = ++ln, AccountId = vatAccount.Id, Debit = 0, Credit = vatAmount, Description = "عكس ضريبة مدخلات" });
            vendor.ApBalance -= total2;
        }
        else
        {
            foreach (var l in lines) jl.Add(new JournalEntryLine { LineNumber = ++ln, AccountId = l.AccountId, Debit = l.LineTotal, Credit = 0, Description = l.Description });
            if (vatAccount is not null) jl.Add(new JournalEntryLine { LineNumber = ++ln, AccountId = vatAccount.Id, Debit = vatAmount, Credit = 0, Description = "ضريبة مدخلات إضافية" });
            jl.Add(new JournalEntryLine { LineNumber = ++ln, AccountId = apAccount.Id, Debit = 0, Credit = total2, Description = $"رسوم إضافية من المورد - {vendor.NameAr}" });
            vendor.ApBalance += total2;
        }

        var entry = new JournalEntry
        {
            EntryNumber = $"JV-{(await _c.JournalEntries.CountAsync(ct) + 1):D6}", EntryDate = dto.NoteDate, FiscalPeriodId = dto.FiscalPeriodId,
            Description = $"{(isReturn ? "مرتجع مشتريات" : "رسوم إضافية من مورد")} {noteNumber} - {vendor.NameAr}",
            Reference = isReturn ? AccountingConstants.PurchaseReturnNoteReference : AccountingConstants.PurchaseChargeNoteReference,
            Status = JournalEntryStatus.Posted, Lines = jl,
        };
        _c.JournalEntries.Add(entry);
        var note = new PurchaseNote
        {
            NoteNumber = noteNumber, NoteType = dto.NoteType, NoteDate = dto.NoteDate.Date, OriginalInvoiceId = invoice.Id, VendorId = vendor.Id, FiscalPeriodId = dto.FiscalPeriodId,
            Reason = dto.Reason.Trim(), Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(), SubTotal = subTotal, VatAmount = vatAmount, TotalAmount = total2, JournalEntry = entry, Lines = lines,
        };
        _c.PurchaseNotes.Add(note);
        await _c.SaveChangesAsync(ct);
        return await GetAsync(note.Id, ct);
    }

    private static PurchaseNoteDto Map(PurchaseNote n) => new()
    {
        Id = n.Id, NoteNumber = n.NoteNumber, NoteType = n.NoteType, NoteDate = n.NoteDate, OriginalInvoiceId = n.OriginalInvoiceId, OriginalInvoiceNumber = n.OriginalInvoice?.InvoiceNumber ?? string.Empty,
        VendorId = n.VendorId, VendorName = n.Vendor?.NameAr ?? string.Empty, Reason = n.Reason, Notes = n.Notes, SubTotal = n.SubTotal, VatAmount = n.VatAmount, TotalAmount = n.TotalAmount, JournalEntryId = n.JournalEntryId,
        Lines = n.Lines.OrderBy(l => l.LineNumber).Select(l => new PurchaseNoteLineDto { LineNumber = l.LineNumber, Description = l.Description, Quantity = l.Quantity, UnitPrice = l.UnitPrice, LineTotal = l.LineTotal, VatRate = l.VatRate, VatAmount = l.VatAmount, AccountId = l.AccountId }).ToList(),
    };
}
