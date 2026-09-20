import { Navigate, Route, Routes } from "react-router-dom";
import { useAuth } from "./context/AuthContext";
import { ProductScope } from "./api/types";
import Layout from "./components/Layout";
import PeopleLayout from "./components/PeopleLayout";
import RestaurantPortalLayout from "./components/RestaurantPortalLayout";
import InventoryPortalLayout from "./components/InventoryPortalLayout";
import Login from "./pages/Login";
import PeopleLogin from "./pages/PeopleLogin";
import RestaurantLogin from "./pages/RestaurantLogin";
import InventoryLogin from "./pages/InventoryLogin";
import StartTrial from "./pages/StartTrial";
import PosLogin from "./pages/restaurant/PosLogin";
import DemoTenantsPage from "./pages/system/DemoTenants";
import DataDeletionPage from "./pages/system/DataDeletion";
import SubscriptionsPage from "./pages/system/Subscriptions";
import SupportTicketsPage from "./pages/system/SupportTickets";
import Dashboard from "./pages/Dashboard";
import Users from "./pages/Users";
import MyProfile from "./pages/MyProfile";
import AlertsPage from "./pages/Alerts";
import SupportPage from "./pages/Support";
import AuditLogPage from "./pages/AuditLog";
import LoginHistoryPage from "./pages/LoginHistory";
import EInvoicing from "./pages/EInvoicing";
import ChartOfAccounts from "./pages/accounting/ChartOfAccounts";
import OpeningBalances from "./pages/accounting/OpeningBalances";
import JournalEntries from "./pages/accounting/JournalEntries";
import TrialBalance from "./pages/accounting/TrialBalance";
import IncomeStatementPage from "./pages/accounting/IncomeStatement";
import BalanceSheetPage from "./pages/accounting/BalanceSheet";
import CostCenterAnalysisPage from "./pages/accounting/CostCenterAnalysis";
import CashFlowPage from "./pages/accounting/CashFlow";
import CashFlowIntelligencePage from "./pages/accounting/CashFlowIntelligence";
import VatSummaryPage from "./pages/accounting/VatSummary";
import MoneyFlowPage from "./pages/accounting/MoneyFlow";
import ItemProfitabilityPage from "./pages/accounting/ItemProfitability";
import CustomerProfitabilityPage from "./pages/accounting/CustomerProfitability";
import BranchProfitabilityPage from "./pages/accounting/BranchProfitability";
import SalesChannelProfitabilityPage from "./pages/accounting/SalesChannelProfitability";
import MarginAnalysisPage from "./pages/accounting/MarginAnalysis";
import BreakEvenPage from "./pages/accounting/BreakEven";
import WhatIfCalculatorPage from "./pages/accounting/WhatIfCalculator";
import BottleneckPage from "./pages/accounting/Bottleneck";
import FiscalPeriods from "./pages/accounting/FiscalPeriods";
import ExchangeRates from "./pages/accounting/ExchangeRates";
import Budgets from "./pages/accounting/Budgets";
import BankFeedReconciliation from "./pages/accounting/BankFeedReconciliation";
import FixedAssets from "./pages/accounting/FixedAssets";
import DepreciationRuns from "./pages/accounting/DepreciationRuns";
import Customers from "./pages/sales/Customers";
import SalesInvoices from "./pages/sales/SalesInvoices";
import SalesNotes from "./pages/sales/SalesNotes";
import ArAging from "./pages/sales/ArAging";
import Vendors from "./pages/purchasing/Vendors";
import PurchaseInvoices from "./pages/purchasing/PurchaseInvoices";
import ApAging from "./pages/purchasing/ApAging";
import Departments from "./pages/hr/Departments";
import Positions from "./pages/hr/Positions";
import Employees from "./pages/hr/Employees";
import WorkLocations from "./pages/hr/WorkLocations";
import Attendance from "./pages/hr/Attendance";
import MyRequests from "./pages/hr/MyRequests";
import PeopleDashboard from "./pages/hr/PeopleDashboard";
import PeopleCalendar from "./pages/hr/PeopleCalendar";
import EmployeeRequestsAdmin from "./pages/hr/EmployeeRequestsAdmin";
import Payroll from "./pages/hr/Payroll";
import PayrollSettingsPage from "./pages/hr/PayrollSettings";
import EmployeeContracts from "./pages/hr/EmployeeContracts";
import LaborReportPage from "./pages/hr/LaborReport";
import SalaryComponentsPage from "./pages/hr/SalaryComponents";
import Items from "./pages/inventory/Items";
import Warehouses from "./pages/inventory/Warehouses";
import Manufacturing from "./pages/inventory/Manufacturing";
import ExpiringStock from "./pages/inventory/ExpiringStock";
import StockMovements from "./pages/inventory/StockMovements";
import StockValuationPage from "./pages/inventory/StockValuation";
import InventoryMovementPage from "./pages/inventory/InventoryMovement";
import PurchasePriceVariancePage from "./pages/inventory/PurchasePriceVariance";
import RecipeCostPage from "./pages/inventory/RecipeCost";
import PhysicalStockCountsPage from "./pages/inventory/PhysicalStockCounts";
import WasteEntriesPage from "./pages/inventory/WasteEntries";
import WasteAnalysisPage from "./pages/inventory/WasteAnalysis";
import HiddenProfitPage from "./pages/accounting/HiddenProfit";
import ExecutiveBriefPage from "./pages/accounting/ExecutiveBrief";
import ComparisonToolPage from "./pages/accounting/ComparisonTool";
import SmartPricingPage from "./pages/accounting/SmartPricing";
import ForecastPage from "./pages/accounting/Forecast";
import AiAssistant from "./pages/assistant/AiAssistant";
import ExpenseApprovals from "./pages/assistant/ExpenseApprovals";
import BankReconciliation from "./pages/assistant/BankReconciliation";
import RestaurantTables from "./pages/restaurant/RestaurantTables";
import RestaurantMenu from "./pages/restaurant/RestaurantMenu";
import PurchaseReceivingPage from "./pages/restaurant/PurchaseReceiving";
import DeliveryReconciliationPage from "./pages/restaurant/DeliveryReconciliation";
import DeliveryPlatforms from "./pages/restaurant/DeliveryPlatforms";
import RestaurantPOS from "./pages/restaurant/RestaurantPOS";
import KitchenDisplay from "./pages/restaurant/KitchenDisplay";

