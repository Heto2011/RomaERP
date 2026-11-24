using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting.Services;
using RomaERP.Application.Assistant.DTOs;
using RomaERP.Application.Assistant.Services;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.Purchasing.Services;
using RomaERP.Application.Sales.Services;

namespace RomaERP.Application.Assistant.Agent;

internal static class ToolArgs
{
    public static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };
    public static string Str(JsonObject a, string k) => a[k] is { } n && n.GetValueKind() == JsonValueKind.String ? n.GetValue<string>().Trim() : string.Empty;
    public static bool Flag(JsonObject a, string k) => a[k] is { } n && n.GetValueKind() == JsonValueKind.True;
    public static DateTime Date(JsonObject a, string k, DateTime fallback)
    {
        var s = Str(a, k);
        if (s.Length == 0) return fallback;
        if (!DateTime.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) throw new ValidationAppException($"التاريخ \"{s}\" لازم يكون بصيغة yyyy-MM-dd.");
        return d.Date;
    }
    public static string Ser(object o) => JsonSerializer.Serialize(o, Json);
    public static string Norm(string? s) => (s ?? string.Empty).Trim().ToLowerInvariant();
    public static JsonObject Schema(Action<JsonObject> props, params string[] required)
    {
        var p = new JsonObject(); props(p);
        return new JsonObject { ["type"] = "object", ["properties"] = p, ["required"] = new JsonArray(required.Select(r => (JsonNode)r).ToArray()) };
    }
    public static JsonObject S(string desc) => new() { ["type"] = "string", ["description"] = desc };
    public static JsonObject B(string desc) => new() { ["type"] = "boolean", ["description"] = desc };
    public static JsonObject N(string desc) => new() { ["type"] = "number", ["description"] = desc };
    public static readonly string Today = "(التاريخ بصيغة yyyy-MM-dd)";
}

public abstract class ReadTool : IAgentTool
{
    public abstract string Name { get; }
    public abstract string Description { get; }
    public virtual JsonObject InputSchema => ToolArgs.Schema(_ => { });
    public abstract Task<AgentToolResult> RunAsync(JsonObject args, CancellationToken ct);
}

public class SearchCustomersTool : ReadTool
{
    private readonly IApplicationDbContext _c;
    public SearchCustomersTool(IApplicationDbContext c) => _c = c;
    public override string Name => "search_customers";
    public override string Description => "Find customers by part of their name or phone, with what each one currently owes. Returns up to 10.";
    public override JsonObject InputSchema => ToolArgs.Schema(p => p["query"] = ToolArgs.S("part of the name or phone"), "query");
    public override async Task<AgentToolResult> RunAsync(JsonObject a, CancellationToken ct)
    {
        var q = ToolArgs.Norm(ToolArgs.Str(a, "query"));
        var customers = (await _c.Customers.AsNoTracking().Where(x => !x.IsDeleted && x.IsActive).ToListAsync(ct))
            .Where(x => q.Length == 0 || ToolArgs.Norm(x.NameAr).Contains(q) || ToolArgs.Norm(x.NameEn).Contains(q) || (x.Phone ?? "").Contains(q) || (x.Mobile ?? "").Contains(q)).Take(10).ToList();
        var ids = customers.Select(x => x.Id).ToList();
        var owed = await _c.SalesInvoices.AsNoTracking().Where(i => !i.IsDeleted && ids.Contains(i.CustomerId) && i.PaidAmount < i.TotalAmount)
            .GroupBy(i => i.CustomerId).Select(g => new { g.Key, Amount = g.Sum(i => i.TotalAmount - i.PaidAmount) }).ToListAsync(ct);
        return new(ToolArgs.Ser(customers.Select(x => new { x.Id, name = x.NameAr, x.Code, phone = x.Phone ?? x.Mobile, owes = owed.FirstOrDefault(o => o.Key == x.Id)?.Amount ?? 0 })));
    }
}

