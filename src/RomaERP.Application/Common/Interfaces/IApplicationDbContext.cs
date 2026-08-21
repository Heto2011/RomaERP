using Microsoft.EntityFrameworkCore;
using RomaERP.Domain.Accounting;
using RomaERP.Domain.HR;
using RomaERP.Domain.Inventory;

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

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