function ProtectedRoute({ children, layout = true, loginPath = "/login" }: { children: React.ReactNode; layout?: boolean; loginPath?: string }) {
  const { user } = useAuth();
  if (!user) return <Navigate to={loginPath} replace />;
  return layout ? <Layout>{children}</Layout> : <>{children}</>;
}

function PeopleRoute({ children }: { children: React.ReactNode }) {
  const { user } = useAuth();
  if (!user) return <Navigate to="/people-login" replace />;
  return <PeopleLayout>{children}</PeopleLayout>;
}

function RestaurantPortalRoute({ children }: { children: React.ReactNode }) {
  const { user } = useAuth();
  if (!user) return <Navigate to="/restaurant-login" replace />;
  return <RestaurantPortalLayout>{children}</RestaurantPortalLayout>;
}

function InventoryPortalRoute({ children }: { children: React.ReactNode }) {
  const { user } = useAuth();
  if (!user) return <Navigate to="/inventory-login" replace />;
  return <InventoryPortalLayout>{children}</InventoryPortalLayout>;
}

// A user whose only role is Employee is a cashier — send them straight to the POS screen instead of
// the general dashboard, matching the restricted sidebar Layout shows them. A tenant that signed up for
// ROMA People only (not the full suite) lands straight in the People portal instead of the ERP dashboard.
function HomeRoute() {
  const { user } = useAuth();
  const isCashierOnly = user?.roles.length === 1 && user.roles[0] === "Employee";
  if (isCashierOnly) return <Navigate to="/restaurant/pos" replace />;
  if (user?.productScope === ProductScope.PeopleOnly) return <Navigate to="/people" replace />;
  return <Dashboard />;
}