public class SearchVendorsTool : ReadTool
{
    private readonly IApplicationDbContext _c;
    public SearchVendorsTool(IApplicationDbContext c) => _c = c;
    public override string Name => "search_vendors";
    public override string Description => "Find suppliers by part of their name, with what the company currently owes each. Returns up to 10.";
    public override JsonObject InputSchema => ToolArgs.Schema(p => p["query"] = ToolArgs.S("part of the name"), "query");
    public override async Task<AgentToolResult> RunAsync(JsonObject a, CancellationToken ct)
    {
        var q = ToolArgs.Norm(ToolArgs.Str(a, "query"));
        var vendors = (await _c.Vendors.AsNoTracking().Where(x => !x.IsDeleted && x.IsActive).ToListAsync(ct))
            .Where(x => q.Length == 0 || ToolArgs.Norm(x.NameAr).Contains(q) || ToolArgs.Norm(x.NameEn).Contains(q)).Take(10).ToList();
        var ids = vendors.Select(x => x.Id).ToList();
        var owed = await _c.PurchaseInvoices.AsNoTracking().Where(i => !i.IsDeleted && ids.Contains(i.VendorId) && i.PaidAmount < i.TotalAmount)
            .GroupBy(i => i.VendorId).Select(g => new { g.Key, Amount = g.Sum(i => i.TotalAmount - i.PaidAmount) }).ToListAsync(ct);
        return new(ToolArgs.Ser(vendors.Select(x => new { x.Id, name = x.NameAr, x.Code, phone = x.Phone, weOwe = owed.FirstOrDefault(o => o.Key == x.Id)?.Amount ?? 0 })));
    }
}

public class SearchItemsTool : ReadTool
{
    private readonly IApplicationDbContext _c;
    public SearchItemsTool(IApplicationDbContext c) => _c = c;
    public override string Name => "search_items";
    public override string Description => "Find stock or menu items by part of the name or code: quantity on hand, reorder level, menu price. Returns up to 10. Set low_stock_only to list items at or below their reorder level.";
    public override JsonObject InputSchema => ToolArgs.Schema(p => { p["query"] = ToolArgs.S("part of the name or code (may be empty)"); p["low_stock_only"] = ToolArgs.B("only items at or below the reorder level"); });
    public override async Task<AgentToolResult> RunAsync(JsonObject a, CancellationToken ct)
    {
        var q = ToolArgs.Norm(ToolArgs.Str(a, "query"));
        var low = ToolArgs.Flag(a, "low_stock_only");
        var items = (await _c.Items.AsNoTracking().Where(x => !x.IsDeleted && x.IsActive).ToListAsync(ct))
            .Where(x => (q.Length == 0 || ToolArgs.Norm(x.NameAr).Contains(q) || ToolArgs.Norm(x.NameEn).Contains(q) || ToolArgs.Norm(x.Code).Contains(q)) && (!low || x.QuantityOnHand <= x.ReorderLevel))
            .Take(10).Select(x => new { x.Id, name = x.NameAr, x.Code, onHand = x.QuantityOnHand, reorderLevel = x.ReorderLevel, unit = x.UnitOfMeasure, menuPrice = x.IsMenuItem ? x.MenuPrice : (decimal?)null });
        return new(ToolArgs.Ser(items));
    }
}

public class ListSalesInvoicesTool : ReadTool
{
    private readonly IApplicationDbContext _c;
    public ListSalesInvoicesTool(IApplicationDbContext c) => _c = c;
    public override string Name => "list_sales_invoices";
    public override string Description => "List recent sales invoices, newest first (up to 15), optionally for one customer name, only unpaid ones, or a date range. Also returns the total of what was found.";
    public override JsonObject InputSchema => ToolArgs.Schema(p =>
    {
        p["customer"] = ToolArgs.S("part of the customer name"); p["unpaid_only"] = ToolArgs.B("only invoices not fully paid");
        p["from"] = ToolArgs.S("start date yyyy-MM-dd"); p["to"] = ToolArgs.S("end date yyyy-MM-dd");
    });
    public override async Task<AgentToolResult> RunAsync(JsonObject a, CancellationToken ct)
    {
        var from = ToolArgs.Date(a, "from", DateTime.MinValue); var to = ToolArgs.Date(a, "to", DateTime.MaxValue);
        var q = ToolArgs.Norm(ToolArgs.Str(a, "customer")); var unpaid = ToolArgs.Flag(a, "unpaid_only");
        var rows = (await _c.SalesInvoices.AsNoTracking().Include(i => i.Customer).Where(i => !i.IsDeleted && i.InvoiceDate >= from && i.InvoiceDate <= to && (!unpaid || i.PaidAmount < i.TotalAmount))
            .OrderByDescending(i => i.InvoiceDate).Take(300).ToListAsync(ct))
            .Where(i => q.Length == 0 || (i.Customer is not null && (ToolArgs.Norm(i.Customer.NameAr).Contains(q) || ToolArgs.Norm(i.Customer.NameEn).Contains(q)))).ToList();
        return new(ToolArgs.Ser(new
        {
            count = rows.Count, total = rows.Sum(i => i.TotalAmount), outstanding = rows.Sum(i => i.TotalAmount - i.PaidAmount),
            invoices = rows.Take(15).Select(i => new { number = i.InvoiceNumber, date = i.InvoiceDate.ToString("yyyy-MM-dd"), customer = i.Customer?.NameAr, i.TotalAmount, outstanding = i.TotalAmount - i.PaidAmount }),
        }));
    }
}

