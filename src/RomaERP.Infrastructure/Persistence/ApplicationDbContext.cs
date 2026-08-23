using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Domain.Accounting;
using RomaERP.Domain.Assistant;
using RomaERP.Domain.HR;
using RomaERP.Domain.Inventory;
using RomaERP.Domain.Purchasing;
using RomaERP.Domain.Sales;
using RomaERP.Domain.Tenancy;
using RomaERP.Infrastructure.Identity;

namespace RomaERP.Infrastructure.Persistence;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<CostCenter> CostCenters => Set<CostCenter>();
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

    public DbSet<ExpenseCapture> ExpenseCaptures => Set<ExpenseCapture>();
    public DbSet<ExpenseCaptureMessage> ExpenseCaptureMessages => Set<ExpenseCaptureMessage>();
    public DbSet<BankStatementImport> BankStatementImports => Set<BankStatementImport>();
    public DbSet<BankStatementLine> BankStatementLines => Set<BankStatementLine>();

    public DbSet<CompanySettings> CompanySettings => Set<CompanySettings>();

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<SalesInvoice> SalesInvoices => Set<SalesInvoice>();
    public DbSet<SalesInvoiceLine> SalesInvoiceLines => Set<SalesInvoiceLine>();
    public DbSet<SalesPayment> SalesPayments => Set<SalesPayment>();

    public DbSet<Vendor> Vendors => Set<Vendor>();
    public DbSet<PurchaseInvoice> PurchaseInvoices => Set<PurchaseInvoice>();
    public DbSet<PurchaseInvoiceLine> PurchaseInvoiceLines => Set<PurchaseInvoiceLine>();
    public DbSet<PurchasePayment> PurchasePayments => Set<PurchasePayment>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
