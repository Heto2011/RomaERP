namespace RomaERP.Domain.Tenancy;

/// <summary>Which parts of the API each product (<see cref="ProductScope"/>) may use. The app also hides the sections a
/// product does not include, but hiding is only cosmetic: this is what actually stops a company on one product from
/// reaching another product's data by typing a link or calling the API directly.</summary>
public static class ProductScopeAccess
{
    public enum Area
    {
        /// <summary>Sign-in, users, company settings, subscription, support, search, alerts… every product needs these.</summary>
        Common,
        Hr,
        Restaurant,
        /// <summary>Stock movements, counts, waste, manufacturing, inventory reports.</summary>
        InventoryOperations,
        /// <summary>Items, categories and warehouses — also what sales and purchase invoice lines pick from.</summary>
        InventoryBasics,
        /// <summary>Journal entries, fixed assets, budgets, bank reconciliation, loans, deferrals, opening balances…</summary>
        AdvancedAccounting,
        /// <summary>Chart of accounts, fiscal periods and the financial reports.</summary>
        Accounting,
        /// <summary>Fixed monthly expenses and expense policies.</summary>
        Expenses,
        Sales,
        Purchasing,
        Ai,
        EInvoicing
    }

    private static readonly Dictionary<string, Area> AreaBySegment = new(StringComparer.OrdinalIgnoreCase)
    {
        ["attendance"] = Area.Hr, ["departments"] = Area.Hr, ["employee-contracts"] = Area.Hr, ["employee-requests"] = Area.Hr,
        ["employees"] = Area.Hr, ["payroll"] = Area.Hr, ["performance"] = Area.Hr, ["positions"] = Area.Hr, ["recruitment"] = Area.Hr,
        ["salarycomponents"] = Area.Hr, ["shifts"] = Area.Hr, ["surveys"] = Area.Hr, ["work-locations"] = Area.Hr, ["careers"] = Area.Hr,

        ["restaurant"] = Area.Restaurant, ["cashiershifts"] = Area.Restaurant, ["delivery-platforms"] = Area.Restaurant, ["deliveryreconciliation"] = Area.Restaurant,

        ["inventory"] = Area.InventoryOperations, ["inventoryreports"] = Area.InventoryOperations, ["wasteentries"] = Area.InventoryOperations,
        ["physicalstockcounts"] = Area.InventoryOperations, ["manufacturing"] = Area.InventoryOperations,
        ["items"] = Area.InventoryBasics, ["itemcategories"] = Area.InventoryBasics, ["warehouses"] = Area.InventoryBasics,

        ["journalentries"] = Area.AdvancedAccounting, ["fixedassets"] = Area.AdvancedAccounting, ["depreciation"] = Area.AdvancedAccounting,
        ["budgets"] = Area.AdvancedAccounting, ["bank-feed-reconciliation"] = Area.AdvancedAccounting, ["bankreconciliation"] = Area.AdvancedAccounting,
        ["exchange-rates"] = Area.AdvancedAccounting, ["openingbalance"] = Area.AdvancedAccounting, ["manualprofitentries"] = Area.AdvancedAccounting,
        ["deferrals"] = Area.AdvancedAccounting, ["loans"] = Area.AdvancedAccounting,

        ["accounts"] = Area.Accounting, ["financialreports"] = Area.Accounting, ["fiscalperiods"] = Area.Accounting,
        ["recurring-expenses"] = Area.Expenses, ["expense-policies"] = Area.Expenses,

        ["sales"] = Area.Sales, ["customer-followups"] = Area.Sales,
        ["purchasing"] = Area.Purchasing,
        ["aiassistant"] = Area.Ai, ["whatsapp"] = Area.Ai,
        ["einvoicing"] = Area.EInvoicing,
    };

    /// <summary>What each product may NOT use. Anything not listed (and every unknown route) stays open, so a new
    /// shared screen is never locked out by accident; the full product has no restrictions.</summary>
    private static readonly Dictionary<ProductScope, HashSet<Area>> Blocked = new()
    {
        [ProductScope.PeopleOnly] = new()
        {
            Area.Restaurant, Area.InventoryOperations, Area.InventoryBasics, Area.AdvancedAccounting,
            Area.Sales, Area.Purchasing, Area.Ai, Area.EInvoicing
        },
        [ProductScope.BooksOnly] = new() { Area.Hr, Area.Restaurant, Area.InventoryOperations, Area.Ai, Area.EInvoicing },
        [ProductScope.PosOnly] = new() { Area.Hr, Area.AdvancedAccounting },
    };

    public static Area AreaOf(string path)
    {
        var p = (path ?? string.Empty).Trim('/');
        if (p.StartsWith("api/", StringComparison.OrdinalIgnoreCase)) p = p[4..];
        var segment = p.Split('/', 2)[0];
        return AreaBySegment.GetValueOrDefault(segment, Area.Common);
    }

    public static bool IsAllowed(ProductScope scope, string path)
        => !Blocked.TryGetValue(scope, out var blocked) || !blocked.Contains(AreaOf(path));
}