public class ListPurchaseInvoicesTool : ReadTool
{
    private readonly IApplicationDbContext _c;
    public ListPurchaseInvoicesTool(IApplicationDbContext c) => _c = c;
    public override string Name => "list_purchase_invoices";
    public override string Description => "List recent purchase invoices, newest first (up to 15), optionally for one supplier name, only unpaid ones, or a date range. Also returns the total of what was found.";
    public override JsonObject InputSchema => ToolArgs.Schema(p =>
    {
        p["vendor"] = ToolArgs.S("part of the supplier name"); p["unpaid_only"] = ToolArgs.B("only invoices not fully paid");
        p["from"] = ToolArgs.S("start date yyyy-MM-dd"); p["to"] = ToolArgs.S("end date yyyy-MM-dd");
    });
    public override async Task<AgentToolResult> RunAsync(JsonObject a, CancellationToken ct)
    {
        var from = ToolArgs.Date(a, "from", DateTime.MinValue); var to = ToolArgs.Date(a, "to", DateTime.MaxValue);
        var q = ToolArgs.Norm(ToolArgs.Str(a, "vendor")); var unpaid = ToolArgs.Flag(a, "unpaid_only");
        var rows = (await _c.PurchaseInvoices.AsNoTracking().Include(i => i.Vendor).Where(i => !i.IsDeleted && i.InvoiceDate >= from && i.InvoiceDate <= to && (!unpaid || i.PaidAmount < i.TotalAmount))
            .OrderByDescending(i => i.InvoiceDate).Take(300).ToListAsync(ct))
            .Where(i => q.Length == 0 || (i.Vendor is not null && (ToolArgs.Norm(i.Vendor.NameAr).Contains(q) || ToolArgs.Norm(i.Vendor.NameEn).Contains(q)))).ToList();
        return new(ToolArgs.Ser(new
        {
            count = rows.Count, total = rows.Sum(i => i.TotalAmount), outstanding = rows.Sum(i => i.TotalAmount - i.PaidAmount),
            invoices = rows.Take(15).Select(i => new { number = i.InvoiceNumber, date = i.InvoiceDate.ToString("yyyy-MM-dd"), vendor = i.Vendor?.NameAr, i.TotalAmount, outstanding = i.TotalAmount - i.PaidAmount }),
        }));
    }
}

public class ReceivablesTool : ReadTool
{
    private readonly ISalesService _s;
    public ReceivablesTool(ISalesService s) => _s = s;
    public override string Name => "get_receivables_aging";
    public override string Description => "What customers owe, by age (current, 31-60, 61-90, over 90 days), per customer.";
    public override async Task<AgentToolResult> RunAsync(JsonObject a, CancellationToken ct)
        => new(ToolArgs.Ser((await _s.GetArAgingAsync(null, ct)).OrderByDescending(x => x.TotalOutstanding).Take(20)));
}

public class PayablesTool : ReadTool
{
    private readonly IPurchasingService _p;
    public PayablesTool(IPurchasingService p) => _p = p;
    public override string Name => "get_payables_aging";
    public override string Description => "What the company owes suppliers, by age (current, 31-60, 61-90, over 90 days), per supplier.";
    public override async Task<AgentToolResult> RunAsync(JsonObject a, CancellationToken ct)
        => new(ToolArgs.Ser((await _p.GetApAgingAsync(null, ct)).OrderByDescending(x => x.TotalOutstanding).Take(20)));
}

public class IncomeStatementTool : ReadTool
{
    private readonly IFinancialReportService _r;
    public IncomeStatementTool(IFinancialReportService r) => _r = r;
    public override string Name => "get_income_statement";
    public override string Description => "Revenue, expenses and profit for a period.";
    public override JsonObject InputSchema => ToolArgs.Schema(p => { p["from"] = ToolArgs.S("start date yyyy-MM-dd"); p["to"] = ToolArgs.S("end date yyyy-MM-dd"); }, "from", "to");
    public override async Task<AgentToolResult> RunAsync(JsonObject a, CancellationToken ct)
        => new(ToolArgs.Ser(await _r.GetIncomeStatementAsync(ToolArgs.Date(a, "from", DateTime.UtcNow.Date.AddDays(1 - DateTime.UtcNow.Day)), ToolArgs.Date(a, "to", DateTime.UtcNow.Date), ct)));
}

