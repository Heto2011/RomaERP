using Microsoft.Extensions.DependencyInjection;
using RomaERP.Application.Accounting.Services;
using RomaERP.Application.Assistant.Services;
using RomaERP.Application.HR.Services;
using RomaERP.Application.Inventory.Services;
using RomaERP.Application.Purchasing.Services;
using RomaERP.Application.Sales.Services;

namespace RomaERP.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IJournalEntryService, JournalEntryService>();
        services.AddScoped<IFinancialReportService, FinancialReportService>();
        services.AddScoped<IFiscalPeriodService, FiscalPeriodService>();
        services.AddScoped<IOpeningBalanceService, OpeningBalanceService>();

        services.AddScoped<IDepartmentService, DepartmentService>();
        services.AddScoped<IPositionService, PositionService>();
        services.AddScoped<IEmployeeService, EmployeeService>();
        services.AddScoped<ISalaryComponentService, SalaryComponentService>();
        services.AddScoped<IPayrollService, PayrollService>();

        services.AddScoped<IItemCategoryService, ItemCategoryService>();
        services.AddScoped<IWarehouseService, WarehouseService>();
        services.AddScoped<IItemService, ItemService>();
        services.AddScoped<IInventoryService, InventoryService>();

        services.AddScoped<IExpenseAssistantService, ExpenseAssistantService>();
        services.AddScoped<IBankReconciliationService, BankReconciliationService>();

        services.AddScoped<ISalesService, SalesService>();
        services.AddScoped<IPurchasingService, PurchasingService>();

        return services;
    }
}
