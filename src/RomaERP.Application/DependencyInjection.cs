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
        services.AddScoped<IChartImportService, ChartImportService>();
        services.AddScoped<IAccountBalanceTransferService, AccountBalanceTransferService>();
        services.AddScoped<IExchangeRateService, ExchangeRateService>();
        services.AddScoped<IBudgetService, BudgetService>();
        services.AddScoped<IBankFeedReconciliationService, BankFeedReconciliationService>();
        services.AddScoped<IJournalEntryService, JournalEntryService>();
        services.AddScoped<IAlertsService, AlertsService>();
        services.AddScoped<IWhatsAppNotificationService, WhatsAppNotificationService>();
        services.AddScoped<IFinancialReportService, FinancialReportService>();
        services.AddScoped<ILedgerReportService, LedgerReportService>();
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
        services.AddScoped<IShiftService, ShiftService>();
        services.AddScoped<IRecruitmentService, RecruitmentService>();
        services.AddScoped<IPerformanceService, PerformanceService>();
        services.AddScoped<ISurveyService, SurveyService>();

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
        services.AddScoped<IExpensePolicyService, ExpensePolicyService>();
        services.AddScoped<IRecurringExpenseService, RecurringExpenseService>();
        services.AddScoped<IRecurringExpenseReminderService, RecurringExpenseReminderService>();
        services.AddScoped<IBankReconciliationService, BankReconciliationService>();

        services.AddScoped<ISalesService, SalesService>();
        services.AddScoped<IPurchasingService, PurchasingService>();
        services.AddScoped<IPendingPurchaseService, PendingPurchaseService>();
        services.AddScoped<ICustomerFollowUpService, CustomerFollowUpService>();
        services.AddScoped<IDeferralService, DeferralService>();
        services.AddScoped<ILoanService, LoanService>();
        services.AddScoped<IInvoiceScanService, InvoiceScanService>();
        services.AddScoped<IRestaurantService, RestaurantService>();
        services.AddScoped<IQrOrderingService, QrOrderingService>();
        services.AddScoped<RomaERP.Application.Assistant.Services.IAssistantActionService, RomaERP.Application.Assistant.Services.AssistantActionService>();
        services.AddScoped<RomaERP.Application.Assistant.Agent.IAssistantAgentService, RomaERP.Application.Assistant.Agent.AssistantAgentService>();
        foreach (var tool in new[]
        {
            typeof(RomaERP.Application.Assistant.Agent.SearchCustomersTool), typeof(RomaERP.Application.Assistant.Agent.SearchVendorsTool), typeof(RomaERP.Application.Assistant.Agent.SearchItemsTool),
            typeof(RomaERP.Application.Assistant.Agent.ListSalesInvoicesTool), typeof(RomaERP.Application.Assistant.Agent.ListPurchaseInvoicesTool),
            typeof(RomaERP.Application.Assistant.Agent.ReceivablesTool), typeof(RomaERP.Application.Assistant.Agent.PayablesTool),
            typeof(RomaERP.Application.Assistant.Agent.IncomeStatementTool), typeof(RomaERP.Application.Assistant.Agent.BalanceSheetTool), typeof(RomaERP.Application.Assistant.Agent.VatSummaryTool),
            typeof(RomaERP.Application.Assistant.Agent.CashFlowTool), typeof(RomaERP.Application.Assistant.Agent.BranchProfitTool),
            typeof(RomaERP.Application.Assistant.Agent.PrepareSalesInvoiceTool), typeof(RomaERP.Application.Assistant.Agent.PrepareCustomerPaymentTool), typeof(RomaERP.Application.Assistant.Agent.PrepareMenuPriceTool),
            typeof(RomaERP.Application.Assistant.Agent.TrialBalanceTool), typeof(RomaERP.Application.Assistant.Agent.SearchAccountsTool), typeof(RomaERP.Application.Assistant.Agent.AccountLedgerTool),
            typeof(RomaERP.Application.Assistant.Agent.CustomerStatementTool), typeof(RomaERP.Application.Assistant.Agent.VendorStatementTool),
            typeof(RomaERP.Application.Assistant.Agent.CustomerProfitabilityTool), typeof(RomaERP.Application.Assistant.Agent.ItemProfitabilityTool),
            typeof(RomaERP.Application.Assistant.Agent.PrepareNewCustomerTool), typeof(RomaERP.Application.Assistant.Agent.PrepareNewVendorTool),
            typeof(RomaERP.Application.Assistant.Agent.PrepareVendorPaymentTool), typeof(RomaERP.Application.Assistant.Agent.PreparePurchaseInvoiceTool),
            typeof(RomaERP.Application.Assistant.Agent.StockValuationTool), typeof(RomaERP.Application.Assistant.Agent.ReorderSuggestionsTool), typeof(RomaERP.Application.Assistant.Agent.WasteAnalysisTool),
            typeof(RomaERP.Application.Assistant.Agent.InventoryMovementTool), typeof(RomaERP.Application.Assistant.Agent.RecipeCostTool), typeof(RomaERP.Application.Assistant.Agent.RestaurantSalesTool),
            typeof(RomaERP.Application.Assistant.Agent.PendingApprovalsTool), typeof(RomaERP.Application.Assistant.Agent.HeadcountTool), typeof(RomaERP.Application.Assistant.Agent.PrepareJournalEntryTool),
        })
            services.AddScoped(typeof(RomaERP.Application.Assistant.Agent.IAgentTool), tool);
        services.AddScoped<IReservationService, ReservationService>();
        services.AddScoped<IMenuImportService, MenuImportService>();
        services.AddScoped<ILoyaltyService, LoyaltyService>();
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