public class BalanceSheetTool : ReadTool
{
    private readonly IFinancialReportService _r;
    public BalanceSheetTool(IFinancialReportService r) => _r = r;
    public override string Name => "get_balance_sheet";
    public override string Description => "Assets, liabilities and equity as of a date.";
    public override JsonObject InputSchema => ToolArgs.Schema(p => p["as_of"] = ToolArgs.S("date yyyy-MM-dd"), "as_of");
    public override async Task<AgentToolResult> RunAsync(JsonObject a, CancellationToken ct)
        => new(ToolArgs.Ser(await _r.GetBalanceSheetAsync(ToolArgs.Date(a, "as_of", DateTime.UtcNow.Date), ct)));
}

public class VatSummaryTool : ReadTool
{
    private readonly IFinancialReportService _r;
    public VatSummaryTool(IFinancialReportService r) => _r = r;
    public override string Name => "get_vat_summary";
    public override string Description => "Output VAT, input VAT and the net due for a period.";
    public override JsonObject InputSchema => ToolArgs.Schema(p => { p["from"] = ToolArgs.S("start date yyyy-MM-dd"); p["to"] = ToolArgs.S("end date yyyy-MM-dd"); }, "from", "to");
    public override async Task<AgentToolResult> RunAsync(JsonObject a, CancellationToken ct)
        => new(ToolArgs.Ser(await _r.GetVatSummaryAsync(ToolArgs.Date(a, "from", DateTime.UtcNow.Date.AddDays(1 - DateTime.UtcNow.Day)), ToolArgs.Date(a, "to", DateTime.UtcNow.Date), ct)));
}

public class CashFlowTool : ReadTool
{
    private readonly IFinancialReportService _r;
    public CashFlowTool(IFinancialReportService r) => _r = r;
    public override string Name => "get_cash_flow";
    public override string Description => "Cash in and out for a period.";
    public override JsonObject InputSchema => ToolArgs.Schema(p => { p["from"] = ToolArgs.S("start date yyyy-MM-dd"); p["to"] = ToolArgs.S("end date yyyy-MM-dd"); }, "from", "to");
    public override async Task<AgentToolResult> RunAsync(JsonObject a, CancellationToken ct)
        => new(ToolArgs.Ser(await _r.GetCashFlowStatementAsync(ToolArgs.Date(a, "from", DateTime.UtcNow.Date.AddDays(1 - DateTime.UtcNow.Day)), ToolArgs.Date(a, "to", DateTime.UtcNow.Date), ct)));
}

public class BranchProfitTool : ReadTool
{
    private readonly IFinancialReportService _r;
    public BranchProfitTool(IFinancialReportService r) => _r = r;
    public override string Name => "get_branch_profit";
    public override string Description => "Profit and loss of each branch for a period.";
    public override JsonObject InputSchema => ToolArgs.Schema(p => { p["from"] = ToolArgs.S("start date yyyy-MM-dd"); p["to"] = ToolArgs.S("end date yyyy-MM-dd"); }, "from", "to");
    public override async Task<AgentToolResult> RunAsync(JsonObject a, CancellationToken ct)
        => new(ToolArgs.Ser(await _r.GetBranchProfitAsync(ToolArgs.Date(a, "from", DateTime.UtcNow.Date.AddDays(1 - DateTime.UtcNow.Day)), ToolArgs.Date(a, "to", DateTime.UtcNow.Date), ct)));
}

/// <summary>Write tools never write. They match the names in the request to real records and hand back a draft for the owner to approve on screen.</summary>
public abstract class PrepareTool : IAgentTool
{
    protected readonly IAssistantActionService Actions;
    protected PrepareTool(IAssistantActionService actions) => Actions = actions;
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract JsonObject InputSchema { get; }
    protected abstract ActionIntent ToIntent(JsonObject a);
    public async Task<AgentToolResult> RunAsync(JsonObject a, CancellationToken ct)
    {
        var draft = await Actions.ProposeFromIntentAsync(ToIntent(a), ct);
        var note = draft.Warnings.Count > 0 ? " Needs attention: " + string.Join(" | ", draft.Warnings) : string.Empty;
        return new($"A draft was prepared and is now shown to the user for review. NOTHING was booked. Tell the user to review it and press Execute. Draft: {draft.Summary}{note}", draft);
    }
}

