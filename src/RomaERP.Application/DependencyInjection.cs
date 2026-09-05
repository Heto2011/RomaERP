using Microsoft.Extensions.DependencyInjection;
using RomaERP.Application.Accounting.Services;
using RomaERP.Application.Alerts.Services;
using RomaERP.Application.Assistant.Services;
using RomaERP.Application.EInvoicing.Services;
using RomaERP.Application.EInvoicing.Services.Eta;
using RomaERP.Application.EInvoicing.Services.Zatca;
using RomaERP.Application.HR.Services;
using RomaERP.Application.Inventory.Services;
using RomaERP.Application.Purchasing.Services;
using RomaERP.Application.Restaurant.Services;
using RomaERP.Application.Sales.Services;

namespace RomaERP.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IJournalEntryService, JournalEntryService>();
        services.AddScoped<IAlertsService, AlertsService>();
        services.AddScoped<IFinancialReportService, FinancialReportService>();
        services.AddScoped<IManualProfitEntryService, ManualProfitEntryService>();
        services.AddScoped<IFiscalPeriodService, FiscalPeriodService>();
        services.AddScoped<IOpeningBalanceService, OpeningBalanceService>();
        services.AddScoped<IFixedAssetService, FixedAssetService>();
        services.AddScoped<IDepreciationService, DepreciationService>();

        services.AddScoped<IDepartmentService, DepartmentService>();
        services.AddScoped<IPositionService, PositionService>();
        services.AddScoped<IEmployeeService, EmployeeService>();
        services.AddScoped<ISalaryComponentService, SalaryComponentService>();
        services.AddScoped<IPayrollService, PayrollService>();

        services.AddScoped<IItemCategoryService, ItemCategoryService>();
        services.AddScoped<IWarehouseService, WarehouseService>();
        services.AddScoped<IItemService, ItemService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IInventoryReportService, InventoryReportService>();
        services.AddScoped<IPhysicalStockCountService, PhysicalStockCountService>();
        services.AddScoped<IWasteEntryService, WasteEntryService>();
        services.AddScoped<IManufacturingService, ManufacturingService>();
        services.AddScoped<IItemLotService, ItemLotService>();

        services.AddScoped<IExpenseAssistantService, ExpenseAssistantService>();
        services.AddScoped<IBankReconciliationService, BankReconciliationService>();

        services.AddScoped<ISalesService, SalesService>();
        services.AddScoped<IPurchasingService, PurchasingService>();
        services.AddScoped<IRestaurantService, RestaurantService>();
        services.AddScoped<IDeliveryReconciliationService, DeliveryReconciliationService>();
        services.AddScoped<ICashierShiftService, CashierShiftService>();

        // E-invoicing: ZATCA's document signer AND API client are real (see Infrastructure.AddInfrastructure —
        // ZatcaXadesDocumentSigner / ZatcaHttpApiClient). The ETA side is still mock — the ETA signer needs a
        // physical USB signing token that can't run server-side, and its government API client has nowhere
        // real to call yet — swap for a real implementation once a customer's credentials are available.
        services.AddScoped<IEInvoicingService, EInvoicingService>();
        services.AddScoped<IEInvoicingProvider, EtaEInvoicingProvider>();
        services.AddScoped<IEInvoicingProvider, ZatcaEInvoicingProvider>();
        services.AddScoped<IEtaDocumentSigner, MockEtaDocumentSigner>();
        services.AddScoped<IEtaApiClient, MockEtaApiClient>();

        return services;
    }
}
