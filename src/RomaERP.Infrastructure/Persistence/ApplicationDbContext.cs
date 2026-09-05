using System.Text.Json;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Domain.Accounting;
using RomaERP.Domain.Assistant;
using RomaERP.Domain.Audit;
using RomaERP.Domain.Common;
using RomaERP.Domain.HR;
using RomaERP.Domain.Inventory;
using RomaERP.Domain.Purchasing;
using RomaERP.Domain.Restaurant;
using RomaERP.Domain.Sales;
using RomaERP.Domain.Tenancy;
using RomaERP.Infrastructure.Identity;

namespace RomaERP.Infrastructure.Persistence;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>, IApplicationDbContext
{
    private static readonly HashSet<string> AuditExcludedProperties = new()
    {
        "PasswordHash", "SecurityStamp", "ConcurrencyStamp", "NormalizedEmail", "NormalizedUserName",
    };

    private readonly ICurrentUserService? _currentUser;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ICurrentUserService? currentUser = null) : base(options)
    {
        _currentUser = currentUser;
    }

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<CostCenter> CostCenters => Set<CostCenter>();
    public DbSet<ManualProfitEntry> ManualProfitEntries => Set<ManualProfitEntry>();
    public DbSet<FiscalYear> FiscalYears => Set<FiscalYear>();
    public DbSet<FiscalPeriod> FiscalPeriods => Set<FiscalPeriod>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<JournalEntryLine> JournalEntryLines => Set<JournalEntryLine>();
    public DbSet<FixedAsset> FixedAssets => Set<FixedAsset>();
    public DbSet<DepreciationRun> DepreciationRuns => Set<DepreciationRun>();
    public DbSet<DepreciationRunLine> DepreciationRunLines => Set<DepreciationRunLine>();

    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<SalaryComponent> SalaryComponents => Set<SalaryComponent>();
    public DbSet<EmployeeSalaryComponent> EmployeeSalaryComponents => Set<EmployeeSalaryComponent>();
    public DbSet<PayrollRun> PayrollRuns => Set<PayrollRun>();
    public DbSet<PayrollRunLine> PayrollRunLines => Set<PayrollRunLine>();

    public DbSet<ItemCategory> ItemCategories => Set<ItemCategory>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<PhysicalStockCount> PhysicalStockCounts => Set<PhysicalStockCount>();
    public DbSet<WasteEntry> WasteEntries => Set<WasteEntry>();
    public DbSet<ManufacturingBom> ManufacturingBoms => Set<ManufacturingBom>();
    public DbSet<ManufacturingBomLine> ManufacturingBomLines => Set<ManufacturingBomLine>();
    public DbSet<ManufacturingOrder> ManufacturingOrders => Set<ManufacturingOrder>();
    public DbSet<ManufacturingOrderLine> ManufacturingOrderLines => Set<ManufacturingOrderLine>();

    public DbSet<RestaurantTable> RestaurantTables => Set<RestaurantTable>();
    public DbSet<RestaurantOrder> RestaurantOrders => Set<RestaurantOrder>();
    public DbSet<RestaurantOrderLine> RestaurantOrderLines => Set<RestaurantOrderLine>();
    public DbSet<MenuRecipeLine> MenuRecipeLines => Set<MenuRecipeLine>();
    public DbSet<DeliverySettlementImport> DeliverySettlementImports => Set<DeliverySettlementImport>();
    public DbSet<DeliverySettlementLine> DeliverySettlementLines => Set<DeliverySettlementLine>();
    public DbSet<CashierShift> CashierShifts => Set<CashierShift>();

    public DbSet<ExpenseCapture> ExpenseCaptures => Set<ExpenseCapture>();
    public DbSet<ExpenseCaptureMessage> ExpenseCaptureMessages => Set<ExpenseCaptureMessage>();
    public DbSet<BankStatementImport> BankStatementImports => Set<BankStatementImport>();
    public DbSet<BankStatementLine> BankStatementLines => Set<BankStatementLine>();

    public DbSet<CompanySettings> CompanySettings => Set<CompanySettings>();

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<SalesInvoice> SalesInvoices => Set<SalesInvoice>();
    public DbSet<SalesInvoiceLine> SalesInvoiceLines => Set<SalesInvoiceLine>();
    public DbSet<SalesPayment> SalesPayments => Set<SalesPayment>();
    public DbSet<SalesInstallmentLine> SalesInstallmentLines => Set<SalesInstallmentLine>();
    public DbSet<SalesNote> SalesNotes => Set<SalesNote>();
    public DbSet<SalesNoteLine> SalesNoteLines => Set<SalesNoteLine>();

    public DbSet<Vendor> Vendors => Set<Vendor>();
    public DbSet<PurchaseInvoice> PurchaseInvoices => Set<PurchaseInvoice>();
    public DbSet<PurchaseInvoiceLine> PurchaseInvoiceLines => Set<PurchaseInvoiceLine>();
    public DbSet<PurchasePayment> PurchasePayments => Set<PurchasePayment>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var auditEntries = BuildAuditEntries();
        if (auditEntries.Count > 0) AuditLogs.AddRange(auditEntries);
        return await base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        var auditEntries = BuildAuditEntries();
        if (auditEntries.Count > 0) AuditLogs.AddRange(auditEntries);
        return base.SaveChanges();
    }

    /// <summary>Turns pending changes into AuditLog rows before they're saved — soft-deletes (IsDeleted flipping
    /// to true) are recorded as Deleted rather than Updated, since this app never physically removes rows.</summary>
    private List<AuditLog> BuildAuditEntries()
    {
        var entries = new List<AuditLog>();
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is AuditLog) continue;
            if (entry.Entity is not (BaseEntity or ApplicationUser)) continue;
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)) continue;

            var idProperty = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "Id");
            var entityId = idProperty?.CurrentValue?.ToString() ?? string.Empty;

            AuditAction action;
            Dictionary<string, object?> changes = new();

            if (entry.State == EntityState.Added)
            {
                action = AuditAction.Created;
                foreach (var prop in entry.Properties.Where(p => !AuditExcludedProperties.Contains(p.Metadata.Name)))
                    changes[prop.Metadata.Name] = prop.CurrentValue;
            }
            else if (entry.State == EntityState.Deleted)
            {
                action = AuditAction.Deleted;
                foreach (var prop in entry.Properties.Where(p => !AuditExcludedProperties.Contains(p.Metadata.Name)))
                    changes[prop.Metadata.Name] = prop.OriginalValue;
            }
            else
            {
                var softDeleted = entry.Properties.Any(p =>
                    p.Metadata.Name == "IsDeleted" && p.IsModified && Equals(p.CurrentValue, true));
                action = softDeleted ? AuditAction.Deleted : AuditAction.Updated;

                foreach (var prop in entry.Properties.Where(p => p.IsModified && !AuditExcludedProperties.Contains(p.Metadata.Name)))
                    changes[prop.Metadata.Name] = new { old = prop.OriginalValue, @new = prop.CurrentValue };

                if (changes.Count == 0) continue;
            }

            entries.Add(new AuditLog
            {
                EntityName = entry.Entity.GetType().Name,
                EntityId = entityId,
                Action = action,
                UserId = _currentUser?.UserId,
                UserName = _currentUser?.UserName,
                OccurredAtUtc = now,
                Changes = JsonSerializer.Serialize(changes),
            });
        }

        return entries;
    }
}