public class PrepareSalesInvoiceTool : PrepareTool
{
    public PrepareSalesInvoiceTool(IAssistantActionService a) : base(a) { }
    public override string Name => "prepare_sales_invoice";
    public override string Description => "Prepare (not book) a sales invoice draft for an existing customer. Do not invent prices: leave unit_price out if the user did not say it.";
    public override JsonObject InputSchema => ToolArgs.Schema(p =>
    {
        p["customer_name"] = ToolArgs.S("the customer's name as the user said it");
        p["lines"] = new JsonObject { ["type"] = "array", ["items"] = ToolArgs.Schema(l => { l["description"] = ToolArgs.S("what is sold"); l["quantity"] = ToolArgs.N("quantity"); l["unit_price"] = ToolArgs.N("price of one, only if stated"); }, "description", "quantity") };
        p["payment_term"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("cash", "card", "credit") };
        p["note"] = ToolArgs.S("optional note");
    }, "customer_name", "lines");
    protected override ActionIntent ToIntent(JsonObject a)
    {
        var intent = new ActionIntent { Kind = AssistantActionKind.SalesInvoice, CustomerName = ToolArgs.Str(a, "customer_name"), PaymentTerm = ToolArgs.Str(a, "payment_term"), Note = ToolArgs.Str(a, "note") };
        if (a["lines"] is JsonArray lines)
            foreach (var l in lines.OfType<JsonObject>())
                intent.Lines.Add(new ActionInvoiceLineDto { Description = ToolArgs.Str(l, "description"), Quantity = l["quantity"] is { } q && q.GetValueKind() == JsonValueKind.Number ? q.GetValue<decimal>() : 1, UnitPrice = l["unit_price"] is { } u && u.GetValueKind() == JsonValueKind.Number ? u.GetValue<decimal>() : null });
        return intent;
    }
}

public class PrepareCustomerPaymentTool : PrepareTool
{
    public PrepareCustomerPaymentTool(IAssistantActionService a) : base(a) { }
    public override string Name => "prepare_customer_payment";
    public override string Description => "Prepare (not book) a payment received from a customer on an open invoice. Give the invoice number or the customer's name.";
    public override JsonObject InputSchema => ToolArgs.Schema(p =>
    {
        p["invoice_number"] = ToolArgs.S("invoice number if known"); p["customer_name"] = ToolArgs.S("customer name if the invoice number is not known");
        p["amount"] = ToolArgs.N("amount paid, only if stated"); p["method"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("cash", "card") };
    });
    protected override ActionIntent ToIntent(JsonObject a) => new()
    {
        Kind = AssistantActionKind.CustomerPayment, InvoiceNumber = ToolArgs.Str(a, "invoice_number"), CustomerName = ToolArgs.Str(a, "customer_name"), Method = ToolArgs.Str(a, "method"),
        Amount = a["amount"] is { } n && n.GetValueKind() == JsonValueKind.Number ? n.GetValue<decimal>() : null,
    };
}

public class PrepareMenuPriceTool : PrepareTool
{
    public PrepareMenuPriceTool(IAssistantActionService a) : base(a) { }
    public override string Name => "prepare_menu_price";
    public override string Description => "Prepare (not apply) a new selling price for a menu item.";
    public override JsonObject InputSchema => ToolArgs.Schema(p => { p["item_name"] = ToolArgs.S("the menu item's name"); p["new_price"] = ToolArgs.N("the new price"); }, "item_name", "new_price");
    protected override ActionIntent ToIntent(JsonObject a) => new()
    {
        Kind = AssistantActionKind.MenuPrice, ItemName = ToolArgs.Str(a, "item_name"),
        NewPrice = a["new_price"] is { } n && n.GetValueKind() == JsonValueKind.Number ? n.GetValue<decimal>() : null,
    };
}

// ---------- More ways to look things up ----------

public class TrialBalanceTool : ReadTool
{
    private readonly IJournalEntryService _j;
    public TrialBalanceTool(IJournalEntryService j) => _j = j;
    public override string Name => "get_trial_balance";
    public override string Description => "The trial balance: every account with its debit and credit balance as of a date.";
    public override JsonObject InputSchema => ToolArgs.Schema(p => p["as_of"] = ToolArgs.S("date yyyy-MM-dd (empty = today)"));
    public override async Task<AgentToolResult> RunAsync(JsonObject a, CancellationToken ct)
        => new(ToolArgs.Ser(await _j.GetTrialBalanceAsync(ToolArgs.Date(a, "as_of", DateTime.UtcNow.Date), ct)));
}

