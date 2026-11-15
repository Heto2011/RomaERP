using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting;
using RomaERP.Application.Accounting.DTOs;
using RomaERP.Application.Accounting.Services;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.Inventory.DTOs;
using RomaERP.Application.Inventory.Services;
using RomaERP.Application.Purchasing.DTOs;
using RomaERP.Application.Purchasing.Services;
using RomaERP.Application.Sales.DTOs;
using RomaERP.Application.Sales.Services;
using RomaERP.Domain.Integration;

namespace RomaERP.Application.Integration;

public interface IIntegrationInboxService
{
    Task<IntegrationReceiveResult> ReceiveAsync(string source, string type, string externalRef, JsonElement data, CancellationToken ct = default);
    Task<List<InboxItemDto>> ListAsync(IntegrationItemStatus? status, CancellationToken ct = default);
    Task<InboxItemDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<InboxItemDto> ApproveAsync(Guid id, string? reviewer, CancellationToken ct = default);
    Task<InboxItemDto> RejectAsync(Guid id, string? reason, string? reviewer, CancellationToken ct = default);
    Task<ApproveAllResult> ApproveAllPendingAsync(string? reviewer, CancellationToken ct = default);
}

/// <summary>Everything an outside bot sends lands here first. A person approves each item; approving turns it into the real
/// record (customer, invoice, journal entry...) through the same services the screens use, so every rule still applies.</summary>
public class IntegrationInboxService : IIntegrationInboxService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly IApplicationDbContext _context;
    private readonly ISalesService _sales;
    private readonly IPurchasingService _purchasing;
    private readonly IJournalEntryService _journals;
    private readonly IItemService? _itemService;

    public IntegrationInboxService(IApplicationDbContext context, ISalesService sales, IPurchasingService purchasing, IJournalEntryService journals, IItemService? itemService = null)
    {
        _itemService = itemService;
        _context = context;
        _sales = sales;
        _purchasing = purchasing;
        _journals = journals;
    }

    // ---------- intake ----------

    public async Task<IntegrationReceiveResult> ReceiveAsync(string source, string type, string externalRef, JsonElement data, CancellationToken ct = default)
    {
        var itemType = ParseType(type);
        externalRef = (externalRef ?? string.Empty).Trim();
        if (externalRef.Length == 0 || externalRef.Length > 100)
            throw new ValidationAppException("externalRef مطلوب (رقمك المرجعي للعنصر، لحد 100 حرف). نفس الرقم بيتجاهل التكرار.");
        if (data.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            throw new ValidationAppException("data مطلوبة.");

        var existing = await _context.IntegrationInboxItems.AsNoTracking().FirstOrDefaultAsync(i => i.Type == itemType && i.ExternalRef == externalRef, ct);
        if (existing is not null)
            return new IntegrationReceiveResult { Id = existing.Id, Status = existing.Status.ToString(), Duplicate = true };

        var (title, amount) = Describe(itemType, data);
        var item = new IntegrationInboxItem
        {
            Type = itemType,
            ExternalRef = externalRef,
            Title = Truncate(title, 300),
            Amount = amount,
            PayloadJson = data.GetRawText(),
            Source = Truncate(source, 100)
        };
        _context.IntegrationInboxItems.Add(item);
        await _context.SaveChangesAsync(ct);
        return new IntegrationReceiveResult { Id = item.Id, Status = item.Status.ToString(), Duplicate = false };
    }

    private static IntegrationItemType ParseType(string? type)
    {
        var normalized = (type ?? string.Empty).Replace("-", "").Replace("_", "").Replace(" ", "");
        if (Enum.TryParse<IntegrationItemType>(normalized, true, out var parsed) && Enum.IsDefined(parsed)) return parsed;
        throw new ValidationAppException($"type غير معروف. القيم المسموحة: {string.Join(", ", Enum.GetNames<IntegrationItemType>())}.");
    }

    private static T Read<T>(JsonElement data) where T : new()
    {
        try { return data.Deserialize<T>(Json) ?? new T(); }
        catch (JsonException ex) { throw new ValidationAppException($"شكل data مش مظبوط: {ex.Message}"); }
    }

    private static (string Title, decimal? Amount) Describe(IntegrationItemType type, JsonElement data)
    {
        switch (type)
        {
            case IntegrationItemType.Customer:
            {
                var c = Read<CreateCustomerDto>(data);
                if (string.IsNullOrWhiteSpace(c.NameAr) && string.IsNullOrWhiteSpace(c.NameEn)) throw new ValidationAppException("اسم العميل مطلوب (nameAr أو nameEn).");
                return ($"عميل: {FirstNonEmpty(c.NameAr, c.NameEn)}", null);
            }
            case IntegrationItemType.Vendor:
            {
                var v = Read<CreateVendorDto>(data);
                if (string.IsNullOrWhiteSpace(v.NameAr) && string.IsNullOrWhiteSpace(v.NameEn)) throw new ValidationAppException("اسم المورد مطلوب (nameAr أو nameEn).");
                return ($"مورد: {FirstNonEmpty(v.NameAr, v.NameEn)}", null);
            }
            case IntegrationItemType.SalesInvoice:
            {
                var p = Read<IntegrationSalesInvoicePayload>(data);
                if (string.IsNullOrWhiteSpace(p.CustomerCode) && string.IsNullOrWhiteSpace(p.CustomerName)) throw new ValidationAppException("العميل مطلوب (customerCode أو customerName).");
                RequireDate(p.InvoiceDate, "invoiceDate");
                if (p.Lines.Count == 0) throw new ValidationAppException("لازم سطر واحد على الأقل في lines.");
                if (p.Lines.Any(l => l.Quantity <= 0 || l.UnitPrice < 0)) throw new ValidationAppException("الكمية لازم تكون أكبر من صفر والسعر مش بالسالب.");
                return ($"فاتورة مبيعات — {FirstNonEmpty(p.CustomerName, p.CustomerCode)}", Math.Round(p.Lines.Sum(l => l.Quantity * l.UnitPrice), 2));
            }
            case IntegrationItemType.PurchaseInvoice:
            {
                var p = Read<IntegrationPurchaseInvoicePayload>(data);
                if (string.IsNullOrWhiteSpace(p.VendorCode) && string.IsNullOrWhiteSpace(p.VendorName)) throw new ValidationAppException("المورد مطلوب (vendorCode أو vendorName).");
                RequireDate(p.InvoiceDate, "invoiceDate");
                if (p.Lines.Count == 0) throw new ValidationAppException("لازم سطر واحد على الأقل في lines.");
                if (p.Lines.Any(l => l.Quantity <= 0 || l.UnitPrice < 0)) throw new ValidationAppException("الكمية لازم تكون أكبر من صفر والسعر مش بالسالب.");
                return ($"فاتورة مشتريات — {FirstNonEmpty(p.VendorName, p.VendorCode)}", Math.Round(p.Lines.Sum(l => l.Quantity * l.UnitPrice), 2));
            }
            case IntegrationItemType.SalesPayment:
            case IntegrationItemType.PurchasePayment:
            {
                var p = Read<IntegrationPaymentPayload>(data);
                if (string.IsNullOrWhiteSpace(p.InvoiceExternalRef)) throw new ValidationAppException("invoiceExternalRef مطلوب (المرجع اللي بعت بيه الفاتورة).");
                if (p.Amount <= 0) throw new ValidationAppException("المبلغ لازم يكون أكبر من صفر.");
                RequireDate(p.PaymentDate, "paymentDate");
                return ((type == IntegrationItemType.SalesPayment ? "دفعة من عميل" : "دفعة لمورد") + $" — فاتورة {p.InvoiceExternalRef}", Math.Round(p.Amount, 2));
            }
            case IntegrationItemType.Expense:
            {
                var p = Read<IntegrationExpensePayload>(data);
                if (p.Amount <= 0) throw new ValidationAppException("المبلغ لازم يكون أكبر من صفر.");
                if (string.IsNullOrWhiteSpace(p.Description)) throw new ValidationAppException("وصف المصروف مطلوب (description).");
                RequireDate(p.Date, "date");
                return ($"مصروف: {p.Description.Trim()}", Math.Round(p.Amount, 2));
            }
            case IntegrationItemType.JournalEntry:
            {
                var p = Read<IntegrationJournalPayload>(data);
                RequireDate(p.Date, "date");
                if (p.Lines.Count < 2) throw new ValidationAppException("القيد لازم يكون فيه سطرين على الأقل.");
                if (p.Lines.Any(l => string.IsNullOrWhiteSpace(l.AccountCode) || l.Debit < 0 || l.Credit < 0 || (l.Debit > 0) == (l.Credit > 0)))
                    throw new ValidationAppException("كل سطر لازم يكون له accountCode وطرف واحد بس (مدين أو دائن) بمبلغ موجب.");
                var debit = Math.Round(p.Lines.Sum(l => l.Debit), 2);
                if (debit != Math.Round(p.Lines.Sum(l => l.Credit), 2)) throw new ValidationAppException("القيد مش متوازن: مجموع المدين لازم يساوي مجموع الدائن.");
                return ($"قيد يومية: {FirstNonEmpty(p.Description, p.Reference, "بدون وصف")}", debit);
            }
            case IntegrationItemType.Item:
            {
                var p = Read<IntegrationItemPayload>(data);
                if (string.IsNullOrWhiteSpace(p.Code)) throw new ValidationAppException("كود الصنف مطلوب (code).");
                if (string.IsNullOrWhiteSpace(p.NameAr) && string.IsNullOrWhiteSpace(p.NameEn)) throw new ValidationAppException("اسم الصنف مطلوب (nameAr أو nameEn).");
                return ($"صنف: {FirstNonEmpty(p.NameAr, p.NameEn)} ({p.Code.Trim()})", null);
            }
            case IntegrationItemType.BankStatementLine:
            {
                var p = Read<IntegrationBankLinePayload>(data);
                RequireDate(p.Date, "date");
                if (p.Amount == 0) throw new ValidationAppException("المبلغ لازم يكون مش صفر (موجب = إيداع، سالب = سحب).");
                if (string.IsNullOrWhiteSpace(p.Description)) throw new ValidationAppException("وصف الحركة مطلوب (description).");
                return ($"حركة بنك: {p.Description.Trim()}", Math.Round(p.Amount, 2));
            }
            default:
            {
                string? title = null;
                if (data.ValueKind == JsonValueKind.Object)
                {
                    foreach (var name in new[] { "title", "category", "name" })
                        if (data.TryGetProperty(name, out var t) && t.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(t.GetString())) { title = t.GetString(); break; }
                }
                return ($"بيانات: {title ?? "من العميل"}", null);
            }
        }
    }

    // ---------- review ----------

    public async Task<List<InboxItemDto>> ListAsync(IntegrationItemStatus? status, CancellationToken ct = default)
    {
        var query = _context.IntegrationInboxItems.AsNoTracking();
        if (status is not null) query = query.Where(i => i.Status == status);
        var items = await query.OrderByDescending(i => i.ReceivedAtUtc).Take(500).ToListAsync(ct);
        return items.Select(i => Map(i, false)).ToList();
    }

    public async Task<InboxItemDto> GetAsync(Guid id, CancellationToken ct = default)
        => Map(await Load(id, ct), true);

    private async Task<IntegrationInboxItem> Load(Guid id, CancellationToken ct)
        => await _context.IntegrationInboxItems.FirstOrDefaultAsync(i => i.Id == id, ct) ?? throw new NotFoundException(nameof(IntegrationInboxItem), id);

    public async Task<InboxItemDto> RejectAsync(Guid id, string? reason, string? reviewer, CancellationToken ct = default)
    {
        var item = await Load(id, ct);
        if (item.Status == IntegrationItemStatus.Approved) throw new ValidationAppException("العنصر ده اتعتمد قبل كده ومش ممكن يترفض.");
        item.Status = IntegrationItemStatus.Rejected;
        item.ReviewedAtUtc = DateTime.UtcNow;
        item.ReviewedBy = reviewer;
        item.ReviewNote = Truncate(reason ?? string.Empty, 500);
        item.ErrorMessage = null;
        await _context.SaveChangesAsync(ct);
        return Map(item, false);
    }

    public async Task<InboxItemDto> ApproveAsync(Guid id, string? reviewer, CancellationToken ct = default)
    {
        var item = await Load(id, ct);
        if (item.Status is IntegrationItemStatus.Approved or IntegrationItemStatus.Rejected)
            throw new ValidationAppException("العنصر ده اتراجع قبل كده.");

        try
        {
            item.ResultEntityId = await ApplyAsync(item, ct);
            item.Status = IntegrationItemStatus.Approved;
            item.ErrorMessage = null;
        }
        catch (Exception ex) when (ex is ValidationAppException or NotFoundException)
        {
            item.Status = IntegrationItemStatus.Failed;
            item.ErrorMessage = Truncate(ex.Message, 1000);
        }
        item.ReviewedAtUtc = DateTime.UtcNow;
        item.ReviewedBy = reviewer;
        await _context.SaveChangesAsync(ct);
        return Map(item, false);
    }

    public async Task<ApproveAllResult> ApproveAllPendingAsync(string? reviewer, CancellationToken ct = default)
    {
        // Parents first (customers and vendors), then invoices, then payments: so an invoice can find its customer.
        var ids = (await _context.IntegrationInboxItems.AsNoTracking()
                .Where(i => i.Status == IntegrationItemStatus.Pending).ToListAsync(ct))
            .OrderBy(i => Rank(i.Type)).ThenBy(i => i.ReceivedAtUtc).Select(i => i.Id).ToList();
        var result = new ApproveAllResult();
        foreach (var id in ids)
        {
            var dto = await ApproveAsync(id, reviewer, ct);
            if (dto.Status == nameof(IntegrationItemStatus.Approved)) result.Approved++; else result.Failed++;
        }
        return result;
    }

    /// <summary>Approval order: things others refer to come first (customers, vendors, items), then invoices, then payments.</summary>
    private static int Rank(IntegrationItemType type) => type switch
    {
        IntegrationItemType.Customer => 1, IntegrationItemType.Vendor => 2, IntegrationItemType.Item => 3,
        IntegrationItemType.SalesInvoice => 4, IntegrationItemType.PurchaseInvoice => 5,
        IntegrationItemType.SalesPayment => 6, IntegrationItemType.PurchasePayment => 7,
        IntegrationItemType.Expense => 8, IntegrationItemType.JournalEntry => 9, IntegrationItemType.BankStatementLine => 10,
        _ => 11
    };

    // ---------- turning an approved item into the real record ----------

    private async Task<Guid?> ApplyAsync(IntegrationInboxItem item, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(item.PayloadJson);
        var data = doc.RootElement;
        switch (item.Type)
        {
            case IntegrationItemType.Customer: return await ApplyCustomerAsync(item, data, ct);
            case IntegrationItemType.Vendor: return await ApplyVendorAsync(item, data, ct);
            case IntegrationItemType.SalesInvoice: return await ApplySalesInvoiceAsync(Read<IntegrationSalesInvoicePayload>(data), ct);
            case IntegrationItemType.PurchaseInvoice: return await ApplyPurchaseInvoiceAsync(Read<IntegrationPurchaseInvoicePayload>(data), ct);
            case IntegrationItemType.SalesPayment:
            {
                var p = Read<IntegrationPaymentPayload>(data);
                var invoiceId = await LinkedInvoiceAsync(IntegrationItemType.SalesInvoice, p.InvoiceExternalRef, ct);
                await _sales.RecordPaymentAsync(invoiceId, new RecordSalesPaymentDto { Amount = p.Amount, Method = p.Method, PaymentDate = p.PaymentDate, Reference = p.Reference ?? item.ExternalRef }, ct);
                return invoiceId;
            }
            case IntegrationItemType.PurchasePayment:
            {
                var p = Read<IntegrationPaymentPayload>(data);
                var invoiceId = await LinkedInvoiceAsync(IntegrationItemType.PurchaseInvoice, p.InvoiceExternalRef, ct);
                await _purchasing.RecordPaymentAsync(invoiceId, new RecordPurchasePaymentDto { Amount = p.Amount, Method = p.Method, PaymentDate = p.PaymentDate, Reference = p.Reference ?? item.ExternalRef }, ct);
                return invoiceId;
            }
            case IntegrationItemType.Expense: return await ApplyExpenseAsync(item, Read<IntegrationExpensePayload>(data), ct);
            case IntegrationItemType.JournalEntry: return await ApplyJournalAsync(Read<IntegrationJournalPayload>(data), ct);
            case IntegrationItemType.Item: return await ApplyItemAsync(Read<IntegrationItemPayload>(data), ct);
            case IntegrationItemType.BankStatementLine: return await ApplyBankLineAsync(item, Read<IntegrationBankLinePayload>(data), ct);
            default: return null; // Data: reviewing it is the whole job
        }
    }

    private async Task<Guid?> ApplyCustomerAsync(IntegrationInboxItem item, JsonElement data, CancellationToken ct)
    {
        var dto = Read<CreateCustomerDto>(data);
        dto.Code = CodeOrFallback(dto.Code, item.ExternalRef);
        if (string.IsNullOrWhiteSpace(dto.NameAr)) dto.NameAr = dto.NameEn;
        if (string.IsNullOrWhiteSpace(dto.NameEn)) dto.NameEn = dto.NameAr;
        var existing = await _context.Customers.FirstOrDefaultAsync(c => c.Code == dto.Code, ct);
        if (existing is null) return (await _sales.CreateCustomerAsync(dto, ct)).Id;
        var update = JsonSerializer.Deserialize<UpdateCustomerDto>(JsonSerializer.Serialize(dto, Json), Json)!;
        return (await _sales.UpdateCustomerAsync(existing.Id, update, ct)).Id;
    }

    private async Task<Guid?> ApplyVendorAsync(IntegrationInboxItem item, JsonElement data, CancellationToken ct)
    {
        var dto = Read<CreateVendorDto>(data);
        dto.Code = CodeOrFallback(dto.Code, item.ExternalRef);
        if (string.IsNullOrWhiteSpace(dto.NameAr)) dto.NameAr = dto.NameEn;
        if (string.IsNullOrWhiteSpace(dto.NameEn)) dto.NameEn = dto.NameAr;
        var existing = await _context.Vendors.FirstOrDefaultAsync(v => v.Code == dto.Code, ct);
        if (existing is null) return (await _purchasing.CreateVendorAsync(dto, ct)).Id;
        var update = JsonSerializer.Deserialize<UpdateVendorDto>(JsonSerializer.Serialize(dto, Json), Json)!;
        return (await _purchasing.UpdateVendorAsync(existing.Id, update, ct)).Id;
    }

    private async Task<Guid?> ApplySalesInvoiceAsync(IntegrationSalesInvoicePayload p, CancellationToken ct)
    {
        var customerId = await FindCustomerAsync(p.CustomerCode, p.CustomerName, ct);
        var dto = new CreateSalesInvoiceDto
        {
            CustomerId = customerId,
            InvoiceDate = p.InvoiceDate,
            FiscalPeriodId = await FiscalPeriodForAsync(p.InvoiceDate, ct),
            PaymentTerm = p.PaymentTerm,
            Notes = p.Notes,
            CurrencyCode = string.IsNullOrWhiteSpace(p.CurrencyCode) ? null : p.CurrencyCode.Trim().ToUpperInvariant(),
            NumberOfInstallments = p.NumberOfInstallments,
            FirstInstallmentDueDate = p.FirstInstallmentDueDate
        };
        foreach (var line in p.Lines)
        {
            Guid? itemId = null;
            if (!string.IsNullOrWhiteSpace(line.ItemCode)) itemId = await FindItemAsync(line.ItemCode, ct);
            dto.Lines.Add(new SalesInvoiceLineInputDto { Description = line.Description, Quantity = line.Quantity, UnitPrice = line.UnitPrice, ItemId = itemId });
        }
        if (dto.Lines.Any(l => l.ItemId is not null))
            dto.WarehouseId = await FindWarehouseAsync(p.WarehouseCode, ct);
        return (await _sales.CreateInvoiceAsync(dto, ct)).Id;
    }

    private async Task<Guid?> ApplyPurchaseInvoiceAsync(IntegrationPurchaseInvoicePayload p, CancellationToken ct)
    {
        var vendorId = await FindVendorAsync(p.VendorCode, p.VendorName, ct);
        var dto = new CreatePurchaseInvoiceDto
        {
            VendorId = vendorId,
            InvoiceDate = p.InvoiceDate,
            FiscalPeriodId = await FiscalPeriodForAsync(p.InvoiceDate, ct),
            PaymentTerm = p.PaymentTerm,
            Notes = p.Notes,
            CurrencyCode = string.IsNullOrWhiteSpace(p.CurrencyCode) ? null : p.CurrencyCode.Trim().ToUpperInvariant()
        };
        foreach (var line in p.Lines)
        {
            Guid? itemId = null;
            if (!string.IsNullOrWhiteSpace(line.ItemCode)) itemId = await FindItemAsync(line.ItemCode, ct);
            var accountCode = string.IsNullOrWhiteSpace(line.AccountCode)
                ? (itemId is null ? AccountingConstants.GeneralAdminExpenseAccountCode : AccountingConstants.InventoryAccountCode)
                : line.AccountCode.Trim();
            dto.Lines.Add(new PurchaseInvoiceLineInputDto
            {
                Description = line.Description, Quantity = line.Quantity, UnitPrice = line.UnitPrice, ItemId = itemId,
                AccountId = await FindAccountAsync(accountCode, ct)
            });
        }
        if (!string.IsNullOrWhiteSpace(p.ReceiveToWarehouseCode)) dto.ReceiveToWarehouseId = await FindWarehouseAsync(p.ReceiveToWarehouseCode, ct);
        return (await _purchasing.CreateInvoiceAsync(dto, ct)).Id;
    }

    private async Task<Guid?> ApplyItemAsync(IntegrationItemPayload p, CancellationToken ct)
    {
        if (_itemService is null) throw new ValidationAppException("خدمة الأصناف مش متاحة.");
        var code = Truncate(p.Code.Trim(), 20);
        var nameAr = string.IsNullOrWhiteSpace(p.NameAr) ? p.NameEn : p.NameAr;
        var nameEn = string.IsNullOrWhiteSpace(p.NameEn) ? p.NameAr : p.NameEn;
        var unit = string.IsNullOrWhiteSpace(p.UnitOfMeasure) ? "unit" : p.UnitOfMeasure.Trim();
        var categoryId = await CategoryIdAsync(p.CategoryCode, ct);
        var existing = await _context.Items.AsNoTracking().FirstOrDefaultAsync(i => i.Code == code, ct);
        if (existing is not null)
        {
            await _itemService.UpdateAsync(existing.Id, new UpdateItemDto { NameAr = nameAr, NameEn = nameEn, UnitOfMeasure = unit, ItemCategoryId = categoryId, ReorderLevel = p.ReorderLevel, LeadTimeDays = p.LeadTimeDays, IsLotTracked = p.IsLotTracked }, ct);
            return existing.Id;
        }
        return (await _itemService.CreateAsync(new CreateItemDto { Code = code, NameAr = nameAr, NameEn = nameEn, UnitOfMeasure = unit, ItemCategoryId = categoryId, ReorderLevel = p.ReorderLevel, LeadTimeDays = p.LeadTimeDays, IsLotTracked = p.IsLotTracked }, ct)).Id;
    }

    private async Task<Guid> CategoryIdAsync(string? code, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(code))
            return (await _context.ItemCategories.AsNoTracking().FirstOrDefaultAsync(c => c.Code == code.Trim(), ct))?.Id
                   ?? throw new ValidationAppException($"تصنيف الأصناف \"{code}\" مش موجود.");
        var first = await _context.ItemCategories.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.Code).FirstOrDefaultAsync(ct);
        if (first is not null) return first.Id;
        var general = new RomaERP.Domain.Inventory.ItemCategory { Code = "GEN", NameAr = "عام", NameEn = "General" };
        _context.ItemCategories.Add(general);
        await _context.SaveChangesAsync(ct);
        return general.Id;
    }

    private async Task<Guid?> ApplyBankLineAsync(IntegrationInboxItem item, IntegrationBankLinePayload p, CancellationToken ct)
    {
        var accountId = await FindAccountAsync(string.IsNullOrWhiteSpace(p.AccountCode) ? AccountingConstants.BankAccountCode : p.AccountCode.Trim(), ct);
        var duplicate = await _context.BankFeedTransactions.AsNoTracking().AnyAsync(t => t.AccountId == accountId && t.ExternalId == item.ExternalRef, ct);
        if (duplicate) throw new ValidationAppException("حركة البنك دي اتسجلت قبل كده بنفس المرجع.");
        var line = new RomaERP.Domain.Accounting.BankFeedTransaction
        {
            AccountId = accountId, TransactionDate = p.Date.Date, Description = Truncate(p.Description.Trim(), 300),
            Amount = Math.Round(p.Amount, 2), Source = "Bot", ExternalId = item.ExternalRef
        };
        _context.BankFeedTransactions.Add(line);
        await _context.SaveChangesAsync(ct);
        return line.Id;
    }

    private async Task<Guid?> ApplyExpenseAsync(IntegrationInboxItem item, IntegrationExpensePayload p, CancellationToken ct)
    {
        var expenseId = await FindAccountAsync(string.IsNullOrWhiteSpace(p.ExpenseAccountCode) ? AccountingConstants.GeneralAdminExpenseAccountCode : p.ExpenseAccountCode.Trim(), ct);
        var paidFromCode = !string.IsNullOrWhiteSpace(p.PaidFromAccountCode) ? p.PaidFromAccountCode.Trim()
            : (p.PaidFrom ?? "cash").Trim().ToLowerInvariant() switch
            {
                "bank" => AccountingConstants.BankAccountCode,
                "payable" => AccountingConstants.AccountsPayableAccountCode,
                "cash" or "" => AccountingConstants.CashOnHandAccountCode,
                _ => throw new ValidationAppException("paidFrom لازم تكون cash أو bank أو payable (أو ابعت paidFromAccountCode).")
            };
        var paidFromId = await FindAccountAsync(paidFromCode, ct);
        var period = await FiscalPeriodForAsync(p.Date, ct);
        var entry = await SimpleJournalEntryFactory.CreatePostedAsync(_context, p.Date, period, Truncate(p.Description, 200), expenseId, paidFromId,
            Math.Round(p.Amount, 2), Truncate(p.Reference ?? item.ExternalRef, 50), ct);
        await _context.SaveChangesAsync(ct);
        return entry.Id;
    }

    private async Task<Guid?> ApplyJournalAsync(IntegrationJournalPayload p, CancellationToken ct)
    {
        var dto = new CreateJournalEntryDto
        {
            EntryDate = p.Date,
            FiscalPeriodId = await FiscalPeriodForAsync(p.Date, ct),
            Description = p.Description,
            Reference = p.Reference
        };
        foreach (var line in p.Lines)
            dto.Lines.Add(new CreateJournalEntryLineDto { AccountId = await FindAccountAsync(line.AccountCode.Trim(), ct), Debit = line.Debit, Credit = line.Credit, Description = line.Description });
        var created = await _journals.CreateAsync(dto, ct);
        await _journals.PostAsync(created.Id, ct);
        return created.Id;
    }

    // ---------- lookups ----------

    private async Task<Guid> LinkedInvoiceAsync(IntegrationItemType invoiceType, string externalRef, CancellationToken ct)
    {
        var invoice = await _context.IntegrationInboxItems.AsNoTracking().FirstOrDefaultAsync(i => i.Type == invoiceType && i.ExternalRef == externalRef.Trim(), ct);
        if (invoice is null) throw new ValidationAppException($"مفيش فاتورة اتبعتت بالمرجع {externalRef}. ابعت الفاتورة الأول.");
        if (invoice.Status != IntegrationItemStatus.Approved || invoice.ResultEntityId is null)
            throw new ValidationAppException($"الفاتورة {externalRef} لسه ما اتعتمدتش. اعتمدها الأول وبعدين اعتمد الدفعة.");
        return invoice.ResultEntityId.Value;
    }

    private async Task<Guid> FindCustomerAsync(string? code, string? name, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(code))
        {
            var byCode = await _context.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Code == code.Trim(), ct);
            if (byCode is not null) return byCode.Id;
        }
        if (!string.IsNullOrWhiteSpace(name))
        {
            var n = name.Trim();
            var byName = await _context.Customers.AsNoTracking().Where(c => c.NameAr == n || c.NameEn == n).ToListAsync(ct);
            if (byName.Count == 1) return byName[0].Id;
            if (byName.Count > 1) throw new ValidationAppException($"في أكتر من عميل بالاسم \"{n}\". ابعت customerCode.");
        }
        throw new ValidationAppException($"العميل \"{FirstNonEmpty(code, name)}\" مش موجود. ابعت العميل الأول (type: Customer) واعتمده.");
    }

    private async Task<Guid> FindVendorAsync(string? code, string? name, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(code))
        {
            var byCode = await _context.Vendors.AsNoTracking().FirstOrDefaultAsync(v => v.Code == code.Trim(), ct);
            if (byCode is not null) return byCode.Id;
        }
        if (!string.IsNullOrWhiteSpace(name))
        {
            var n = name.Trim();
            var byName = await _context.Vendors.AsNoTracking().Where(v => v.NameAr == n || v.NameEn == n).ToListAsync(ct);
            if (byName.Count == 1) return byName[0].Id;
            if (byName.Count > 1) throw new ValidationAppException($"في أكتر من مورد بالاسم \"{n}\". ابعت vendorCode.");
        }
        throw new ValidationAppException($"المورد \"{FirstNonEmpty(code, name)}\" مش موجود. ابعت المورد الأول (type: Vendor) واعتمده.");
    }

    private async Task<Guid> FindItemAsync(string code, CancellationToken ct)
        => (await _context.Items.AsNoTracking().FirstOrDefaultAsync(i => i.Code == code.Trim(), ct))?.Id
           ?? throw new ValidationAppException($"الصنف \"{code}\" مش موجود في المخزون.");

    private async Task<Guid> FindWarehouseAsync(string? code, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ValidationAppException("في سطور مرتبطة بأصناف، فلازم تبعت warehouseCode.");
        return (await _context.Warehouses.AsNoTracking().FirstOrDefaultAsync(w => w.Code == code.Trim(), ct))?.Id
               ?? throw new ValidationAppException($"المخزن \"{code}\" مش موجود.");
    }

    private async Task<Guid> FindAccountAsync(string code, CancellationToken ct)
        => (await _context.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.Code == code && !a.IsDeleted, ct))?.Id
           ?? throw new ValidationAppException($"الحساب {code} مش موجود في دليل الحسابات.");

    private async Task<Guid> FiscalPeriodForAsync(DateTime date, CancellationToken ct)
    {
        var day = date.Date;
        var period = await _context.FiscalPeriods.AsNoTracking().FirstOrDefaultAsync(p => p.StartDate <= day && p.EndDate >= day, ct)
            ?? throw new ValidationAppException($"مفيش فترة محاسبية بتغطي تاريخ {day:yyyy-MM-dd}.");
        if (period.IsClosed) throw new ValidationAppException($"الفترة المحاسبية ({period.Name}) مقفولة.");
        return period.Id;
    }

    // ---------- helpers ----------

    private static InboxItemDto Map(IntegrationInboxItem i, bool withPayload) => new()
    {
        Id = i.Id, Type = i.Type.ToString(), ExternalRef = i.ExternalRef, Title = i.Title, Amount = i.Amount, Status = i.Status.ToString(),
        Source = i.Source, ReceivedAtUtc = i.ReceivedAtUtc, ReviewedAtUtc = i.ReviewedAtUtc, ReviewedBy = i.ReviewedBy, ReviewNote = i.ReviewNote,
        ErrorMessage = i.ErrorMessage, ResultEntityId = i.ResultEntityId, PayloadJson = withPayload ? i.PayloadJson : null
    };

    private static string CodeOrFallback(string? code, string externalRef)
    {
        var chosen = string.IsNullOrWhiteSpace(code) ? "EXT-" + new string(externalRef.Where(char.IsLetterOrDigit).ToArray()) : code.Trim();
        return Truncate(chosen, 20);
    }

    private static void RequireDate(DateTime date, string field)
    {
        if (date == default || date.Year < 2000) throw new ValidationAppException($"{field} مطلوب (تاريخ صالح).");
    }

    private static string FirstNonEmpty(params string?[] values) => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim() ?? string.Empty;
    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
