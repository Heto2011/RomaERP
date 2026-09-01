using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.Purchasing.DTOs;
using RomaERP.Domain.Accounting;
using RomaERP.Domain.Common;
using RomaERP.Domain.Inventory;
using RomaERP.Domain.Purchasing;
using RomaERP.Domain.Tenancy;

namespace RomaERP.Application.Purchasing.Services;

public class PurchasingService : IPurchasingService
{
    private readonly IApplicationDbContext _context;
    private readonly IHtmlToPdfRenderer _pdfRenderer;

    public PurchasingService(IApplicationDbContext context, IHtmlToPdfRenderer pdfRenderer)
    {
        _context = context;
        _pdfRenderer = pdfRenderer;
    }

    public async Task<List<VendorDto>> GetVendorsAsync(CancellationToken ct = default)
    {
        var vendors = await _context.Vendors.AsNoTracking().OrderBy(v => v.Code).ToListAsync(ct);
        return vendors.Select(Map).ToList();
    }

    public async Task<VendorDto> CreateVendorAsync(CreateVendorDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Code) || string.IsNullOrWhiteSpace(dto.NameAr) || string.IsNullOrWhiteSpace(dto.NameEn))
            throw new ValidationAppException("الكود والاسم بالعربي والإنجليزي مطلوبين.");

        if (await _context.Vendors.AnyAsync(v => v.Code == dto.Code, ct))
            throw new ValidationAppException("كود المورد ده مستخدم قبل كده.");

        var vendor = new Vendor
        {
            Code = dto.Code,
            NameAr = dto.NameAr,
            NameEn = dto.NameEn,
            Phone = dto.Phone,
            Email = dto.Email,
            TaxRegistrationNumber = dto.TaxRegistrationNumber,
            IsActive = true
        };

        _context.Vendors.Add(vendor);
        await _context.SaveChangesAsync(ct);

        return Map(vendor);
    }

    public async Task<List<PurchaseInvoiceDto>> GetInvoicesAsync(CancellationToken ct = default)
    {
        var invoices = await _context.PurchaseInvoices
            .AsNoTracking()
            .Include(i => i.Vendor)
            .Include(i => i.Lines).ThenInclude(l => l.Account)
            .Include(i => i.Lines).ThenInclude(l => l.Item)
            .Include(i => i.Payments)
            .OrderByDescending(i => i.InvoiceDate)
            .ThenByDescending(i => i.InvoiceNumber)
            .ToListAsync(ct);

        return invoices.Select(Map).ToList();
    }

    public async Task<PurchaseInvoiceDto> GetInvoiceAsync(Guid id, CancellationToken ct = default)
    {
        var invoice = await LoadInvoiceAsync(id, ct);
        return Map(invoice);
    }

    public async Task<PurchaseInvoiceDto> CreateInvoiceAsync(CreatePurchaseInvoiceDto dto, CancellationToken ct = default)
    {
        if (dto.Lines.Count == 0)
            throw new ValidationAppException("لازم يكون في بند واحد على الأقل في الفاتورة.");

        foreach (var line in dto.Lines)
        {
            if (line.Quantity <= 0)
                throw new ValidationAppException("الكمية يجب أن تكون أكبر من صفر.");
            if (line.UnitPrice < 0)
                throw new ValidationAppException("سعر الوحدة لا يمكن أن يكون سالبًا.");
        }

        var vendor = await _context.Vendors.FirstOrDefaultAsync(v => v.Id == dto.VendorId && !v.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Vendor), dto.VendorId);

        var period = await _context.FiscalPeriods.FirstOrDefaultAsync(p => p.Id == dto.FiscalPeriodId, ct)
            ?? throw new NotFoundException(nameof(FiscalPeriod), dto.FiscalPeriodId);
        if (period.IsClosed)
            throw new ValidationAppException("لا يمكن تسجيل فاتورة لفترة محاسبية مقفلة.");

        var accountIds = dto.Lines.Select(l => l.AccountId).Distinct().ToList();
        var accounts = await _context.Accounts.Where(a => accountIds.Contains(a.Id) && !a.IsDeleted).ToListAsync(ct);
        foreach (var accountId in accountIds)
        {
            var account = accounts.FirstOrDefault(a => a.Id == accountId)
                ?? throw new NotFoundException(nameof(Account), accountId);
            if (account.IsControlAccount)
                throw new ValidationAppException($"لا يمكن الترحيل على حساب إجمالي ({account.Code}).");
        }

        var itemIds = dto.Lines.Where(l => l.ItemId.HasValue).Select(l => l.ItemId!.Value).Distinct().ToList();
        if (itemIds.Count > 0)
        {
            var existingItemIds = await _context.Items.Where(i => itemIds.Contains(i.Id) && !i.IsDeleted).Select(i => i.Id).ToListAsync(ct);
            var missingItemId = itemIds.FirstOrDefault(id => !existingItemIds.Contains(id));
            if (missingItemId != default)
                throw new NotFoundException(nameof(Item), missingItemId);
        }

        var vatRate = await GetVatRateAsync(ct);

        var lines = dto.Lines.Select((l, idx) => new PurchaseInvoiceLine
        {
            LineNumber = idx + 1,
            Description = l.Description,
            AccountId = l.AccountId,
            ItemId = l.ItemId,
            Quantity = l.Quantity,
            UnitPrice = l.UnitPrice,
            LineTotal = Math.Round(l.Quantity * l.UnitPrice, 2)
        }).ToList();

        var subTotal = lines.Sum(l => l.LineTotal);
        var vatAmount = Math.Round(subTotal * vatRate, 2);
        var totalAmount = subTotal + vatAmount;

        var journalLines = new List<JournalEntryLine>();
        var lineNumber = 1;
        foreach (var lineGroup in lines.GroupBy(l => l.AccountId))
        {
            journalLines.Add(new JournalEntryLine
            {
                LineNumber = lineNumber++,
                AccountId = lineGroup.Key,
                Debit = lineGroup.Sum(l => l.LineTotal),
                Credit = 0,
                Description = "بند فاتورة مشتريات"
            });
        }

        Account? inputVatAccount = null;
        if (vatAmount > 0)
        {
            inputVatAccount = await GetAccountAsync(AccountingConstants.InputVatAccountCode, "ضريبة القيمة المضافة (مدخلات)", ct);
            journalLines.Add(new JournalEntryLine { LineNumber = lineNumber++, AccountId = inputVatAccount.Id, Debit = vatAmount, Credit = 0, Description = "ضريبة مدخلات" });
        }

        decimal paidAmount;
        if (dto.PaymentTerm == PaymentTerm.Credit)
        {
            var apAccount = await GetAccountAsync(AccountingConstants.AccountsPayableAccountCode, "الموردون", ct);
            journalLines.Add(new JournalEntryLine { LineNumber = lineNumber++, AccountId = apAccount.Id, Debit = 0, Credit = totalAmount, Description = $"فاتورة مشتريات آجلة - {vendor.NameAr}" });
            vendor.ApBalance += totalAmount;
            paidAmount = 0;
        }
        else
        {
            var settlementAccount = await GetSettlementAccountAsync(dto.PaymentTerm, ct);
            journalLines.Add(new JournalEntryLine { LineNumber = lineNumber++, AccountId = settlementAccount.Id, Debit = 0, Credit = totalAmount, Description = $"فاتورة مشتريات - {vendor.NameAr}" });
            paidAmount = totalAmount;
        }

        var entry = new JournalEntry
        {
            EntryNumber = await GenerateEntryNumberAsync(ct),
            EntryDate = dto.InvoiceDate,
            FiscalPeriodId = dto.FiscalPeriodId,
            Description = $"فاتورة مشتريات - {vendor.NameAr}",
            Reference = AccountingConstants.PurchaseInvoiceReference,
            Status = JournalEntryStatus.Posted,
            Lines = journalLines
        };
        _context.JournalEntries.Add(entry);

        var invoice = new PurchaseInvoice
        {
            InvoiceNumber = await GenerateInvoiceNumberAsync(ct),
            InvoiceDate = dto.InvoiceDate,
            VendorId = vendor.Id,
            FiscalPeriodId = dto.FiscalPeriodId,
            SubTotal = subTotal,
            VatRate = vatRate,
            VatAmount = vatAmount,
            TotalAmount = totalAmount,
            PaymentTerm = dto.PaymentTerm,
            PaidAmount = paidAmount,
            Notes = dto.Notes,
            JournalEntry = entry,
            Lines = lines
        };

        _context.PurchaseInvoices.Add(invoice);
        await _context.SaveChangesAsync(ct);

        return await GetInvoiceAsync(invoice.Id, ct);
    }

    /// <summary>The item-based counterpart to CreateInvoiceAsync — every line updates the item's quantity
    /// and weighted-average cost (like a stock receipt), and the whole delivery still posts as one normal
    /// purchase invoice + journal entry (VAT-exclusive costs, VAT added automatically), so it shows up in
    /// AP aging and purchasing reports exactly like an invoice entered on the regular screen.</summary>
    public async Task<PurchaseInvoiceDto> ReceiveInventoryPurchaseAsync(ReceiveInventoryPurchaseDto dto, CancellationToken ct = default)
    {
        if (dto.Lines.Count == 0)
            throw new ValidationAppException("لازم يكون في صنف واحد على الأقل في الفاتورة.");

        foreach (var line in dto.Lines)
        {
            if (line.Quantity <= 0)
                throw new ValidationAppException("الكمية يجب أن تكون أكبر من صفر.");
            if (line.UnitCost < 0)
                throw new ValidationAppException("تكلفة الوحدة لا يمكن أن تكون سالبة.");
        }

        var vendor = await _context.Vendors.FirstOrDefaultAsync(v => v.Id == dto.VendorId && !v.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Vendor), dto.VendorId);

        var period = await _context.FiscalPeriods.FirstOrDefaultAsync(p => p.Id == dto.FiscalPeriodId, ct)
            ?? throw new NotFoundException(nameof(FiscalPeriod), dto.FiscalPeriodId);
        if (period.IsClosed)
            throw new ValidationAppException("لا يمكن تسجيل فاتورة لفترة محاسبية مقفلة.");

        var warehouseExists = await _context.Warehouses.AnyAsync(w => w.Id == dto.WarehouseId && !w.IsDeleted, ct);
        if (!warehouseExists)
            throw new NotFoundException(nameof(Warehouse), dto.WarehouseId);

        var itemIds = dto.Lines.Select(l => l.ItemId).Distinct().ToList();
        var items = await _context.Items.Where(i => itemIds.Contains(i.Id) && !i.IsDeleted).ToListAsync(ct);
        var missingItemId = itemIds.FirstOrDefault(id => items.All(i => i.Id != id));
        if (missingItemId != default)
            throw new NotFoundException(nameof(Item), missingItemId);

        var inventoryAccount = await GetAccountAsync(AccountingConstants.InventoryAccountCode, "المخزون", ct);
        var vatRate = await GetVatRateAsync(ct);

        var lines = dto.Lines.Select((l, idx) =>
        {
            var item = items.First(i => i.Id == l.ItemId);
            return new PurchaseInvoiceLine
            {
                LineNumber = idx + 1,
                Description = item.NameAr,
                AccountId = inventoryAccount.Id,
                ItemId = item.Id,
                Quantity = l.Quantity,
                UnitPrice = l.UnitCost,
                LineTotal = Math.Round(l.Quantity * l.UnitCost, 2)
            };
        }).ToList();

        var subTotal = lines.Sum(l => l.LineTotal);
        var vatAmount = Math.Round(subTotal * vatRate, 2);
        var totalAmount = subTotal + vatAmount;

        var journalLines = new List<JournalEntryLine>
        {
            new() { LineNumber = 1, AccountId = inventoryAccount.Id, Debit = subTotal, Credit = 0, Description = "استلام مخزون - فاتورة مشتريات" }
        };
        var lineNumber = 2;

        Account? inputVatAccount = null;
        if (vatAmount > 0)
        {
            inputVatAccount = await GetAccountAsync(AccountingConstants.InputVatAccountCode, "ضريبة القيمة المضافة (مدخلات)", ct);
            journalLines.Add(new JournalEntryLine { LineNumber = lineNumber++, AccountId = inputVatAccount.Id, Debit = vatAmount, Credit = 0, Description = "ضريبة مدخلات" });
        }

        decimal paidAmount;
        if (dto.PaymentTerm == PaymentTerm.Credit)
        {
            var apAccount = await GetAccountAsync(AccountingConstants.AccountsPayableAccountCode, "الموردون", ct);
            journalLines.Add(new JournalEntryLine { LineNumber = lineNumber++, AccountId = apAccount.Id, Debit = 0, Credit = totalAmount, Description = $"فاتورة مشتريات آجلة - {vendor.NameAr}" });
            vendor.ApBalance += totalAmount;
            paidAmount = 0;
        }
        else
        {
            var settlementAccount = await GetSettlementAccountAsync(dto.PaymentTerm, ct);
            journalLines.Add(new JournalEntryLine { LineNumber = lineNumber++, AccountId = settlementAccount.Id, Debit = 0, Credit = totalAmount, Description = $"فاتورة مشتريات - {vendor.NameAr}" });
            paidAmount = totalAmount;
        }

        var entry = new JournalEntry
        {
            EntryNumber = await GenerateEntryNumberAsync(ct),
            EntryDate = dto.InvoiceDate,
            FiscalPeriodId = dto.FiscalPeriodId,
            Description = $"استلام مشتريات - {vendor.NameAr}",
            Reference = AccountingConstants.PurchaseInvoiceReference,
            Status = JournalEntryStatus.Posted,
            Lines = journalLines
        };
        _context.JournalEntries.Add(entry);

        var invoice = new PurchaseInvoice
        {
            InvoiceNumber = await GenerateInvoiceNumberAsync(ct),
            InvoiceDate = dto.InvoiceDate,
            VendorId = vendor.Id,
            FiscalPeriodId = dto.FiscalPeriodId,
            SubTotal = subTotal,
            VatRate = vatRate,
            VatAmount = vatAmount,
            TotalAmount = totalAmount,
            PaymentTerm = dto.PaymentTerm,
            PaidAmount = paidAmount,
            Notes = dto.Notes,
            JournalEntry = entry,
            Lines = lines
        };
        _context.PurchaseInvoices.Add(invoice);

        var movementCount = await _context.StockMovements.CountAsync(ct);
        foreach (var (dtoLine, index) in dto.Lines.Select((l, i) => (l, i)))
        {
            var item = items.First(i => i.Id == dtoLine.ItemId);
            var totalCost = Math.Round(dtoLine.Quantity * dtoLine.UnitCost, 2);
            var newQuantity = item.QuantityOnHand + dtoLine.Quantity;
            var newAverageCost = newQuantity == 0 ? 0 : ((item.QuantityOnHand * item.AverageCost) + totalCost) / newQuantity;

            item.QuantityOnHand = newQuantity;
            item.AverageCost = Math.Round(newAverageCost, 4);

            _context.StockMovements.Add(new StockMovement
            {
                MovementNumber = $"SM-{(movementCount + index + 1):D6}",
                MovementDate = dto.InvoiceDate,
                MovementType = StockMovementType.Receipt,
                ItemId = item.Id,
                WarehouseId = dto.WarehouseId,
                Quantity = dtoLine.Quantity,
                UnitCost = dtoLine.UnitCost,
                TotalCost = totalCost,
                Reference = invoice.InvoiceNumber,
                Description = $"استلام مشتريات - {vendor.NameAr}",
                JournalEntryId = entry.Id
            });
        }

        await _context.SaveChangesAsync(ct);

        return await GetInvoiceAsync(invoice.Id, ct);
    }

    public async Task<PurchaseInvoiceDto> RecordPaymentAsync(Guid invoiceId, RecordPurchasePaymentDto dto, CancellationToken ct = default)
    {
        if (dto.Amount <= 0)
            throw new ValidationAppException("قيمة الدفعة يجب أن تكون أكبر من صفر.");
        if (dto.Method == PaymentTerm.Credit)
            throw new ValidationAppException("طريقة سداد الدفعة لازم تكون كاش أو شبكة.");

        var invoice = await _context.PurchaseInvoices
            .Include(i => i.Vendor)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, ct)
            ?? throw new NotFoundException(nameof(PurchaseInvoice), invoiceId);

        if (invoice.PaymentTerm != PaymentTerm.Credit)
            throw new ValidationAppException("الفاتورة دي متسددة بالكامل عند الإنشاء، مفيش سداد إضافي مطلوب.");

        var outstanding = invoice.TotalAmount - invoice.PaidAmount;
        if (dto.Amount > outstanding)
            throw new ValidationAppException($"قيمة الدفعة ({dto.Amount}) أكبر من المبلغ المتبقي ({outstanding}).");

        var period = await _context.FiscalPeriods.FirstOrDefaultAsync(p => p.StartDate <= dto.PaymentDate && p.EndDate >= dto.PaymentDate && !p.IsClosed, ct)
            ?? throw new ValidationAppException("لا توجد فترة محاسبية مفتوحة تغطي تاريخ السداد.");

        var settlementAccount = await GetSettlementAccountAsync(dto.Method, ct);
        var apAccount = await GetAccountAsync(AccountingConstants.AccountsPayableAccountCode, "الموردون", ct);

        var entry = new JournalEntry
        {
            EntryNumber = await GenerateEntryNumberAsync(ct),
            EntryDate = dto.PaymentDate,
            FiscalPeriodId = period.Id,
            Description = $"سداد فاتورة مشتريات {invoice.InvoiceNumber} - {invoice.Vendor!.NameAr}",
            Reference = AccountingConstants.PurchaseInvoiceReference,
            Status = JournalEntryStatus.Posted,
            Lines =
            {
                new JournalEntryLine { LineNumber = 1, AccountId = apAccount.Id, Debit = dto.Amount, Credit = 0, Description = "تخفيض رصيد المورد" },
                new JournalEntryLine { LineNumber = 2, AccountId = settlementAccount.Id, Debit = 0, Credit = dto.Amount, Description = "سداد" }
            }
        };
        _context.JournalEntries.Add(entry);

        invoice.PaidAmount += dto.Amount;
        invoice.Vendor!.ApBalance -= dto.Amount;

        _context.PurchasePayments.Add(new PurchasePayment
        {
            PurchaseInvoiceId = invoice.Id,
            PaymentDate = dto.PaymentDate,
            Amount = dto.Amount,
            Method = dto.Method,
            Reference = dto.Reference,
            JournalEntry = entry
        });

        await _context.SaveChangesAsync(ct);

        return await GetInvoiceAsync(invoice.Id, ct);
    }

    public async Task<byte[]> GetInvoicePdfAsync(Guid id, CancellationToken ct = default)
    {
        var invoice = await LoadInvoiceAsync(id, ct);
        var settings = await _context.CompanySettings.AsNoTracking().FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(nameof(CompanySettings), Guid.Empty);

        var html = PurchaseInvoiceHtmlTemplate.Build(invoice, settings);
        return await _pdfRenderer.RenderAsync(html, ct);
    }

    public async Task<List<VendorAgingDto>> GetApAgingAsync(DateTime? asOfDate = null, CancellationToken ct = default)
    {
        var referenceDate = (asOfDate ?? DateTime.UtcNow).Date;

        var outstandingInvoices = await _context.PurchaseInvoices
            .AsNoTracking()
            .Include(i => i.Vendor)
            .Where(i => i.PaymentTerm == PaymentTerm.Credit && i.TotalAmount > i.PaidAmount)
            .Select(i => new { i.VendorId, VendorCode = i.Vendor!.Code, VendorName = i.Vendor.NameAr, i.InvoiceDate, Outstanding = i.TotalAmount - i.PaidAmount })
            .ToListAsync(ct);

        var buckets = new Dictionary<Guid, VendorAgingDto>();
        foreach (var inv in outstandingInvoices)
        {
            if (!buckets.TryGetValue(inv.VendorId, out var bucket))
            {
                bucket = new VendorAgingDto { VendorId = inv.VendorId, VendorCode = inv.VendorCode, VendorName = inv.VendorName };
                buckets[inv.VendorId] = bucket;
            }

            var ageDays = (referenceDate - inv.InvoiceDate.Date).Days;
            bucket.TotalOutstanding += inv.Outstanding;
            if (ageDays <= 30) bucket.Current += inv.Outstanding;
            else if (ageDays <= 60) bucket.Days31To60 += inv.Outstanding;
            else if (ageDays <= 90) bucket.Days61To90 += inv.Outstanding;
            else bucket.Over90Days += inv.Outstanding;
        }

        return buckets.Values.OrderByDescending(b => b.TotalOutstanding).ToList();
    }

    private async Task<decimal> GetVatRateAsync(CancellationToken ct)
    {
        var settings = await _context.CompanySettings.AsNoTracking().FirstOrDefaultAsync(ct);
        return settings?.VatRate ?? 0;
    }

    private async Task<Account> GetAccountAsync(string code, string arabicLabel, CancellationToken ct)
        => await _context.Accounts.FirstOrDefaultAsync(a => a.Code == code && !a.IsDeleted, ct)
            ?? throw new ValidationAppException($"حساب {arabicLabel} ({code}) غير موجود في دليل الحسابات.");

    private Task<Account> GetSettlementAccountAsync(PaymentTerm term, CancellationToken ct) => term switch
    {
        PaymentTerm.Cash => GetAccountAsync(AccountingConstants.CashOnHandAccountCode, "الصندوق", ct),
        PaymentTerm.Card => GetAccountAsync(AccountingConstants.BankAccountCode, "البنك", ct),
        _ => throw new ValidationAppException("طريقة دفع غير معروفة.")
    };

    private async Task<string> GenerateEntryNumberAsync(CancellationToken ct)
    {
        var count = await _context.JournalEntries.CountAsync(ct);
        return $"JV-{(count + 1):D6}";
    }

    private async Task<string> GenerateInvoiceNumberAsync(CancellationToken ct)
    {
        var count = await _context.PurchaseInvoices.CountAsync(ct);
        return $"PI-{(count + 1):D6}";
    }

    private async Task<PurchaseInvoice> LoadInvoiceAsync(Guid id, CancellationToken ct)
        => await _context.PurchaseInvoices
            .AsNoTracking()
            .Include(i => i.Vendor)
            .Include(i => i.Lines).ThenInclude(l => l.Account)
            .Include(i => i.Lines).ThenInclude(l => l.Item)
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw new NotFoundException(nameof(PurchaseInvoice), id);

    private static VendorDto Map(Vendor v) => new()
    {
        Id = v.Id,
        Code = v.Code,
        NameAr = v.NameAr,
        NameEn = v.NameEn,
        Phone = v.Phone,
        Email = v.Email,
        TaxRegistrationNumber = v.TaxRegistrationNumber,
        IsActive = v.IsActive,
        ApBalance = v.ApBalance
    };

    private static PurchaseInvoiceDto Map(PurchaseInvoice i) => new()
    {
        Id = i.Id,
        InvoiceNumber = i.InvoiceNumber,
        InvoiceDate = i.InvoiceDate,
        VendorId = i.VendorId,
        VendorName = i.Vendor?.NameAr ?? string.Empty,
        SubTotal = i.SubTotal,
        VatRate = i.VatRate,
        VatAmount = i.VatAmount,
        TotalAmount = i.TotalAmount,
        PaymentTerm = i.PaymentTerm,
        PaidAmount = i.PaidAmount,
        OutstandingAmount = i.TotalAmount - i.PaidAmount,
        JournalEntryId = i.JournalEntryId,
        Notes = i.Notes,
        Lines = i.Lines.OrderBy(l => l.LineNumber).Select(l => new PurchaseInvoiceLineDto
        {
            Description = l.Description,
            AccountId = l.AccountId,
            AccountCode = l.Account?.Code ?? string.Empty,
            AccountName = l.Account?.NameAr ?? string.Empty,
            ItemId = l.ItemId,
            ItemCode = l.Item?.Code,
            ItemName = l.Item?.NameAr,
            Quantity = l.Quantity,
            UnitPrice = l.UnitPrice,
            LineTotal = l.LineTotal
        }).ToList(),
        Payments = i.Payments.OrderBy(p => p.PaymentDate).Select(p => new PurchasePaymentDto
        {
            Id = p.Id,
            PaymentDate = p.PaymentDate,
            Amount = p.Amount,
            Method = p.Method,
            Reference = p.Reference,
            JournalEntryId = p.JournalEntryId
        }).ToList()
    };
}