public class SearchAccountsTool : ReadTool
{
    private readonly IApplicationDbContext _c;
    public SearchAccountsTool(IApplicationDbContext c) => _c = c;
    public override string Name => "search_accounts";
    public override string Description => "Find accounts in the chart of accounts by code or part of the name. Returns up to 15.";
    public override JsonObject InputSchema => ToolArgs.Schema(p => p["query"] = ToolArgs.S("code or part of the name"), "query");
    public override async Task<AgentToolResult> RunAsync(JsonObject a, CancellationToken ct)
    {
        var q = ToolArgs.Norm(ToolArgs.Str(a, "query"));
        var list = (await _c.Accounts.AsNoTracking().Where(x => !x.IsDeleted && x.IsActive).OrderBy(x => x.Code).ToListAsync(ct))
            .Where(x => q.Length == 0 || x.Code.StartsWith(q) || ToolArgs.Norm(x.NameAr).Contains(q) || ToolArgs.Norm(x.NameEn).Contains(q)).Take(15)
            .Select(x => new { x.Id, x.Code, name = x.NameAr, type = x.AccountType.ToString() });
        return new(ToolArgs.Ser(list));
    }
}

public class AccountLedgerTool : ReadTool
{
    private readonly IApplicationDbContext _c; private readonly ILedgerReportService _l;
    public AccountLedgerTool(IApplicationDbContext c, ILedgerReportService l) { _c = c; _l = l; }
    public override string Name => "get_account_ledger";
    public override string Description => "The general ledger of one account for a period: opening balance, every movement and the closing balance. Give the account code or a name that matches exactly one account.";
    public override JsonObject InputSchema => ToolArgs.Schema(p => { p["account"] = ToolArgs.S("account code or name"); p["from"] = ToolArgs.S("start date yyyy-MM-dd"); p["to"] = ToolArgs.S("end date yyyy-MM-dd"); }, "account", "from", "to");
    public override async Task<AgentToolResult> RunAsync(JsonObject a, CancellationToken ct)
    {
        var q = ToolArgs.Norm(ToolArgs.Str(a, "account"));
        var all = await _c.Accounts.AsNoTracking().Where(x => !x.IsDeleted && x.IsActive).ToListAsync(ct);
        var hit = all.Where(x => ToolArgs.Norm(x.Code) == q).ToList();
        if (hit.Count == 0) hit = all.Where(x => ToolArgs.Norm(x.NameAr).Contains(q) || ToolArgs.Norm(x.NameEn).Contains(q)).ToList();
        if (hit.Count == 0) throw new ValidationAppException("مفيش حساب بالاسم أو الكود ده.");
        if (hit.Count > 1) return new("More than one account matches: " + ToolArgs.Ser(hit.Take(8).Select(x => new { x.Code, name = x.NameAr })) + ". Ask the user which one.");
        return new(ToolArgs.Ser(await _l.GetGeneralLedgerAsync(hit[0].Id, ToolArgs.Date(a, "from", DateTime.UtcNow.Date.AddDays(1 - DateTime.UtcNow.Day)), ToolArgs.Date(a, "to", DateTime.UtcNow.Date), ct)));
    }
}

public class CustomerStatementTool : ReadTool
{
    private readonly IApplicationDbContext _c; private readonly ILedgerReportService _l;
    public CustomerStatementTool(IApplicationDbContext c, ILedgerReportService l) { _c = c; _l = l; }
    public override string Name => "get_customer_statement";
    public override string Description => "A customer's account statement for a period: opening balance, invoices, payments and the closing balance. The name must match one customer.";
    public override JsonObject InputSchema => ToolArgs.Schema(p => { p["customer"] = ToolArgs.S("customer name"); p["from"] = ToolArgs.S("start date yyyy-MM-dd"); p["to"] = ToolArgs.S("end date yyyy-MM-dd"); }, "customer");
    public override async Task<AgentToolResult> RunAsync(JsonObject a, CancellationToken ct)
    {
        var q = ToolArgs.Norm(ToolArgs.Str(a, "customer"));
        var hit = (await _c.Customers.AsNoTracking().Where(x => !x.IsDeleted && x.IsActive).ToListAsync(ct)).Where(x => q.Length > 0 && (ToolArgs.Norm(x.NameAr).Contains(q) || ToolArgs.Norm(x.NameEn).Contains(q))).ToList();
        if (hit.Count == 0) throw new ValidationAppException("مفيش عميل بالاسم ده.");
        if (hit.Count > 1) return new("More than one customer matches: " + ToolArgs.Ser(hit.Take(8).Select(x => x.NameAr)) + ". Ask the user which one.");
        var from = ToolArgs.Date(a, "from", new DateTime(DateTime.UtcNow.Year, 1, 1));
        return new(ToolArgs.Ser(await _l.GetCustomerStatementAsync(hit[0].Id, from, ToolArgs.Date(a, "to", DateTime.UtcNow.Date), ct)));
    }
}

