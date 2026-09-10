namespace RomaERP.Application.Common;

/// <summary>Fine-grained, per-user module grants layered on top of the four fixed roles (Admin, Accountant,
/// HR, Employee). Admin always has every module; a user's base role also keeps its existing broad access
/// (Accountant/HR still work exactly as before) — this only lets an Admin additionally hand one specific
/// module (e.g. just "Sales") to someone who wouldn't otherwise have it, without making them a full
/// Accountant. Stored as ASP.NET Identity user claims (type <see cref="ClaimType"/>), so no schema change
/// was needed, and embedded into the JWT at login the same way roles already are.</summary>
public static class ModulePermissions
{
    public const string ClaimType = "module";

    public const string Accounting = "Accounting";
    public const string Reports = "Reports";
    public const string Sales = "Sales";
    public const string Purchasing = "Purchasing";
    public const string HR = "HR";
    public const string Inventory = "Inventory";
    public const string POS = "POS";

    public static readonly string[] All = { Accounting, Reports, Sales, Purchasing, HR, Inventory, POS };

    // Policy names as compile-time constants so [Authorize(Policy = ...)] attributes can reference them.
    public const string AccountingPolicy = "Module:" + Accounting;
    public const string ReportsPolicy = "Module:" + Reports;
    public const string SalesPolicy = "Module:" + Sales;
    public const string PurchasingPolicy = "Module:" + Purchasing;
    public const string HRPolicy = "Module:" + HR;
    public const string InventoryPolicy = "Module:" + Inventory;
    public const string POSPolicy = "Module:" + POS;

    /// <summary>Existing roles that already imply this module, in addition to Admin (which always passes).</summary>
    public static readonly IReadOnlyDictionary<string, string[]> FallbackRoles = new Dictionary<string, string[]>
    {
        [Accounting] = new[] { "Accountant" },
        [Reports] = new[] { "Accountant" },
        [Sales] = new[] { "Accountant" },
        [Purchasing] = new[] { "Accountant" },
        [Inventory] = new[] { "Accountant" },
        [HR] = new[] { "HR" },
        [POS] = new[] { "Accountant", "Employee" },
    };

    public static string PolicyName(string module) => $"Module:{module}";
}
