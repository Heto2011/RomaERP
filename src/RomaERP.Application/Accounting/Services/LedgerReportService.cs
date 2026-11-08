using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting.DTOs;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Domain.Accounting;
using RomaERP.Domain.Common;
using RomaERP.Domain.Purchasing;
using RomaERP.Domain.Sales;

namespace RomaERP.Application.Accounting.Services;

public class LedgerReportService : ILedgerReportService
{
    private readonly IApplicationDbContext _context;

    public LedgerReportService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<GeneralLedgerDto> GetGeneralLedgerAsync(Guid accountId, DateTime fromDate, DateTime toDate, CancellationToken ct = default)
    {
        EnsureRange(fromDate, toDate);
        var account = await _context.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == accountId, ct)
            ?? throw new NotFoundException(nameof(Account), accountId);

        var from = fromDate.Date;
        var to = toDate.Date;

        // Only posted entries count, exactly as on the trial balance, so the two always agree.
        var movements = await _context.JournalEntryLines
            .AsNoTracking()
            .Where(l => l.AccountId == accountId && l.JournalEntry!.Status == JournalEntryStatus.Posted && !l.JournalEntry.IsDeleted
                        && l.JournalEntry.EntryDate <= to)
            .Select(l => new
            {
                l.JournalEntry!.EntryDate,
                l.JournalEntry.EntryNumber,
                l.JournalEntry.Reference,
                EntryDescription = l.JournalEntry.Description,
                LineDescription = l.Description,
                l.LineNumber,
                l.Debit,
                l.Credit
            })
            .ToListAsync(ct);

        decimal Signed(decimal debit, decimal credit) => account.Nature == AccountNature.Debit ? debit - credit : credit - debit;

        var opening = movements.Where(m => m.EntryDate.Date < from).Sum(m => Signed(m.Debit, m.Credit));

        var result = new GeneralLedgerDto
        {
            AccountId = account.Id,
            AccountCode = account.Code,
            AccountName = account.NameAr,
            Nature = account.Nature,
            FromDate = from,
            ToDate = to,
            OpeningBalance = opening
        };

        var running = opening;
        foreach (var m in movements.Where(m => m.EntryDate.Date >= from)
                     .OrderBy(m => m.EntryDate).ThenBy(m => m.EntryNumber).ThenBy(m => m.LineNumber))
        {
            running += Signed(m.Debit, m.Credit);
            result.Lines.Add(new GeneralLedgerLineDto
            {
                EntryDate = m.EntryDate,
                EntryNumber = m.EntryNumber,
                Description = string.IsNullOrWhiteSpace(m.LineDescription) ? m.EntryDescription : m.LineDescription,
                Reference = m.Reference,
                Debit = m.Debit,
                Credit = m.Credit,
                Balance = running
            });
        }