public class VendorStatementTool : ReadTool
{
    private readonly IApplicationDbContext _c; private readonly ILedgerReportService _l;
    public VendorStatementTool(IApplicationDbContext c, ILedgerReportService l) { _c = c; _l = l; }
    public override string Name => "get_vendor_statement";
    public override string Description => "A supplier's account statement for a period: opening balance, invoices, payments and the closing balance. The name must match one supplier.";
    public override JsonObject InputSchema => ToolArgs.Schema(p => { p["vendor"] = ToolArgs.S("supplier name"); p["from"] = ToolArgs.S("start date yyyy-MM-dd"); p["to"] = ToolArgs.S("end date yyyy-MM-dd"); }, "vendor");
    public override async Task<AgentToolResult> RunAsync(JsonObject a, CancellationToken ct)
    {
        var q = ToolArgs.Norm(ToolArgs.Str(a, "vendor"));
        var hit = (await _c.Vendors.AsNoTracking().Where(x => !x.IsDeleted && x.IsActive).ToListAsync(ct)).Where(x => q.Length > 0 && (ToolArgs.Norm(x.NameAr).Contains(q) || ToolArgs.Norm(x.NameEn).Contains(q))).ToList();
        if (hit.Count == 0) throw new ValidationAppException("مفيش مورد بالاسم ده.");
        if (hit.Count > 1) return new("More than one supplier matches: " + ToolArgs.Ser(hit.Take(8).Select(x => x.NameAr)) + ". Ask the user which one.");
        var from = ToolArgs.Date(a, "from", new DateTime(DateTime.UtcNow.Year, 1, 1));
        return new(ToolArgs.Ser(await _l.GetVendorStatementAsync(hit[0].Id, from, ToolArgs.Date(a, "to", DateTime.UtcNow.Date), ct)));
    }
}

public class CustomerProfitabilityTool : ReadTool
{
    private readonly IFinancialReportService _r;
    public CustomerProfitabilityTool(IFinancialReportService r) => _r = r;
    public override string Name => "get_customer_profitability";
    public override string Description => "Which customers bring the most profit in a period.";
    public override JsonObject InputSchema => ToolArgs.Schema(p => { p["from"] = ToolArgs.S("start date yyyy-MM-dd"); p["to"] = ToolArgs.S("end date yyyy-MM-dd"); }, "from", "to");
    public override async Task<AgentToolResult> RunAsync(JsonObject a, CancellationToken ct)
        => new(ToolArgs.Ser(await _r.GetCustomerProfitabilityAsync(ToolArgs.Date(a, "from", DateTime.UtcNow.Date.AddDays(1 - DateTime.UtcNow.Day)), ToolArgs.Date(a, "to", DateTime.UtcNow.Date), ct)));
}

public class ItemProfitabilityTool : ReadTool
{
    private readonly IFinancialReportService _r;
    public ItemProfitabilityTool(IFinancialReportService r) => _r = r;
    public override string Name => "get_item_profitability";
    public override string Description => "Which items sell most and bring the most profit in a period.";
    public override JsonObject InputSchema => ToolArgs.Schema(p => { p["from"] = ToolArgs.S("start date yyyy-MM-dd"); p["to"] = ToolArgs.S("end date yyyy-MM-dd"); }, "from", "to");
    public override async Task<AgentToolResult> RunAsync(JsonObject a, CancellationToken ct)
        => new(ToolArgs.Ser(await _r.GetItemProfitabilityAsync(ToolArgs.Date(a, "from", DateTime.UtcNow.Date.AddDays(1 - DateTime.UtcNow.Day)), ToolArgs.Date(a, "to", DateTime.UtcNow.Date), ct)));
}

// ---------- More drafts (nothing is booked until the owner approves) ----------

