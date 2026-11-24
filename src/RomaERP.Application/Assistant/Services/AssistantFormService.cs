using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Assistant.DTOs;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.HR.DTOs;
using RomaERP.Application.HR.Services;
using RomaERP.Application.Inventory.DTOs;
using RomaERP.Application.Inventory.Services;
using RomaERP.Application.Purchasing.DTOs;
using RomaERP.Application.Purchasing.Services;
using RomaERP.Application.Sales.DTOs;
using RomaERP.Application.Sales.Services;
using RomaERP.Domain.Assistant;

namespace RomaERP.Application.Assistant.Services;

public interface IAssistantFormService
{
    Task<ActionProposalDto> PrepareAsync(string formKind, IReadOnlyDictionary<string, string?> args, CancellationToken ct = default);
    Task<ExecuteActionResultDto> ExecuteAsync(string formKind, IReadOnlyDictionary<string, string?> values, CancellationToken ct = default);
}

/// <summary>The drafts that are plain forms: change a customer, supplier or item, approve something waiting, a credit note, a stock transfer, a waste entry.
/// The assistant only fills the form in; the owner edits it and presses Execute, and the same services as the normal screens do the booking after the role of the person is checked here.</summary>
public class AssistantFormService : IAssistantFormService
{
    public const int MaxOptions = 40;
    private static readonly string[] Finance = { "Admin", "Accountant" };

    private readonly IApplicationDbContext _c;
    private readonly ISalesService _sales;
    private readonly IPurchasingService _purchasing;
    private readonly IItemService _items;
    private readonly IExpenseAssistantService _expenses;
    private readonly IEmployeeRequestService _requests;
    private readonly IStockTransferService _transfers;
    private readonly IWasteEntryService _waste;
    private readonly ICurrentUserService _user;

    public AssistantFormService(IApplicationDbContext c, ISalesService sales, IPurchasingService purchasing, IItemService items, IExpenseAssistantService expenses,
        IEmployeeRequestService requests, IStockTransferService transfers, IWasteEntryService waste, ICurrentUserService user)
    {
        _c = c; _sales = sales; _purchasing = purchasing; _items = items; _expenses = expenses; _requests = requests; _transfers = transfers; _waste = waste; _user = user;
    }