        result.TotalDebit = result.Lines.Sum(l => l.Debit);
        result.TotalCredit = result.Lines.Sum(l => l.Credit);
        result.ClosingBalance = running;
        return result;
    }

    public async Task<PartnerStatementDto> GetCustomerStatementAsync(Guid customerId, DateTime fromDate, DateTime toDate, CancellationToken ct = default)
    {
        EnsureRange(fromDate, toDate);
        var customer = await _context.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == customerId, ct)
            ?? throw new NotFoundException(nameof(Customer), customerId);

        var events = new List<(DateTime Date, PartnerStatementLineType Type, string Number, string? Description, decimal Debit, decimal Credit)>();

        // Only credit/installment invoices go through Accounts Receivable; cash and card sales settle on the spot.
        var invoices = await _context.SalesInvoices.AsNoTracking()
            .Where(i => i.CustomerId == customerId && (i.PaymentTerm == PaymentTerm.Credit || i.PaymentTerm == PaymentTerm.Installment))
            .Select(i => new { i.InvoiceNumber, i.InvoiceDate, i.TotalAmount, i.ExchangeRateToFunctional, i.CurrencyCode })
            .ToListAsync(ct);
        foreach (var i in invoices)
            events.Add((i.InvoiceDate, PartnerStatementLineType.Invoice, i.InvoiceNumber, null, Math.Round(i.TotalAmount * i.ExchangeRateToFunctional, 2), 0));

        var payments = await _context.SalesPayments.AsNoTracking()
            .Where(p => p.SalesInvoice!.CustomerId == customerId)
            .Select(p => new { p.PaymentDate, p.Amount, p.Reference, p.SalesInvoice!.InvoiceNumber, p.SalesInvoice.ExchangeRateToFunctional })
            .ToListAsync(ct);
        foreach (var p in payments)
            events.Add((p.PaymentDate, PartnerStatementLineType.Payment, p.InvoiceNumber, p.Reference, 0, Math.Round(p.Amount * p.ExchangeRateToFunctional, 2)));

        var notes = await _context.SalesNotes.AsNoTracking()
            .Where(n => n.CustomerId == customerId)
            .Select(n => new { n.NoteNumber, n.NoteDate, n.NoteType, n.TotalAmount, n.Reason })
            .ToListAsync(ct);
        foreach (var n in notes)
        {
            var isCredit = n.NoteType == SalesNoteType.Credit;
            events.Add((n.NoteDate, isCredit ? PartnerStatementLineType.CreditNote : PartnerStatementLineType.DebitNote, n.NoteNumber, n.Reason,
                isCredit ? 0 : n.TotalAmount, isCredit ? n.TotalAmount : 0));
        }

        return BuildStatement(customer.Id, customer.Code, customer.NameAr, isCustomer: true, fromDate, toDate, events);
    }

    public async Task<PartnerStatementDto> GetVendorStatementAsync(Guid vendorId, DateTime fromDate, DateTime toDate, CancellationToken ct = default)
    {
        EnsureRange(fromDate, toDate);
        var vendor = await _context.Vendors.AsNoTracking().FirstOrDefaultAsync(v => v.Id == vendorId, ct)
            ?? throw new NotFoundException(nameof(Vendor), vendorId);

        var events = new List<(DateTime Date, PartnerStatementLineType Type, string Number, string? Description, decimal Debit, decimal Credit)>();

        // Only credit purchase invoices go through Accounts Payable; cash purchases are paid on the spot.
        var invoices = await _context.PurchaseInvoices.AsNoTracking()
            .Where(i => i.VendorId == vendorId && i.PaymentTerm == PaymentTerm.Credit)
            .Select(i => new { i.InvoiceNumber, i.InvoiceDate, i.TotalAmount, i.ExchangeRateToFunctional })
            .ToListAsync(ct);
        foreach (var i in invoices)
            events.Add((i.InvoiceDate, PartnerStatementLineType.Invoice, i.InvoiceNumber, null, 0, Math.Round(i.TotalAmount * i.ExchangeRateToFunctional, 2)));

        var payments = await _context.PurchasePayments.AsNoTracking()
            .Where(p => p.PurchaseInvoice!.VendorId == vendorId)
            .Select(p => new { p.PaymentDate, p.Amount, p.Reference, p.PurchaseInvoice!.InvoiceNumber, p.PurchaseInvoice.ExchangeRateToFunctional })
            .ToListAsync(ct);
        foreach (var p in payments)
            events.Add((p.PaymentDate, PartnerStatementLineType.Payment, p.InvoiceNumber, p.Reference, Math.Round(p.Amount * p.ExchangeRateToFunctional, 2), 0));

        return BuildStatement(vendor.Id, vendor.Code, vendor.NameAr, isCustomer: false, fromDate, toDate, events);
    }

    /// <summary>Customer: debit raises what they owe, credit lowers it. Vendor: credit raises what we owe, debit lowers it.</summary>
    private static PartnerStatementDto BuildStatement(Guid id, string code, string name, bool isCustomer, DateTime fromDate, DateTime toDate,
        List<(DateTime Date, PartnerStatementLineType Type, string Number, string? Description, decimal Debit, decimal Credit)> events)
    {
        var from = fromDate.Date;
        var to = toDate.Date;
        decimal Owed(decimal debit, decimal credit) => isCustomer ? debit - credit : credit - debit;

        var opening = events.Where(e => e.Date.Date < from).Sum(e => Owed(e.Debit, e.Credit));
        var result = new PartnerStatementDto
        {
            PartnerId = id,
            PartnerCode = code,
            PartnerName = name,
            IsCustomer = isCustomer,
            FromDate = from,
            ToDate = to,
            OpeningBalance = opening
        };

        var running = opening;
        foreach (var e in events.Where(e => e.Date.Date >= from && e.Date.Date <= to)
                     .OrderBy(e => e.Date).ThenBy(e => e.Type).ThenBy(e => e.Number))
        {
            running += Owed(e.Debit, e.Credit);
            result.Lines.Add(new PartnerStatementLineDto
            {
                Date = e.Date,
                Type = e.Type,
                Number = e.Number,
                Description = e.Description,
                Debit = e.Debit,
                Credit = e.Credit,
                Balance = running
            });
        }

        result.TotalDebit = result.Lines.Sum(l => l.Debit);
        result.TotalCredit = result.Lines.Sum(l => l.Credit);
        result.ClosingBalance = running;
        return result;
    }

    private static void EnsureRange(DateTime fromDate, DateTime toDate)
    {
        if (toDate.Date < fromDate.Date)
            throw new ValidationAppException("تاريخ النهاية لازم يكون بعد تاريخ البداية.");
    }
}