public class PrepareNewCustomerTool : PrepareTool
{
    public PrepareNewCustomerTool(IAssistantActionService a) : base(a) { }
    public override string Name => "prepare_new_customer";
    public override string Description => "Prepare (not add) a new customer. Use only when the user asked to add one.";
    public override JsonObject InputSchema => ToolArgs.Schema(p => { p["name"] = ToolArgs.S("the customer's name"); p["phone"] = ToolArgs.S("phone, only if stated"); p["email"] = ToolArgs.S("email, only if stated"); }, "name");
    protected override ActionIntent ToIntent(JsonObject a) => new() { Kind = AssistantActionKind.CreateCustomer, PartyName = ToolArgs.Str(a, "name"), PartyPhone = ToolArgs.Str(a, "phone"), PartyEmail = ToolArgs.Str(a, "email") };
}

public class PrepareNewVendorTool : PrepareTool
{
    public PrepareNewVendorTool(IAssistantActionService a) : base(a) { }
    public override string Name => "prepare_new_vendor";
    public override string Description => "Prepare (not add) a new supplier. Use only when the user asked to add one.";
    public override JsonObject InputSchema => ToolArgs.Schema(p => { p["name"] = ToolArgs.S("the supplier's name"); p["phone"] = ToolArgs.S("phone, only if stated"); p["email"] = ToolArgs.S("email, only if stated"); }, "name");
    protected override ActionIntent ToIntent(JsonObject a) => new() { Kind = AssistantActionKind.CreateVendor, PartyName = ToolArgs.Str(a, "name"), PartyPhone = ToolArgs.Str(a, "phone"), PartyEmail = ToolArgs.Str(a, "email") };
}

public class PrepareVendorPaymentTool : PrepareTool
{
    public PrepareVendorPaymentTool(IAssistantActionService a) : base(a) { }
    public override string Name => "prepare_vendor_payment";
    public override string Description => "Prepare (not book) a payment the company makes to a supplier on an open purchase invoice. Give the invoice number or the supplier's name.";
    public override JsonObject InputSchema => ToolArgs.Schema(p =>
    {
        p["invoice_number"] = ToolArgs.S("purchase invoice number if known"); p["vendor_name"] = ToolArgs.S("supplier name if the number is not known");
        p["amount"] = ToolArgs.N("amount paid, only if stated"); p["method"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("cash", "card") };
    });
    protected override ActionIntent ToIntent(JsonObject a) => new()
    {
        Kind = AssistantActionKind.VendorPayment, InvoiceNumber = ToolArgs.Str(a, "invoice_number"), VendorName = ToolArgs.Str(a, "vendor_name"), Method = ToolArgs.Str(a, "method"),
        Amount = a["amount"] is { } n && n.GetValueKind() == JsonValueKind.Number ? n.GetValue<decimal>() : null,
    };
}

public class PreparePurchaseInvoiceTool : PrepareTool
{
    public PreparePurchaseInvoiceTool(IAssistantActionService a) : base(a) { }
    public override string Name => "prepare_purchase_invoice";
    public override string Description => "Prepare (not book) a purchase invoice from an existing supplier. Do not invent prices or accounts: the user chooses the account of each line on the draft.";
    public override JsonObject InputSchema => ToolArgs.Schema(p =>
    {
        p["vendor_name"] = ToolArgs.S("the supplier's name as the user said it");
        p["lines"] = new JsonObject { ["type"] = "array", ["items"] = ToolArgs.Schema(l => { l["description"] = ToolArgs.S("what was bought"); l["quantity"] = ToolArgs.N("quantity"); l["unit_price"] = ToolArgs.N("price of one, only if stated"); }, "description", "quantity") };
        p["payment_term"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("cash", "card", "credit") };
        p["note"] = ToolArgs.S("optional note");
    }, "vendor_name", "lines");
    protected override ActionIntent ToIntent(JsonObject a)
    {
        var intent = new ActionIntent { Kind = AssistantActionKind.PurchaseInvoice, VendorName = ToolArgs.Str(a, "vendor_name"), PaymentTerm = ToolArgs.Str(a, "payment_term"), Note = ToolArgs.Str(a, "note") };
        if (a["lines"] is JsonArray lines)
            foreach (var l in lines.OfType<JsonObject>())
                intent.Lines.Add(new ActionInvoiceLineDto { Description = ToolArgs.Str(l, "description"), Quantity = l["quantity"] is { } q && q.GetValueKind() == JsonValueKind.Number ? q.GetValue<decimal>() : 1, UnitPrice = l["unit_price"] is { } u && u.GetValueKind() == JsonValueKind.Number ? u.GetValue<decimal>() : null });
        return intent;
    }
}
