using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.Sales.DTOs;
using RomaERP.Domain.Accounting;
using RomaERP.Domain.Common;
using RomaERP.Domain.Inventory;
using RomaERP.Domain.Sales;

namespace RomaERP.Application.Sales.Services;

public class SalesService : ISalesService
{
    private readonly IApplicationDbContext _context;

    public SalesService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<CustomerDto>> GetCustomersAsync(CancellationToken ct = default)
    {
        var customers = await _context.Customers.AsNoTracking().OrderBy(c => c.Code).ToListAsync(ct);
        return customers.Select(Map).ToList();
    }

    public async Task<CustomerDto> CreateCustomerAsync(CreateCustomerDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Code) || string.IsNullOrWhiteSpace(dto.NameAr) || string.IsNullOrWhiteSpace(dto.NameEn))
            throw new ValidationAppException("الكود والاسم بالعربي والإنجليزي مطلوبين.");

        if (await _context.Customers.AnyAsync(c => c.Code == dto.Code, ct))
            throw new ValidationAppException("كود العميل ده مستخدم قبل كده.");

        var customer = new Customer
        {
            Code = dto.Code,
            NameAr = dto.NameAr,
            NameEn = dto.NameEn,
            Phone = dto.Phone,
            Email = dto.Email,
            TaxRegistrationNumber = dto.TaxRegistrationNumber,
            IsActive = true
        };

        _context.Customers.Add(customer);
        await _context.SaveChangesAsync(ct);

        return Map(customer);
    }

    public async Task<List<SalesInvoiceDto>> GetInvoicesAsync(CancellationToken ct = default)
    {
        var invoices = await _context.SalesInvoices
            .AsNoTracking()
            .Include(i => i.Customer)
            .Include(i => i.Warehouse)
            .Include(i => i.Lines).ThenInclude(l => l.Item)
            .Include(i => i.Payments)
            .OrderByDescending(i => i.InvoiceDate)
            .ThenByDescending(i => i.InvoiceNumber)
            .ToListAsync(ct);

        return invoices.Select(Map).ToList();
    }

    public async Task<SalesInvoiceDto> GetInvoiceAsync(Guid id, CancellationToken ct = default)
    {
        var invoice = await LoadInvoiceAsync(id, ct);
        return Map(invoice);
    }

    public async Task<SalesInvoiceDto> CreateInvoiceAsync(CreateSalesInvoiceDto dto, CancellationToken ct = default)
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

        var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Id == dto.CustomerId && !c.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Customer), dto.CustomerId);

        var period = await _context.FiscalPeriods.FirstOrDefaultAsync(p => p.Id == dto.FiscalPeriodId, ct)
            ?? throw new NotFoundException(nameof(FiscalPeriod), dto.FiscalPeriodId);
        if (period.IsClosed)
            throw new ValidationAppException("لا يمكن تسجيل فاتورة لفترة محاسبية مقفلة.");

        // Pre-validate inventory lines (load items + check stock) before mutating anything.
        var itemLineInputs = dto.Lines.Where(l => l.ItemId.HasValue).ToList();
        Warehouse? warehouse = null;
        var itemsById = new Dictionary<Guid, Item>();
        if (itemLineInputs.Count > 0)
        {
            if (dto.WarehouseId is null)
                throw new ValidationAppException("لازم تحدد المخزن لما تختار صنف من المخزون في بند الفاتورة.");

            warehouse = await _context.Warehouses.FirstOrDefaultAsync(w => w.Id == dto.WarehouseId && !w.IsDeleted, ct)
                ?? throw new NotFoundException(nameof(Warehouse), dto.WarehouseId.Value);

            foreach (var itemId in itemLineInputs.Select(l => l.ItemId!.Value).Distinct())
            {
                var item = await _context.Items.FirstOrDefaultAsync(i => i.Id == itemId && !i.IsDeleted, ct)
                    ?? throw new NotFoundException(nameof(Item), itemId);
                itemsById[itemId] = item;
            }

            var requestedQuantities = itemLineInputs.GroupBy(l => l.ItemId!.Value).ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));
            foreach (var (itemId, requestedQty) in requestedQuantities)
            {
                var item = itemsById[itemId];
                if (requestedQty > item.QuantityOnHand)
                    throw new ValidationAppException($"الكمية المطلوبة ({requestedQty}) من الصنف {item.Code} أكبر من الرصيد المتاح ({item.QuantityOnHand}).");
            }
        }

        var vatRate = await GetVatRateAsync(ct);

        // Fetched once: this request may add up to two new journal entries (revenue + COGS) before either
        // is saved, so GenerateEntryNumberAsync's DB count can't be re-queried between them without colliding.
        var journalEntrySequenceBase = await _context.JournalEntries.CountAsync(ct);

        var lines = dto.Lines.Select((l, idx) => new SalesInvoiceLine
        {
            LineNumber = idx + 1,
            Description = l.Description,
            Quantity = l.Quantity,
            UnitPrice = l.UnitPrice,
            LineTotal = Math.Round(l.Quantity * l.UnitPrice, 2),
            ItemId = l.ItemId
        }).ToList();

        var subTotal = lines.Sum(l => l.LineTotal);
        var vatAmount = Math.Round(subTotal * vatRate, 2);
        var totalAmount = subTotal + vatAmount;

        var salesRevenueAccount = await GetAccountAsync(AccountingConstants.SalesRevenueAccountCode, "إيرادات المبيعات", ct);
        var outputVatAccount = vatAmount > 0 ? await GetAccountAsync(AccountingConstants.OutputVatAccountCode, "ضريبة القيمة المضافة (مخرجات)", ct) : null;

        var journalLines = new List<JournalEntryLine>
        {
            new() { LineNumber = 2, AccountId = salesRevenueAccount.Id, Debit = 0, Credit = subTotal, Description = "إيرادات مبيعات" }
        };
        if (outputVatAccount is not null)
            journalLines.Add(new JournalEntryLine { LineNumber = 3, AccountId = outputVatAccount.Id, Debit = 0, Credit = vatAmount, Description = "ضريبة مخرجات" });

        decimal paidAmount;
        if (dto.PaymentTerm == PaymentTerm.Credit)
        {
            var arAccount = await GetAccountAsync(AccountingConstants.AccountsReceivableAccountCode, "العملاء", ct);
            journalLines.Insert(0, new JournalEntryLine { LineNumber = 1, AccountId = arAccount.Id, Debit = totalAmount, Credit = 0, Description = $"فاتورة مبيعات آجلة - {customer.NameAr}" });
            customer.ArBalance += totalAmount;
            paidAmount = 0;
        }
        else
        {
            var settlementAccount = await GetSettlementAccountAsync(dto.PaymentTerm, ct);
            journalLines.Insert(0, new JournalEntryLine { LineNumber = 1, AccountId = settlementAccount.Id, Debit = totalAmount, Credit = 0, Description = $"فاتورة مبيعات - {customer.NameAr}" });
            paidAmount = totalAmount;
        }

        var entry = new JournalEntry
        {
            EntryNumber = $"JV-{(journalEntrySequenceBase + 1):D6}",
            EntryDate = dto.InvoiceDate,
            FiscalPeriodId = dto.FiscalPeriodId,
            Description = $"فاتورة مبيعات - {customer.NameAr}",
            Reference = AccountingConstants.SalesInvoiceReference,
            Status = JournalEntryStatus.Posted,
            Lines = journalLines
        };
        _context.JournalEntries.Add(entry);

        var invoiceNumber = await GenerateInvoiceNumberAsync(ct);

        var invoice = new SalesInvoice
        {
            InvoiceNumber = invoiceNumber,
            InvoiceDate = dto.InvoiceDate,
            CustomerId = customer.Id,
            FiscalPeriodId = dto.FiscalPeriodId,
            WarehouseId = warehouse?.Id,
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

        _context.SalesInvoices.Add(invoice);

        // Issue stock and post COGS for every line fulfilled from inventory.
        if (itemLineInputs.Count > 0)
        {
            var cogsAccount = await GetAccountAsync(AccountingConstants.CostOfGoodsSoldAccountCode, "تكلفة البضاعة المباعة", ct);
            var inventoryAccount = await GetAccountAsync(AccountingConstants.InventoryAccountCode, "المخزون", ct);

            var movementSequenceBase = await _context.StockMovements.CountAsync(ct);
            decimal totalCogs = 0;
            var stockMovements = new List<StockMovement>();
            foreach (var (itemLine, movementIndex) in itemLineInputs.Select((l, idx) => (l, idx)))
            {
                var item = itemsById[itemLine.ItemId!.Value];
                var unitCost = item.AverageCost;
                var movementCost = Math.Round(itemLine.Quantity * unitCost, 2);
                totalCogs += movementCost;
                item.QuantityOnHand -= itemLine.Quantity;

                stockMovements.Add(new StockMovement
                {
                    MovementNumber = $"SM-{(movementSequenceBase + movementIndex + 1):D6}",
                    MovementDate = dto.InvoiceDate,
                    MovementType = StockMovementType.Issue,
                    ItemId = item.Id,
                    WarehouseId = warehouse!.Id,
                    Quantity = itemLine.Quantity,
                    UnitCost = unitCost,
                    TotalCost = movementCost,
                    Reference = invoiceNumber,
                    Description = $"صرف لفاتورة مبيعات {invoiceNumber}"
                });
            }

            if (totalCogs > 0)
            {
                var cogsEntry = new JournalEntry
                {
                    EntryNumber = $"JV-{(journalEntrySequenceBase + 2):D6}",
                    EntryDate = dto.InvoiceDate,
                    FiscalPeriodId = dto.FiscalPeriodId,
                    Description = $"تكلفة البضاعة المباعة - فاتورة مبيعات {invoiceNumber}",
                    Reference = AccountingConstants.SalesInvoiceReference,
                    Status = JournalEntryStatus.Posted,
                    Lines =
                    {
                        new JournalEntryLine { LineNumber = 1, AccountId = cogsAccount.Id, Debit = totalCogs, Credit = 0, Description = "تكلفة بضاعة مباعة" },
                        new JournalEntryLine { LineNumber = 2, AccountId = inventoryAccount.Id, Debit = 0, Credit = totalCogs, Description = "صرف من المخزون" }
                    }
                };
                _context.JournalEntries.Add(cogsEntry);
                foreach (var movement in stockMovements)
                    movement.JournalEntry = cogsEntry;
            }

            _context.StockMovements.AddRange(stockMovements);
        }

        await _context.SaveChangesAsync(ct);

        return await GetInvoiceAsync(invoice.Id, ct);
    }

    public async Task<SalesInvoiceDto> RecordPaymentAsync(Guid invoiceId, RecordSalesPaymentDto dto, CancellationToken ct = default)
    {
        if (dto.Amount <= 0)
            throw new ValidationAppException("قيمة الدفعة يجب أن تكون أكبر من صفر.");
        if (dto.Method == PaymentTerm.Credit)
            throw new ValidationAppException("طريقة تحصيل الدفعة لازم تكون كاش أو شبكة.");

        var invoice = await _context.SalesInvoices
            .Include(i => i.Customer)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, ct)
            ?? throw new NotFoundException(nameof(SalesInvoice), invoiceId);

        if (invoice.PaymentTerm != PaymentTerm.Credit)
            throw new ValidationAppException("الفاتورة دي متحصلة بالكامل عند الإنشاء، مفيش تحصيل إضافي مطلوب.");

        var outstanding = invoice.TotalAmount - invoice.PaidAmount;
        if (dto.Amount > outstanding)
            throw new ValidationAppException($"قيمة الدفعة ({dto.Amount}) أكبر من المبلغ المتبقي ({outstanding}).");

        var period = await _context.FiscalPeriods.FirstOrDefaultAsync(p => p.StartDate <= dto.PaymentDate && p.EndDate >= dto.PaymentDate && !p.IsClosed, ct)
            ?? throw new ValidationAppException("لا توجد فترة محاسبية مفتوحة تغطي تاريخ التحصيل.");

        var settlementAccount = await GetSettlementAccountAsync(dto.Method, ct);
        var arAccount = await GetAccountAsync(AccountingConstants.AccountsReceivableAccountCode, "العملاء", ct);

        var entry = new JournalEntry
        {
            EntryNumber = await GenerateEntryNumberAsync(ct),
            EntryDate = dto.PaymentDate,
            FiscalPeriodId = period.Id,
            Description = $"تحصيل فاتورة مبيعات {invoice.InvoiceNumber} - {invoice.Customer!.NameAr}",
            Reference = AccountingConstants.SalesInvoiceReference,
            Status = JournalEntryStatus.Posted,
            Lines =
            {
                new JournalEntryLine { LineNumber = 1, AccountId = settlementAccount.Id, Debit = dto.Amount, Credit = 0, Description = "تحصيل" },
                new JournalEntryLine { LineNumber = 2, AccountId = arAccount.Id, Debit = 0, Credit = dto.Amount, Description = "تخفيض رصيد العميل" }
            }
        };
        _context.JournalEntries.Add(entry);

        invoice.PaidAmount += dto.Amount;
        invoice.Customer!.ArBalance -= dto.Amount;

        _context.SalesPayments.Add(new SalesPayment
        {
            SalesInvoiceId = invoice.Id,
            PaymentDate = dto.PaymentDate,
            Amount = dto.Amount,
            Method = dto.Method,
            Reference = dto.Reference,
            JournalEntry = entry
        });

        await _context.SaveChangesAsync(ct);

        return await GetInvoiceAsync(invoice.Id, ct);
    }

    public async Task<List<CustomerAgingDto>> GetArAgingAsync(DateTime? asOfDate = null, CancellationToken ct = default)
    {
        var referenceDate = (asOfDate ?? DateTime.UtcNow).Date;

        var outstandingInvoices = await _context.SalesInvoices
            .AsNoTracking()
            .Include(i => i.Customer)
            .Where(i => i.PaymentTerm == PaymentTerm.Credit && i.TotalAmount > i.PaidAmount)
            .Select(i => new { i.CustomerId, CustomerCode = i.Customer!.Code, CustomerName = i.Customer.NameAr, i.InvoiceDate, Outstanding = i.TotalAmount - i.PaidAmount })
            .ToListAsync(ct);

        var buckets = new Dictionary<Guid, CustomerAgingDto>();
        foreach (var inv in outstandingInvoices)
        {
            if (!buckets.TryGetValue(inv.CustomerId, out var bucket))
            {
                bucket = new CustomerAgingDto { CustomerId = inv.CustomerId, CustomerCode = inv.CustomerCode, CustomerName = inv.CustomerName };
                buckets[inv.CustomerId] = bucket;
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
        var count = await _context.SalesInvoices.CountAsync(ct);
        return $"SI-{(count + 1):D6}";
    }

    private async Task<SalesInvoice> LoadInvoiceAsync(Guid id, CancellationToken ct)
        => await _context.SalesInvoices
            .AsNoTracking()
            .Include(i => i.Customer)
            .Include(i => i.Warehouse)
            .Include(i => i.Lines).ThenInclude(l => l.Item)
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw new NotFoundException(nameof(SalesInvoice), id);

    private static CustomerDto Map(Customer c) => new()
    {
        Id = c.Id,
        Code = c.Code,
        NameAr = c.NameAr,
        NameEn = c.NameEn,
        Phone = c.Phone,
        Email = c.Email,
        TaxRegistrationNumber = c.TaxRegistrationNumber,
        IsActive = c.IsActive,
        ArBalance = c.ArBalance
    };

    private static SalesInvoiceDto Map(SalesInvoice i) => new()
    {
        Id = i.Id,
        InvoiceNumber = i.InvoiceNumber,
        InvoiceDate = i.InvoiceDate,
        CustomerId = i.CustomerId,
        CustomerName = i.Customer?.NameAr ?? string.Empty,
        SubTotal = i.SubTotal,
        VatRate = i.VatRate,
        VatAmount = i.VatAmount,
        TotalAmount = i.TotalAmount,
        PaymentTerm = i.PaymentTerm,
        PaidAmount = i.PaidAmount,
        OutstandingAmount = i.TotalAmount - i.PaidAmount,
        JournalEntryId = i.JournalEntryId,
        Notes = i.Notes,
        WarehouseId = i.WarehouseId,
        WarehouseName = i.Warehouse?.NameAr,
        Lines = i.Lines.OrderBy(l => l.LineNumber).Select(l => new SalesInvoiceLineDto
        {
            Description = l.Description,
            Quantity = l.Quantity,
            UnitPrice = l.UnitPrice,
            ItemId = l.ItemId,
            ItemCode = l.Item?.Code,
            ItemName = l.Item?.NameAr,
            LineTotal = l.LineTotal
        }).ToList(),
        Payments = i.Payments.OrderBy(p => p.PaymentDate).Select(p => new SalesPaymentDto
        {
            Id = p.Id,
            PaymentDate = p.PaymentDate,
            Amount = p.Amount,
            Method = p.Method,
            Reference = p.Reference,
            JournalEntryId = p.JournalEntryId
        }).ToList(),
        EInvoiceStatus = i.EInvoiceStatus,
        EInvoiceExternalUuid = i.EInvoiceExternalUuid,
        EInvoiceSubmittedAtUtc = i.EInvoiceSubmittedAtUtc,
        EInvoiceErrorMessage = i.EInvoiceErrorMessage
    };
}
