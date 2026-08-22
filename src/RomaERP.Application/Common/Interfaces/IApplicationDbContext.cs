using Microsoft.EntityFrameworkCore;
using RomaERP.Domain.Accounting;
using RomaERP.Domain.Assistant;
using RomaERP.Domain.HR;
using RomaERP.Domain.Inventory;
using RomaERP.Domain.Purchasing;
using RomaERP.Domain.Sales;
using RomaERP.Domain.Tenancy;

namespace RomaERP.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Account> Accounts { get; }
    DbSet<CostCenter> CostCenters { get; }
    DbSet<FiscalYear> FiscalYears { get; }
    DbSet<FiscalPeriod> FiscalPeriods { get; }
    DbSet<JournalEntry> JournalEntries { get; }
    DbSet<JournalEntryLine> JournalEntryLines { get; }

    DbSet<Department> Departments { get; }
    DbSet<Position> Positions { get; }
    DbSet<Employee> Employees { get; }
    DbSet<SalaryComponent> SalaryComponents { get; }
    DbSet<EmployeeSalaryComponent> EmployeeSalaryComponents { get; }
    DbSet<PayrollRun> PayrollRuns { get; }
    DbSet<PayrollRunLine> PayrollRunLines { get; }

    DbSet<ItemCategory> ItemCategories { get; }
    DbSet<Warehouse> Warehouses { get; }
    DbSet<Item> Items { get; }
    DbSet<StockMovement> StockMovements { get; }

    DbSet<ExpenseCapture> ExpenseCaptures { get; }
    DbSet<ExpenseCaptureMessage> ExpenseCaptureMessages { get; }
    DbSet<BankStatementImport> BankStatementImports { get; }
    DbSet<BankStatementLine> BankStatementLines { get; }

    DbSet<CompanySettings> CompanySettings { get; }

    DbSet<Customer> Customers { get; }
    DbSet<SalesInvoice> SalesInvoices { get; }
    DbSet<SalesInvoiceLine> SalesInvoiceLines { get; }
    DbSet<SalesPayment> SalesPayments { get; }

    DbSet<Vendor> Vendors { get; }
    DbSet<PurchaseInvoice> PurchaseInvoices { get; }
    DbSet<PurchaseInvoiceLine> PurchaseInvoiceLines { get; }
    DbSet<PurchasePayment> PurchasePayments { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
