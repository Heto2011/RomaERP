import { Navigate, Route, Routes } from "react-router-dom";
import { useAuth } from "./context/AuthContext";
import Layout from "./components/Layout";
import Login from "./pages/Login";
import StartTrial from "./pages/StartTrial";
import DemoTenantsPage from "./pages/system/DemoTenants";
import Dashboard from "./pages/Dashboard";
import Users from "./pages/Users";
import MyProfile from "./pages/MyProfile";
import AlertsPage from "./pages/Alerts";
import AuditLogPage from "./pages/AuditLog";
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
import Payroll from "./pages/hr/Payroll";
import LaborReportPage from "./pages/hr/LaborReport";
import Items from "./pages/inventory/Items";
import Warehouses from "./pages/inventory/Warehouses";
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
import DeliveryReconciliationPage from "./pages/restaurant/DeliveryReconciliation";
import RestaurantPOS from "./pages/restaurant/RestaurantPOS";

function ProtectedRoute({ children }: { children: React.ReactNode }) {
  const { user } = useAuth();
  if (!user) return <Navigate to="/login" replace />;
  return <Layout>{children}</Layout>;
}

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<Login />} />
      <Route path="/start-trial" element={<StartTrial />} />
      <Route path="/system/demo-tenants" element={<DemoTenantsPage />} />
      <Route path="/" element={<ProtectedRoute><Dashboard /></ProtectedRoute>} />
      <Route path="/users" element={<ProtectedRoute><Users /></ProtectedRoute>} />
      <Route path="/my-profile" element={<ProtectedRoute><MyProfile /></ProtectedRoute>} />
      <Route path="/alerts" element={<ProtectedRoute><AlertsPage /></ProtectedRoute>} />
      <Route path="/audit-log" element={<ProtectedRoute><AuditLogPage /></ProtectedRoute>} />
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
      <Route path="/hr/labor-report" element={<ProtectedRoute><LaborReportPage /></ProtectedRoute>} />
      <Route path="/restaurant/tables" element={<ProtectedRoute><RestaurantTables /></ProtectedRoute>} />
      <Route path="/restaurant/menu" element={<ProtectedRoute><RestaurantMenu /></ProtectedRoute>} />
      <Route path="/restaurant/delivery-reconciliation" element={<ProtectedRoute><DeliveryReconciliationPage /></ProtectedRoute>} />
      <Route path="/restaurant/pos" element={<ProtectedRoute><RestaurantPOS /></ProtectedRoute>} />
      <Route path="/inventory/items" element={<ProtectedRoute><Items /></ProtectedRoute>} />
      <Route path="/inventory/warehouses" element={<ProtectedRoute><Warehouses /></ProtectedRoute>} />
      <Route path="/inventory/movements" element={<ProtectedRoute><StockMovements /></ProtectedRoute>} />
      <Route path="/inventory/reports/stock-valuation" element={<ProtectedRoute><StockValuationPage /></ProtectedRoute>} />
      <Route path="/inventory/reports/movement-analysis" element={<ProtectedRoute><InventoryMovementPage /></ProtectedRoute>} />
      <Route path="/inventory/reports/purchase-price-variance" element={<ProtectedRoute><PurchasePriceVariancePage /></ProtectedRoute>} />
      <Route path="/inventory/reports/recipe-cost" element={<ProtectedRoute><RecipeCostPage /></ProtectedRoute>} />
      <Route path="/inventory/reports/waste-analysis" element={<ProtectedRoute><WasteAnalysisPage /></ProtectedRoute>} />
      <Route path="/inventory/physical-stock-counts" element={<ProtectedRoute><PhysicalStockCountsPage /></ProtectedRoute>} />
      <Route path="/inventory/waste-entries" element={<ProtectedRoute><WasteEntriesPage /></ProtectedRoute>} />
      <Route path="/accounting/hidden-profit" element={<ProtectedRoute><HiddenProfitPage /></ProtectedRoute>} />
      <Route path="/accounting/executive-brief" element={<ProtectedRoute><ExecutiveBriefPage /></ProtectedRoute>} />
      <Route path="/accounting/comparisons" element={<ProtectedRoute><ComparisonToolPage /></ProtectedRoute>} />
      <Route path="/accounting/smart-pricing" element={<ProtectedRoute><SmartPricingPage /></ProtectedRoute>} />
      <Route path="/accounting/forecast" element={<ProtectedRoute><ForecastPage /></ProtectedRoute>} />
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
