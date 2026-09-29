using Microsoft.Extensions.DependencyInjection;
using RomaERP.Application.Accounting.Services;
using RomaERP.Application.Alerts.Services;
using RomaERP.Application.Assistant.Services;
using RomaERP.Application.EInvoicing.Services;
using RomaERP.Application.EInvoicing.Services.Eta;
using RomaERP.Application.EInvoicing.Services.Zatca;
using RomaERP.Application.HR.Services;
using RomaERP.Application.Inventory.Services;
using RomaERP.Application.Notifications.Services;
using RomaERP.Application.Purchasing.Services;
using RomaERP.Application.Restaurant.Services;
using RomaERP.Application.Sales.Services;

namespace RomaERP.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IExchangeRateService, ExchangeRateService>();
        services.AddScoped<IBudgetService, BudgetService>();
        services.AddScoped<IBankFeedReconciliationService, BankFeedReconciliationService>();
        services.AddScoped<IJournalEntryService, JournalEntryService>();
        services.AddScoped<IAlertsService, AlertsService>();
        services.AddScoped<IWhatsAppNotificationService, WhatsAppNotificationService>();
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
        services.AddScoped<IWorkLocationService, WorkLocationService>();
        services.AddScoped<IAttendanceService, AttendanceService>();
        services.AddScoped<IEmployeeRequestService, EmployeeRequestService>();
        services.AddScoped<IEmployeeContractService, EmployeeContractService>();

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
        services.AddScoped<IDeliveryOrderIntakeService, DeliveryOrderIntakeService>();
        services.AddScoped<ICashierShiftService, CashierShiftService>();

        // E-invoicing: both providers' document signer AND API client are real (see
        // Infrastructure.AddInfrastructure — ZatcaXadesDocumentSigner/ZatcaHttpApiClient and
        // EtaCertificateDocumentSigner/EtaHttpApiClient). Neither has been exercised against a live government
        // endpoint from this sandbox (no network access to zatca.gov.sa or eta.gov.eg) — see those classes'
        // doc comments for what remains to verify once a customer has real credentials. The ETA signer also
        // only covers taxpayers on ETA's API-based certificate registration path, not the hardware-USB-token
        // manual-portal path, which cannot be automated from a server at all.
        services.AddScoped<IEInvoicingService, EInvoicingService>();
        services.AddScoped<IEInvoicingProvider, EtaEInvoicingProvider>();
        services.AddScoped<IEInvoicingProvider, ZatcaEInvoicingProvider>();

        return services;
    }
}