// A logged-out visitor hitting the bare domain sees the public marketing/pricing page (a plain static
// file, not part of this SPA) instead of being dropped straight on a login form — a real browser
// navigation, not React Router, since the target lives outside the app's routes entirely.
function RootRoute() {
  const { user } = useAuth();
  if (!user) {
    window.location.replace("/pricing.html");
    return null;
  }
  return (
    <ProtectedRoute>
      <HomeRoute />
    </ProtectedRoute>
  );
}

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<Login />} />
      <Route path="/people-login" element={<PeopleLogin />} />
      <Route path="/restaurant-login" element={<RestaurantLogin />} />
      <Route path="/inventory-login" element={<InventoryLogin />} />
      <Route path="/start-trial" element={<StartTrial />} />
      <Route path="/pos-login" element={<PosLogin />} />
      <Route path="/system/demo-tenants" element={<DemoTenantsPage />} />
      <Route path="/system/data-deletion" element={<DataDeletionPage />} />
      <Route path="/system/subscriptions" element={<SubscriptionsPage />} />
      <Route path="/system/support-tickets" element={<SupportTicketsPage />} />
      <Route path="/" element={<RootRoute />} />
      <Route path="/users" element={<ProtectedRoute><Users /></ProtectedRoute>} />
      <Route path="/my-profile" element={<ProtectedRoute><MyProfile /></ProtectedRoute>} />
      <Route path="/alerts" element={<ProtectedRoute><AlertsPage /></ProtectedRoute>} />
      <Route path="/support" element={<ProtectedRoute><SupportPage /></ProtectedRoute>} />
      <Route path="/audit-log" element={<ProtectedRoute><AuditLogPage /></ProtectedRoute>} />
      <Route path="/login-history" element={<ProtectedRoute><LoginHistoryPage /></ProtectedRoute>} />
      <Route path="/einvoicing" element={<ProtectedRoute><EInvoicing /></ProtectedRoute>} />
      <Route path="/assistant/chat" element={<ProtectedRoute><AiAssistant /></ProtectedRoute>} />
      <Route path="/assistant/approvals" element={<ProtectedRoute><ExpenseApprovals /></ProtectedRoute>} />
      <Route path="/assistant/bank-reconciliation" element={<ProtectedRoute><BankReconciliation /></ProtectedRoute>} />
      <Route path="/accounting/chart-of-accounts" element={<ProtectedRoute><ChartOfAccounts /></ProtectedRoute>} />
      <Route path="/accounting/opening-balances" element={<ProtectedRoute><OpeningBalances /></ProtectedRoute>} />
      <Route path="/accounting/journal-entries" element={<ProtectedRoute><JournalEntries /></ProtectedRoute>} />
      <Route path="/accounting/trial-balance" element={<ProtectedRoute><TrialBalance /></ProtectedRoute>} />
      <Route path="/accounting/income-statement" element={<ProtectedRoute><IncomeStatementPage /></ProtectedRoute>} />
      <Route path="/accounting/balance-sheet" element={<ProtectedRoute><BalanceSheetPage /></ProtectedRoute>} />
      <Route path="/accounting/cost-center-analysis" element={<ProtectedRoute><CostCenterAnalysisPage /></ProtectedRoute>} />
      <Route path="/accounting/cash-flow" element={<ProtectedRoute><CashFlowPage /></ProtectedRoute>} />
      <Route path="/accounting/cash-flow-intelligence" element={<ProtectedRoute><CashFlowIntelligencePage /></ProtectedRoute>} />
      <Route path="/accounting/vat-summary" element={<ProtectedRoute><VatSummaryPage /></ProtectedRoute>} />
      <Route path="/accounting/money-flow" element={<ProtectedRoute><MoneyFlowPage /></ProtectedRoute>} />
      <Route path="/accounting/item-profitability" element={<ProtectedRoute><ItemProfitabilityPage /></ProtectedRoute>} />
      <Route path="/accounting/customer-profitability" element={<ProtectedRoute><CustomerProfitabilityPage /></ProtectedRoute>} />
      <Route path="/accounting/branch-profitability" element={<ProtectedRoute><BranchProfitabilityPage /></ProtectedRoute>} />
      <Route path="/accounting/sales-channel-profitability" element={<ProtectedRoute><SalesChannelProfitabilityPage /></ProtectedRoute>} />
      <Route path="/accounting/margin-analysis" element={<ProtectedRoute><MarginAnalysisPage /></ProtectedRoute>} />
      <Route path="/accounting/break-even" element={<ProtectedRoute><BreakEvenPage /></ProtectedRoute>} />
      <Route path="/accounting/what-if" element={<ProtectedRoute><WhatIfCalculatorPage /></ProtectedRoute>} />
      <Route path="/accounting/bottleneck" element={<ProtectedRoute><BottleneckPage /></ProtectedRoute>} />
      <Route path="/accounting/fiscal-periods" element={<ProtectedRoute><FiscalPeriods /></ProtectedRoute>} />
      <Route path="/accounting/exchange-rates" element={<ProtectedRoute><ExchangeRates /></ProtectedRoute>} />
      <Route path="/accounting/budgets" element={<ProtectedRoute><Budgets /></ProtectedRoute>} />
      <Route path="/accounting/bank-feed-reconciliation" element={<ProtectedRoute><BankFeedReconciliation /></ProtectedRoute>} />
      <Route path="/accounting/fixed-assets" element={<ProtectedRoute><FixedAssets /></ProtectedRoute>} />
      <Route path="/accounting/depreciation-runs" element={<ProtectedRoute><DepreciationRuns /></ProtectedRoute>} />
      <Route path="/sales/customers" element={<ProtectedRoute><Customers /></ProtectedRoute>} />
      <Route path="/sales/invoices" element={<ProtectedRoute><SalesInvoices /></ProtectedRoute>} />
      <Route path="/sales/notes" element={<ProtectedRoute><SalesNotes /></ProtectedRoute>} />
      <Route path="/sales/aging" element={<ProtectedRoute><ArAging /></ProtectedRoute>} />
      <Route path="/purchasing/vendors" element={<ProtectedRoute><Vendors /></ProtectedRoute>} />
      <Route path="/purchasing/invoices" element={<ProtectedRoute><PurchaseInvoices /></ProtectedRoute>} />
      <Route path="/purchasing/aging" element={<ProtectedRoute><ApAging /></ProtectedRoute>} />
      <Route path="/hr/departments" element={<ProtectedRoute><Departments /></ProtectedRoute>} />
      <Route path="/hr/positions" element={<ProtectedRoute><Positions /></ProtectedRoute>} />
      <Route path="/hr/employees" element={<ProtectedRoute><Employees /></ProtectedRoute>} />
      <Route path="/hr/payroll" element={<ProtectedRoute><Payroll /></ProtectedRoute>} />
      <Route path="/hr/payroll-settings" element={<ProtectedRoute><PayrollSettingsPage /></ProtectedRoute>} />
      <Route path="/hr/employee-contracts" element={<ProtectedRoute><EmployeeContracts /></ProtectedRoute>} />
      <Route path="/hr/salary-components" element={<ProtectedRoute><SalaryComponentsPage /></ProtectedRoute>} />
      <Route path="/hr/labor-report" element={<ProtectedRoute><LaborReportPage /></ProtectedRoute>} />
      <Route path="/hr/work-locations" element={<ProtectedRoute><WorkLocations /></ProtectedRoute>} />
      <Route path="/hr/attendance" element={<ProtectedRoute><Attendance /></ProtectedRoute>} />
      <Route path="/hr/my-requests" element={<ProtectedRoute><MyRequests /></ProtectedRoute>} />
      <Route path="/hr/employee-requests" element={<ProtectedRoute><EmployeeRequestsAdmin /></ProtectedRoute>} />
      <Route path="/people" element={<PeopleRoute><PeopleDashboard /></PeopleRoute>} />
      <Route path="/people/calendar" element={<PeopleRoute><PeopleCalendar /></PeopleRoute>} />
      <Route path="/people/attendance" element={<PeopleRoute><Attendance /></PeopleRoute>} />
      <Route path="/people/my-requests" element={<PeopleRoute><MyRequests /></PeopleRoute>} />
      <Route path="/people/departments" element={<PeopleRoute><Departments /></PeopleRoute>} />
      <Route path="/people/positions" element={<PeopleRoute><Positions /></PeopleRoute>} />
      <Route path="/people/employees" element={<PeopleRoute><Employees /></PeopleRoute>} />
      <Route path="/people/work-locations" element={<PeopleRoute><WorkLocations /></PeopleRoute>} />
      <Route path="/people/payroll" element={<PeopleRoute><Payroll /></PeopleRoute>} />
      <Route path="/people/payroll-settings" element={<PeopleRoute><PayrollSettingsPage /></PeopleRoute>} />
      <Route path="/people/employee-contracts" element={<PeopleRoute><EmployeeContracts /></PeopleRoute>} />
      <Route path="/people/salary-components" element={<PeopleRoute><SalaryComponentsPage /></PeopleRoute>} />
      <Route path="/people/labor-report" element={<PeopleRoute><LaborReportPage /></PeopleRoute>} />
      <Route path="/people/employee-requests" element={<PeopleRoute><EmployeeRequestsAdmin /></PeopleRoute>} />
      <Route path="/restaurant/tables" element={<ProtectedRoute><RestaurantTables /></ProtectedRoute>} />
      <Route path="/restaurant/menu" element={<ProtectedRoute><RestaurantMenu /></ProtectedRoute>} />
      <Route path="/restaurant/purchase-receiving" element={<ProtectedRoute><PurchaseReceivingPage /></ProtectedRoute>} />
      <Route path="/restaurant/delivery-reconciliation" element={<ProtectedRoute><DeliveryReconciliationPage /></ProtectedRoute>} />
      <Route path="/restaurant/delivery-platforms" element={<ProtectedRoute><DeliveryPlatforms /></ProtectedRoute>} />
      <Route path="/restaurant/pos" element={<ProtectedRoute layout={false} loginPath="/pos-login"><RestaurantPOS /></ProtectedRoute>} />
      <Route path="/restaurant/kitchen" element={<ProtectedRoute><KitchenDisplay /></ProtectedRoute>} />
      <Route path="/restaurant-portal" element={<RestaurantPortalRoute><RestaurantTables /></RestaurantPortalRoute>} />
      <Route path="/restaurant-portal/tables" element={<RestaurantPortalRoute><RestaurantTables /></RestaurantPortalRoute>} />
      <Route path="/restaurant-portal/menu" element={<RestaurantPortalRoute><RestaurantMenu /></RestaurantPortalRoute>} />
      <Route path="/restaurant-portal/kitchen" element={<RestaurantPortalRoute><KitchenDisplay /></RestaurantPortalRoute>} />
      <Route path="/restaurant-portal/purchase-receiving" element={<RestaurantPortalRoute><PurchaseReceivingPage /></RestaurantPortalRoute>} />
      <Route path="/restaurant-portal/delivery-reconciliation" element={<RestaurantPortalRoute><DeliveryReconciliationPage /></RestaurantPortalRoute>} />
      <Route path="/restaurant-portal/delivery-platforms" element={<RestaurantPortalRoute><DeliveryPlatforms /></RestaurantPortalRoute>} />
      <Route path="/inventory/items" element={<ProtectedRoute><Items /></ProtectedRoute>} />
      <Route path="/inventory/warehouses" element={<ProtectedRoute><Warehouses /></ProtectedRoute>} />
      <Route path="/inventory/manufacturing" element={<ProtectedRoute><Manufacturing /></ProtectedRoute>} />
      <Route path="/inventory/expiring-stock" element={<ProtectedRoute><ExpiringStock /></ProtectedRoute>} />
      <Route path="/inventory/movements" element={<ProtectedRoute><StockMovements /></ProtectedRoute>} />
      <Route path="/inventory/reports/stock-valuation" element={<ProtectedRoute><StockValuationPage /></ProtectedRoute>} />
      <Route path="/inventory/reports/movement-analysis" element={<ProtectedRoute><InventoryMovementPage /></ProtectedRoute>} />
      <Route path="/inventory/reports/purchase-price-variance" element={<ProtectedRoute><PurchasePriceVariancePage /></ProtectedRoute>} />
      <Route path="/inventory/reports/recipe-cost" element={<ProtectedRoute><RecipeCostPage /></ProtectedRoute>} />
      <Route path="/inventory/reports/waste-analysis" element={<ProtectedRoute><WasteAnalysisPage /></ProtectedRoute>} />
      <Route path="/inventory/physical-stock-counts" element={<ProtectedRoute><PhysicalStockCountsPage /></ProtectedRoute>} />
      <Route path="/inventory/waste-entries" element={<ProtectedRoute><WasteEntriesPage /></ProtectedRoute>} />
      <Route path="/inventory-portal" element={<InventoryPortalRoute><Items /></InventoryPortalRoute>} />
      <Route path="/inventory-portal/items" element={<InventoryPortalRoute><Items /></InventoryPortalRoute>} />
      <Route path="/inventory-portal/warehouses" element={<InventoryPortalRoute><Warehouses /></InventoryPortalRoute>} />
      <Route path="/inventory-portal/manufacturing" element={<InventoryPortalRoute><Manufacturing /></InventoryPortalRoute>} />
      <Route path="/inventory-portal/expiring-stock" element={<InventoryPortalRoute><ExpiringStock /></InventoryPortalRoute>} />
      <Route path="/inventory-portal/movements" element={<InventoryPortalRoute><StockMovements /></InventoryPortalRoute>} />
      <Route path="/inventory-portal/physical-stock-counts" element={<InventoryPortalRoute><PhysicalStockCountsPage /></InventoryPortalRoute>} />
      <Route path="/inventory-portal/waste-entries" element={<InventoryPortalRoute><WasteEntriesPage /></InventoryPortalRoute>} />
      <Route path="/inventory-portal/reports/stock-valuation" element={<InventoryPortalRoute><StockValuationPage /></InventoryPortalRoute>} />
      <Route path="/inventory-portal/reports/movement-analysis" element={<InventoryPortalRoute><InventoryMovementPage /></InventoryPortalRoute>} />
      <Route path="/inventory-portal/reports/purchase-price-variance" element={<InventoryPortalRoute><PurchasePriceVariancePage /></InventoryPortalRoute>} />
      <Route path="/inventory-portal/reports/recipe-cost" element={<InventoryPortalRoute><RecipeCostPage /></InventoryPortalRoute>} />
      <Route path="/inventory-portal/reports/waste-analysis" element={<InventoryPortalRoute><WasteAnalysisPage /></InventoryPortalRoute>} />
      <Route path="/accounting/hidden-profit" element={<ProtectedRoute><HiddenProfitPage /></ProtectedRoute>} />
      <Route path="/accounting/executive-brief" element={<ProtectedRoute><ExecutiveBriefPage /></ProtectedRoute>} />
      <Route path="/accounting/comparisons" element={<ProtectedRoute><ComparisonToolPage /></ProtectedRoute>} />
      <Route path="/accounting/smart-pricing" element={<ProtectedRoute><SmartPricingPage /></ProtectedRoute>} />
      <Route path="/accounting/forecast" element={<ProtectedRoute><ForecastPage /></ProtectedRoute>} />
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