    private static string Norm(string? s) => (s ?? string.Empty).Trim().ToLowerInvariant();
    private static string? Arg(IReadOnlyDictionary<string, string?> a, string k) => a.TryGetValue(k, out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null;
    private static ActionFieldDto F(string key, string label, string? value, string type = "text", bool required = false, List<ActionCandidateDto>? options = null)
        => new() { Key = key, Label = label, Type = type, Value = value, Required = required, Options = options ?? new() };
    private static string D(decimal v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    private static decimal Dec(IReadOnlyDictionary<string, string?> v, string k, string label, bool required = true)
    {
        var s = v.TryGetValue(k, out var raw) ? raw : null;
        if (string.IsNullOrWhiteSpace(s)) { if (required) throw new ValidationAppException($"اكتب {label}."); return 0; }
        if (!decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) || d < 0 || d > 1_000_000_000m) throw new ValidationAppException($"{label} غير صحيح.");
        return d;
    }
    private static string Req(IReadOnlyDictionary<string, string?> v, string k, string label, int max)
    {
        var s = v.TryGetValue(k, out var raw) ? raw?.Trim() : null;
        if (string.IsNullOrEmpty(s)) throw new ValidationAppException($"اكتب {label}.");
        if (s.Length > max) throw new ValidationAppException($"{label} أطول من المسموح.");
        return s;
    }
    private static string? Opt(IReadOnlyDictionary<string, string?> v, string k, int max)
    {
        var s = v.TryGetValue(k, out var raw) ? raw?.Trim() : null;
        if (string.IsNullOrEmpty(s)) return null;
        if (s.Length > max) throw new ValidationAppException("في بيان أطول من المسموح.");
        return s;
    }
    private static Guid Id(IReadOnlyDictionary<string, string?> v, string k, string label)
        => v.TryGetValue(k, out var raw) && Guid.TryParse(raw, out var g) ? g : throw new ValidationAppException($"اختار {label}.");
    private void Require(params string[] roles)
    {
        if (!roles.Any(r => _user.Roles.Contains(r, StringComparer.OrdinalIgnoreCase))) throw new ValidationAppException("مفيش عندك صلاحية لتنفيذ ده.");
    }

    private static ActionProposalDto Draft(string formKind, string summary, List<ActionFieldDto> fields, params string[] warnings)
    {
        var p = new ActionProposalDto { Kind = AssistantActionKind.Form, FormKind = formKind, Summary = summary, Fields = fields };
        p.Warnings.AddRange(warnings.Where(w => !string.IsNullOrEmpty(w)));
        return p;
    }

    // ------------------------------------------------------------------ prepare

    public async Task<ActionProposalDto> PrepareAsync(string formKind, IReadOnlyDictionary<string, string?> a, CancellationToken ct = default)
        => formKind switch
        {
            "update_customer" => await UpdateCustomerAsync(a, ct),
            "update_vendor" => await UpdateVendorAsync(a, ct),
            "new_item" => await NewItemAsync(a, ct),
            "update_item" => await UpdateItemAsync(a, ct),
            "decide_expense" => await DecideExpenseAsync(a, ct),
            "decide_request" => await DecideRequestAsync(a, ct),
            "credit_note" => await CreditNoteAsync(a, ct),
            "stock_transfer" => await StockTransferAsync(a, ct),
            "waste_entry" => await WasteEntryAsync(a, ct),
            _ => throw new ValidationAppException("الطلب ده مش مدعوم."),
        };

    private async Task<ActionProposalDto> UpdateCustomerAsync(IReadOnlyDictionary<string, string?> a, CancellationToken ct)
    {
        Require(Finance);
        var q = Norm(Arg(a, "customer"));
        var hits = (await _c.Customers.AsNoTracking().Where(x => !x.IsDeleted).ToListAsync(ct)).Where(x => q.Length > 0 && (Norm(x.NameAr).Contains(q) || Norm(x.NameEn).Contains(q))).ToList();
        var exact = hits.Where(x => Norm(x.NameAr) == q || Norm(x.NameEn) == q).ToList();
        if (exact.Count == 1) hits = exact;
        if (hits.Count != 1)
            return Draft("update_customer", "تعديل بيانات عميل.", new() { F("target", "العميل", null, "select", true, hits.Take(MaxOptions).Select(x => new ActionCandidateDto(x.Id, x.NameAr, x.Phone)).ToList()) },
                hits.Count == 0 ? "مفيش عميل بالاسم ده." : "في أكتر من عميل بنفس الاسم. اسأل المستخدم عن العميل بالظبط وجرّب تاني.");
        var x1 = hits[0];
        string? Pick(string key, string? current) => Arg(a, key) ?? current;
        return Draft("update_customer", $"تعديل بيانات العميل {x1.NameAr}.", new()
        {
            F("target", "العميل", x1.Id.ToString(), "hidden"),
            F("nameAr", "الاسم", Pick("name", x1.NameAr), "text", true), F("nameEn", "الاسم بالإنجليزي", Pick("name_en", x1.NameEn)),
            F("taxNumber", "الرقم الضريبي", Pick("tax_number", x1.TaxRegistrationNumber)), F("commercialRegister", "السجل التجاري", Pick("commercial_register", x1.CommercialRegisterNumber)),
            F("address", "العنوان", Pick("address", x1.Address)), F("city", "المدينة", Pick("city", x1.City)),
            F("phone", "التليفون", Pick("phone", x1.Phone)), F("mobile", "الموبايل", Pick("mobile", x1.Mobile)), F("email", "الإيميل", Pick("email", x1.Email)),
            F("contactPerson", "الشخص المسؤول", Pick("contact_person", x1.ContactPerson)),
            F("termsDays", "أيام الائتمان", Arg(a, "payment_terms_days") ?? x1.PaymentTermsDays.ToString(), "number"), F("creditLimit", "حد الائتمان", Arg(a, "credit_limit") ?? D(x1.CreditLimit), "number"),
        });
    }

    private async Task<ActionProposalDto> UpdateVendorAsync(IReadOnlyDictionary<string, string?> a, CancellationToken ct)
    {
        Require(Finance);
        var q = Norm(Arg(a, "vendor"));
        var hits = (await _c.Vendors.AsNoTracking().Where(x => !x.IsDeleted).ToListAsync(ct)).Where(x => q.Length > 0 && (Norm(x.NameAr).Contains(q) || Norm(x.NameEn).Contains(q))).ToList();
        var exact = hits.Where(x => Norm(x.NameAr) == q || Norm(x.NameEn) == q).ToList();
        if (exact.Count == 1) hits = exact;
        if (hits.Count != 1)
            return Draft("update_vendor", "تعديل بيانات مورد.", new() { F("target", "المورد", null, "select", true, hits.Take(MaxOptions).Select(x => new ActionCandidateDto(x.Id, x.NameAr, x.Phone)).ToList()) },
                hits.Count == 0 ? "مفيش مورد بالاسم ده." : "في أكتر من مورد بنفس الاسم. اسأل المستخدم عن المورد بالظبط وجرّب تاني.");
        var x1 = hits[0];
        string? Pick(string key, string? current) => Arg(a, key) ?? current;
        return Draft("update_vendor", $"تعديل بيانات المورد {x1.NameAr}.", new()
        {
            F("target", "المورد", x1.Id.ToString(), "hidden"),
            F("nameAr", "الاسم", Pick("name", x1.NameAr), "text", true), F("nameEn", "الاسم بالإنجليزي", Pick("name_en", x1.NameEn)),
            F("taxNumber", "الرقم الضريبي", Pick("tax_number", x1.TaxRegistrationNumber)), F("commercialRegister", "السجل التجاري", Pick("commercial_register", x1.CommercialRegisterNumber)),
            F("address", "العنوان", Pick("address", x1.Address)), F("city", "المدينة", Pick("city", x1.City)),
            F("phone", "التليفون", Pick("phone", x1.Phone)), F("mobile", "الموبايل", Pick("mobile", x1.Mobile)), F("email", "الإيميل", Pick("email", x1.Email)),
            F("contactPerson", "الشخص المسؤول", Pick("contact_person", x1.ContactPerson)),
            F("termsDays", "أيام الائتمان", Arg(a, "payment_terms_days") ?? x1.PaymentTermsDays.ToString(), "number"),
            F("bankName", "البنك", Pick("bank_name", x1.BankName)), F("bankAccount", "رقم الحساب البنكي", Pick("bank_account", x1.BankAccountNumber)),
        });
    }

    private async Task<List<ActionCandidateDto>> CategoriesAsync(CancellationToken ct)
        => (await _c.ItemCategories.AsNoTracking().Where(x => !x.IsDeleted && x.IsActive).OrderBy(x => x.NameAr).Take(MaxOptions).ToListAsync(ct)).Select(x => new ActionCandidateDto(x.Id, x.NameAr, null)).ToList();

    private async Task<ActionProposalDto> NewItemAsync(IReadOnlyDictionary<string, string?> a, CancellationToken ct)
    {
        Require(Finance);
        var cats = await CategoriesAsync(ct);
        var q = Norm(Arg(a, "category"));
        var cat = q.Length == 0 ? null : cats.Where(x => Norm(x.Name).Contains(q)).ToList() is { Count: 1 } one ? one[0] : null;
        var name = Arg(a, "name");
        var similar = name is null ? new List<string>() : (await _c.Items.AsNoTracking().Where(i => !i.IsDeleted).ToListAsync(ct)).Where(i => Norm(i.NameAr).Contains(Norm(name)) || Norm(i.NameEn).Contains(Norm(name))).Take(5).Select(i => i.NameAr).ToList();
        return Draft("new_item", name is null ? "صنف جديد." : $"صنف جديد: {name}.", new()
        {
            F("nameAr", "الاسم", name, "text", true), F("nameEn", "الاسم بالإنجليزي", Arg(a, "name_en")), F("unit", "وحدة القياس", Arg(a, "unit"), "text", true),
            F("category", "الفئة", cat?.Id.ToString(), "select", true, cats), F("reorderLevel", "حد إعادة الطلب", Arg(a, "reorder_level") ?? "0", "number"),
            F("leadTimeDays", "أيام التوريد", Arg(a, "lead_time_days") ?? "2", "number"),
        }, similar.Count > 0 ? "في أصناف قريبة بنفس الاسم: " + string.Join("، ", similar) : string.Empty, cat is null ? "اختار الفئة." : string.Empty);
    }

    private async Task<ActionProposalDto> UpdateItemAsync(IReadOnlyDictionary<string, string?> a, CancellationToken ct)
    {
        Require(Finance);
        var q = Norm(Arg(a, "item"));
        var hits = (await _c.Items.AsNoTracking().Where(x => !x.IsDeleted).ToListAsync(ct)).Where(x => q.Length > 0 && (Norm(x.NameAr).Contains(q) || Norm(x.NameEn).Contains(q) || Norm(x.Code) == q)).ToList();
        var exact = hits.Where(x => Norm(x.NameAr) == q || Norm(x.NameEn) == q || Norm(x.Code) == q).ToList();
        if (exact.Count == 1) hits = exact;
        if (hits.Count != 1)
            return Draft("update_item", "تعديل صنف.", new() { F("target", "الصنف", null, "select", true, hits.Take(MaxOptions).Select(x => new ActionCandidateDto(x.Id, x.NameAr, x.Code)).ToList()) },
                hits.Count == 0 ? "مفيش صنف بالاسم ده." : "في أكتر من صنف بنفس الاسم. اسأل المستخدم عن الصنف بالظبط وجرّب تاني.");
        var i = hits[0];
        return Draft("update_item", $"تعديل الصنف {i.NameAr}.", new()
        {
            F("target", "الصنف", i.Id.ToString(), "hidden"),
            F("nameAr", "الاسم", Arg(a, "name") ?? i.NameAr, "text", true), F("nameEn", "الاسم بالإنجليزي", Arg(a, "name_en") ?? i.NameEn),
            F("unit", "وحدة القياس", Arg(a, "unit") ?? i.UnitOfMeasure, "text", true),
            F("category", "الفئة", i.ItemCategoryId.ToString(), "select", true, await CategoriesAsync(ct)),
            F("reorderLevel", "حد إعادة الطلب", Arg(a, "reorder_level") ?? D(i.ReorderLevel), "number"), F("leadTimeDays", "أيام التوريد", Arg(a, "lead_time_days") ?? i.LeadTimeDays.ToString(), "number"),
        }, "سعر الصنف في المنيو بيتغيّر بأداة تغيير سعر المنيو، مش من هنا.");
    }

    private async Task<ActionProposalDto> DecideExpenseAsync(IReadOnlyDictionary<string, string?> a, CancellationToken ct)
    {
        Require("Admin");
        var pending = await _c.ExpenseCaptures.AsNoTracking().Include(x => x.SuggestedAccount).Where(x => !x.IsDeleted && x.Status == ExpenseCaptureStatus.PendingApproval).OrderBy(x => x.CreatedAtUtc).ToListAsync(ct);
        var q = Norm(Arg(a, "search"));
        var hits = q.Length == 0 ? pending : pending.Where(x => Norm(x.Description).Contains(q) || (x.Amount?.ToString(CultureInfo.InvariantCulture) ?? "") == q).ToList();
        var decision = Arg(a, "decision") == "reject" ? "reject" : "approve";
        var options = hits.Take(MaxOptions).Select(x => new ActionCandidateDto(x.Id, $"{x.Description} - {D(x.Amount ?? 0)} {x.Currency}", $"{x.EntryDate:yyyy-MM-dd} {x.SuggestedAccount?.NameAr}")).ToList();
        var decisionOptions = new List<ActionCandidateDto> { new(Guid.Empty, "اعتماد", "approve"), new(Guid.Empty, "رفض", "reject") };
        return Draft("decide_expense", hits.Count == 0 ? "مفيش مصروف منتظر اعتماد." : $"قرار على مصروف منتظر ({hits.Count}).", new()
        {
            F("target", "المصروف", hits.Count == 1 ? hits[0].Id.ToString() : null, "select", true, options),
            F("decision", "القرار", decision, "select", true, decisionOptions),
        }, hits.Count == 0 ? "مفيش مصروف منتظر اعتماد بالوصف ده." : string.Empty, "الاعتماد بيرحّل المصروف للدفاتر. راجع المبلغ والحساب قبل ما تنفّذ.");
    }

    private async Task<ActionProposalDto> DecideRequestAsync(IReadOnlyDictionary<string, string?> a, CancellationToken ct)
    {
        Require("Admin", "HR");
        var pending = await _requests.GetPendingAsync(ct);
        var q = Norm(Arg(a, "employee"));
        var hits = q.Length == 0 ? pending : pending.Where(x => Norm(x.EmployeeName).Contains(q)).ToList();
        var decision = Arg(a, "decision") == "reject" ? "reject" : "approve";
        return Draft("decide_request", hits.Count == 0 ? "مفيش طلب منتظر." : $"قرار على طلب موظف ({hits.Count}).", new()
        {
            F("target", "الطلب", hits.Count == 1 ? hits[0].Id.ToString() : null, "select", true, hits.Take(MaxOptions).Select(x => new ActionCandidateDto(x.Id, $"{x.EmployeeName} - {x.Type}", $"{x.DateFrom:yyyy-MM-dd}{(x.DateTo is null ? "" : " إلى " + x.DateTo.Value.ToString("yyyy-MM-dd"))} {x.Reason}")).ToList()),
            F("decision", "القرار", decision, "select", true, new() { new(Guid.Empty, "موافقة", "approve"), new(Guid.Empty, "رفض", "reject") }),
            F("note", "ملاحظة (اختياري)", Arg(a, "note"), "text"),
        }, hits.Count == 0 ? "مفيش طلب منتظر بالاسم ده." : string.Empty);
    }

    private async Task<ActionProposalDto> CreditNoteAsync(IReadOnlyDictionary<string, string?> a, CancellationToken ct)
    {
        Require(Finance);
        var q = Norm(Arg(a, "customer")); var num = Arg(a, "invoice_number");
        var invoices = _c.SalesInvoices.AsNoTracking().Include(x => x.Customer).Where(x => !x.IsDeleted);
        List<RomaERP.Domain.Sales.SalesInvoice> hits;
        if (num is not null) hits = await invoices.Where(x => x.InvoiceNumber.Contains(num)).OrderByDescending(x => x.InvoiceDate).Take(MaxOptions).ToListAsync(ct);
        else hits = (await invoices.OrderByDescending(x => x.InvoiceDate).Take(300).ToListAsync(ct)).Where(x => q.Length > 0 && x.Customer is not null && (Norm(x.Customer.NameAr).Contains(q) || Norm(x.Customer.NameEn).Contains(q))).Take(MaxOptions).ToList();
        return Draft("credit_note", "إشعار دائن (مرتجع أو تخفيض) على فاتورة مبيعات.", new()
        {
            F("invoice", "الفاتورة", hits.Count == 1 ? hits[0].Id.ToString() : null, "select", true, hits.Select(x => new ActionCandidateDto(x.Id, $"{x.InvoiceNumber} - {x.Customer?.NameAr}", $"{D(x.TotalAmount)} {x.InvoiceDate:yyyy-MM-dd}")).ToList()),
            F("reason", "سبب الإشعار", Arg(a, "reason"), "text", true), F("description", "وصف البند", Arg(a, "description") ?? Arg(a, "reason"), "text", true),
            F("quantity", "الكمية", Arg(a, "quantity") ?? "1", "number", true), F("unitPrice", "سعر الوحدة", Arg(a, "unit_price"), "number", true),
        }, hits.Count == 0 ? "مفيش فاتورة مطابقة." : string.Empty, "الإشعار مالي بس: مش بيرجّع بضاعة للمخزون.");
    }

    private async Task<List<ActionCandidateDto>> WarehousesAsync(CancellationToken ct)
        => (await _c.Warehouses.AsNoTracking().Where(x => !x.IsDeleted && x.IsActive).OrderBy(x => x.NameAr).Take(MaxOptions).ToListAsync(ct)).Select(x => new ActionCandidateDto(x.Id, x.NameAr, x.Code)).ToList();

    private static string? MatchOne(List<ActionCandidateDto> list, string? query)
    {
        var q = Norm(query);
        if (q.Length == 0) return null;
        var hit = list.Where(x => Norm(x.Name).Contains(q) || Norm(x.Extra) == q).ToList();
        return hit.Count == 1 ? hit[0].Id.ToString() : null;
    }

    private async Task<List<ActionCandidateDto>> ItemMatchesAsync(string? query, CancellationToken ct)
    {
        var q = Norm(query);
        return (await _c.Items.AsNoTracking().Where(x => !x.IsDeleted && x.IsActive).ToListAsync(ct)).Where(x => q.Length > 0 && (Norm(x.NameAr).Contains(q) || Norm(x.NameEn).Contains(q) || Norm(x.Code) == q))
            .Take(MaxOptions).Select(x => new ActionCandidateDto(x.Id, x.NameAr, $"{D(x.QuantityOnHand)} {x.UnitOfMeasure}")).ToList();
    }

    private async Task<ActionProposalDto> StockTransferAsync(IReadOnlyDictionary<string, string?> a, CancellationToken ct)
    {
        Require(Finance);
        var wh = await WarehousesAsync(ct); var items = await ItemMatchesAsync(Arg(a, "item"), ct);
        return Draft("stock_transfer", "تحويل مخزون بين فرعين.", new()
        {
            F("item", "الصنف", items.Count == 1 ? items[0].Id.ToString() : null, "select", true, items),
            F("quantity", "الكمية", Arg(a, "quantity"), "number", true),
            F("from", "من فرع", MatchOne(wh, Arg(a, "from")), "select", true, wh), F("to", "إلى فرع", MatchOne(wh, Arg(a, "to")), "select", true, wh),
            F("notes", "ملاحظة", Arg(a, "notes")),
        }, items.Count == 0 ? "مفيش صنف مطابق." : string.Empty, "التحويل بيخصم من الفرع الأول فوراً، والفرع التاني بيستلمه من صفحة التحويلات.");
    }

    private async Task<ActionProposalDto> WasteEntryAsync(IReadOnlyDictionary<string, string?> a, CancellationToken ct)
    {
        Require(Finance);
        var wh = await WarehousesAsync(ct); var items = await ItemMatchesAsync(Arg(a, "item"), ct);
        var reasons = Enum.GetValues<RomaERP.Domain.Inventory.WasteReason>().Select(r => new ActionCandidateDto(Guid.Empty, r switch
        {
            RomaERP.Domain.Inventory.WasteReason.Waste => "هالك", RomaERP.Domain.Inventory.WasteReason.Expired => "منتهي الصلاحية", RomaERP.Domain.Inventory.WasteReason.Damaged => "تالف",
            RomaERP.Domain.Inventory.WasteReason.ProductionWaste => "هالك تصنيع", RomaERP.Domain.Inventory.WasteReason.OverPortion => "زيادة في الحصة", _ => "غير معروف",
        }, ((int)r).ToString())).ToList();
        var reason = Arg(a, "reason");
        var reasonValue = reasons.FirstOrDefault(r => Norm(r.Name) == Norm(reason) || r.Extra == reason)?.Extra ?? "1";
        return Draft("waste_entry", "تسجيل هالك مخزون.", new()
        {
            F("item", "الصنف", items.Count == 1 ? items[0].Id.ToString() : null, "select", true, items),
            F("warehouse", "الفرع", MatchOne(wh, Arg(a, "warehouse")), "select", true, wh),
            F("quantity", "الكمية", Arg(a, "quantity"), "number", true), F("reason", "السبب", reasonValue, "select", true, reasons), F("notes", "ملاحظة", Arg(a, "notes")),
        }, items.Count == 0 ? "مفيش صنف مطابق." : string.Empty, "الهالك بيخصم من المخزون ويتسجل خسارة في الدفاتر.");
    }

    // ------------------------------------------------------------------ execute

    public async Task<ExecuteActionResultDto> ExecuteAsync(string formKind, IReadOnlyDictionary<string, string?> v, CancellationToken ct = default)
    {
        switch (formKind)
        {
            case "update_customer":
            {
                Require(Finance);
                var id = Id(v, "target", "العميل");
                var cur = await _c.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct) ?? throw new NotFoundException(nameof(RomaERP.Domain.Sales.Customer), id);
                var name = Req(v, "nameAr", "الاسم", 150);
                await _sales.UpdateCustomerAsync(id, new UpdateCustomerDto
                {
                    NameAr = name, NameEn = Opt(v, "nameEn", 150) ?? name, Phone = Opt(v, "phone", 40), Email = Opt(v, "email", 120), TaxRegistrationNumber = Opt(v, "taxNumber", 40), IsActive = cur.IsActive,
                    ContactPerson = Opt(v, "contactPerson", 100), Mobile = Opt(v, "mobile", 40), Address = Opt(v, "address", 250), City = Opt(v, "city", 80), CommercialRegisterNumber = Opt(v, "commercialRegister", 40),
                    Notes = cur.Notes, PaymentTermsDays = (int)Math.Min(365, Dec(v, "termsDays", "أيام الائتمان", false)), CreditLimit = Dec(v, "creditLimit", "حد الائتمان", false),
                }, ct);
                return new($"اتحدّثت بيانات العميل {name}.", "/sales/customers");
            }
            case "update_vendor":
            {
                Require(Finance);
                var id = Id(v, "target", "المورد");
                var cur = await _c.Vendors.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct) ?? throw new NotFoundException(nameof(RomaERP.Domain.Purchasing.Vendor), id);
                var name = Req(v, "nameAr", "الاسم", 150);
                await _purchasing.UpdateVendorAsync(id, new UpdateVendorDto
                {
                    NameAr = name, NameEn = Opt(v, "nameEn", 150) ?? name, Phone = Opt(v, "phone", 40), Email = Opt(v, "email", 120), TaxRegistrationNumber = Opt(v, "taxNumber", 40), IsActive = cur.IsActive,
                    ContactPerson = Opt(v, "contactPerson", 100), Mobile = Opt(v, "mobile", 40), Address = Opt(v, "address", 250), City = Opt(v, "city", 80), CommercialRegisterNumber = Opt(v, "commercialRegister", 40),
                    Notes = cur.Notes, PaymentTermsDays = (int)Math.Min(365, Dec(v, "termsDays", "أيام الائتمان", false)), CreditLimit = cur.CreditLimit,
                    BankName = Opt(v, "bankName", 100), BankAccountNumber = Opt(v, "bankAccount", 60),
                }, ct);
                return new($"اتحدّثت بيانات المورد {name}.", "/purchasing/vendors");
            }
            case "new_item":
            {
                Require(Finance);
                var name = Req(v, "nameAr", "الاسم", 150);
                var cat = Id(v, "category", "الفئة");
                var codes = await _c.Items.AsNoTracking().Select(i => i.Code).ToListAsync(ct);
                var set = new HashSet<string>(codes, StringComparer.OrdinalIgnoreCase);
                var code = ""; for (var n = codes.Count + 1; ; n++) { code = $"I-{n:D4}"; if (!set.Contains(code)) break; }
                await _items.CreateAsync(new CreateItemDto
                {
                    Code = code, NameAr = name, NameEn = Opt(v, "nameEn", 150) ?? name, UnitOfMeasure = Req(v, "unit", "وحدة القياس", 30), ItemCategoryId = cat,
                    ReorderLevel = Dec(v, "reorderLevel", "حد إعادة الطلب", false), LeadTimeDays = (int)Math.Min(365, Dec(v, "leadTimeDays", "أيام التوريد", false)),
                }, ct);
                return new($"اتضاف الصنف {name} بكود {code}.", "/inventory/items");
            }
            case "update_item":
            {
                Require(Finance);
                var id = Id(v, "target", "الصنف");
                var cur = await _c.Items.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct) ?? throw new NotFoundException(nameof(RomaERP.Domain.Inventory.Item), id);
                var name = Req(v, "nameAr", "الاسم", 150);
                await _items.UpdateAsync(id, new UpdateItemDto
                {
                    NameAr = name, NameEn = Opt(v, "nameEn", 150) ?? name, UnitOfMeasure = Req(v, "unit", "وحدة القياس", 30), ItemCategoryId = Id(v, "category", "الفئة"),
                    ReorderLevel = Dec(v, "reorderLevel", "حد إعادة الطلب", false), LeadTimeDays = (int)Math.Min(365, Dec(v, "leadTimeDays", "أيام التوريد", false)), IsLotTracked = cur.IsLotTracked,
                }, ct);
                return new($"اتحدّث الصنف {name}.", "/inventory/items");
            }
            case "decide_expense":
            {
                Require("Admin");
                var id = Id(v, "target", "المصروف");
                if (v.TryGetValue("decision", out var d) && d == "reject") { await _expenses.RejectAsync(id, ct); return new("اترفض المصروف.", "/assistant/approvals"); }
                await _expenses.ApproveAsync(id, ct);
                return new("اتعتمد المصروف واترحّل للدفاتر.", "/assistant/approvals");
            }
            case "decide_request":
            {
                Require("Admin", "HR");
                var id = Id(v, "target", "الطلب");
                var by = Guid.TryParse(_user.UserId, out var g) ? g : throw new ValidationAppException("مش قادر أحدد المستخدم.");
                var approve = !(v.TryGetValue("decision", out var d) && d == "reject");
                await _requests.DecideAsync(id, by, new DecideEmployeeRequestDto { Approve = approve, DecisionNote = Opt(v, "note", 300) }, ct);
                return new(approve ? "اتوافق على الطلب." : "اترفض الطلب.", "/hr/employee-requests");
            }
            case "credit_note":
            {
                Require(Finance);
                var id = Id(v, "invoice", "الفاتورة");
                var qty = Dec(v, "quantity", "الكمية"); var price = Dec(v, "unitPrice", "سعر الوحدة");
                if (qty <= 0 || price <= 0) throw new ValidationAppException("الكمية والسعر لازم يكونوا أكبر من صفر.");
                var today = DateTime.UtcNow.Date;
                var period = await _c.FiscalPeriods.AsNoTracking().FirstOrDefaultAsync(x => x.StartDate <= today && x.EndDate >= today && !x.IsClosed, ct) ?? throw new ValidationAppException("مفيش فترة محاسبية مفتوحة لتاريخ النهارده.");
                var note = await _sales.CreateNoteAsync(new CreateSalesNoteDto
                {
                    OriginalInvoiceId = id, NoteType = RomaERP.Domain.Sales.SalesNoteType.Credit, NoteDate = today, FiscalPeriodId = period.Id, Reason = Req(v, "reason", "سبب الإشعار", 200),
                    Notes = "أُنشئ بمساعدة المساعد الذكي", Lines = { new SalesNoteLineInputDto { Description = Req(v, "description", "وصف البند", 200), Quantity = qty, UnitPrice = price } },
                }, ct);
                return new("اتعمل إشعار دائن على الفاتورة.", "/sales/notes");
            }
            case "stock_transfer":
            {
                Require(Finance);
                var qty = Dec(v, "quantity", "الكمية"); if (qty <= 0) throw new ValidationAppException("الكمية لازم تكون أكبر من صفر.");
                var from = Id(v, "from", "الفرع المحوِّل"); var to = Id(v, "to", "الفرع المستلم");
                if (from == to) throw new ValidationAppException("الفرعين لازم يكونوا مختلفين.");
                await _transfers.SendAsync(new CreateStockTransferDto
                {
                    FromWarehouseId = from, ToWarehouseId = to, TransferDate = DateTime.UtcNow.Date, Notes = Opt(v, "notes", 300),
                    Lines = { new CreateStockTransferLineDto { ItemId = Id(v, "item", "الصنف"), Quantity = qty } },
                }, _user.UserName, ct);
                return new("اتبعت التحويل، والفرع المستلم يستلمه من صفحة التحويلات.", "/inventory/transfers");
            }
            case "waste_entry":
            {
                Require(Finance);
                var qty = Dec(v, "quantity", "الكمية"); if (qty <= 0) throw new ValidationAppException("الكمية لازم تكون أكبر من صفر.");
                var today = DateTime.UtcNow.Date;
                var period = await _c.FiscalPeriods.AsNoTracking().FirstOrDefaultAsync(x => x.StartDate <= today && x.EndDate >= today && !x.IsClosed, ct) ?? throw new ValidationAppException("مفيش فترة محاسبية مفتوحة لتاريخ النهارده.");
                if (!int.TryParse(v.TryGetValue("reason", out var r) ? r : null, out var reason) || !Enum.IsDefined(typeof(RomaERP.Domain.Inventory.WasteReason), reason)) throw new ValidationAppException("اختار السبب.");
                await _waste.CreateAsync(new CreateWasteEntryDto
                {
                    ItemId = Id(v, "item", "الصنف"), WarehouseId = Id(v, "warehouse", "الفرع"), FiscalPeriodId = period.Id, WasteDate = today, Quantity = qty, Reason = reason, Notes = Opt(v, "notes", 300),
                }, ct);
                return new("اتسجل الهالك وخُصم من المخزون.", "/inventory/waste");
            }
            default:
                throw new ValidationAppException("الطلب ده مش قابل للتنفيذ.");
        }
    }
}
