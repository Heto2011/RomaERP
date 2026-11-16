using RomaERP.Domain.Common;

namespace RomaERP.Domain.Inventory;

/// <summary>A production site (a warehouse/branch that makes things) and its fixed production overhead (kitchen rent, equipment depreciation,
/// supervisors...). Per IAS 2 the fixed overhead is absorbed into the cost of what is produced on the basis of the site's NORMAL capacity:
/// rate = monthly fixed overhead / normal batches. What is not absorbed (when the site works below normal) stays an expense of the period.</summary>
public class ManufacturingSite : AuditableEntity
{
    public Guid WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public decimal MonthlyFixedOverhead { get; set; }
    /// <summary>How many batches the site normally makes in a month under ordinary conditions (not the best month, not a bad one).</summary>
    public decimal NormalMonthlyBatches { get; set; }
    public bool IsActive { get; set; } = true;
}
