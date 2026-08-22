import { Navigate, Route, Routes } from "react-router-dom";
import { useAuth } from "./context/AuthContext";
import Layout from "./components/Layout";
import Login from "./pages/Login";
import Dashboard from "./pages/Dashboard";
import ChartOfAccounts from "./pages/accounting/ChartOfAccounts";
import OpeningBalances from "./pages/accounting/OpeningBalances";
import JournalEntries from "./pages/accounting/JournalEntries";
import TrialBalance from "./pages/accounting/TrialBalance";
import IncomeStatementPage from "./pages/accounting/IncomeStatement";
import BalanceSheetPage from "./pages/accounting/BalanceSheet";
import FiscalPeriods from "./pages/accounting/FiscalPeriods";
import Customers from "./pages/sales/Customers";
import SalesInvoices from "./pages/sales/SalesInvoices";
import ArAging from "./pages/sales/ArAging";
import Vendors from "./pages/purchasing/Vendors";
import PurchaseInvoices from "./pages/purchasing/PurchaseInvoices";
import ApAging from "./pages/purchasing/ApAging";
import Departments from "./pages/hr/Departments";
import Positions from "./pages/hr/Positions";
import Employees from "./pages/hr/Employees";
import Payroll from "./pages/hr/Payroll";
import Items from "./pages/inventory/Items";
import Warehouses from "./pages/inventory/Warehouses";
import StockMovements from "./pages/inventory/StockMovements";
import AiAssistant from "./pages/assistant/AiAssistant";
import ExpenseApprovals from "./pages/assistant/ExpenseApprovals";
import BankReconciliation from "./pages/assistant/BankReconciliation";

function ProtectedRoute({ children }: { children: React.ReactNode }) {
  const { user } = useAuth();
  if (!user) return <Navigate to="/login" replace />;
  return <Layout>{children}</Layout>;
}

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<Login />} />
      <Route path="/" element={<ProtectedRoute><Dashboard /></ProtectedRoute>} />
      <Route path="/assistant/chat" element={<ProtectedRoute><AiAssistant /></ProtectedRoute>} />
      <Route path="/assistant/approvals" element={<ProtectedRoute><ExpenseApprovals /></ProtectedRoute>} />
      <Route path="/assistant/bank-reconciliation" element={<ProtectedRoute><BankReconciliation /></ProtectedRoute>} />
      <Route path="/accounting/chart-of-accounts" element={<ProtectedRoute><ChartOfAccounts /></ProtectedRoute>} />
      <Route path="/accounting/opening-balances" element={<ProtectedRoute><OpeningBalances /></ProtectedRoute>} />
      <Route path="/accounting/journal-entries" element={<ProtectedRoute><JournalEntries /></ProtectedRoute>} />
      <Route path="/accounting/trial-balance" element={<ProtectedRoute><TrialBalance /></ProtectedRoute>} />
      <Route path="/accounting/income-statement" element={<ProtectedRoute><IncomeStatementPage /></ProtectedRoute>} />
      <Route path="/accounting/balance-sheet" element={<ProtectedRoute><BalanceSheetPage /></ProtectedRoute>} />
      <Route path="/accounting/fiscal-periods" element={<ProtectedRoute><FiscalPeriods /></ProtectedRoute>} />
      <Route path="/sales/customers" element={<ProtectedRoute><Customers /></ProtectedRoute>} />
      <Route path="/sales/invoices" element={<ProtectedRoute><SalesInvoices /></ProtectedRoute>} />
      <Route path="/sales/aging" element={<ProtectedRoute><ArAging /></ProtectedRoute>} />
      <Route path="/purchasing/vendors" element={<ProtectedRoute><Vendors /></ProtectedRoute>} />
      <Route path="/purchasing/invoices" element={<ProtectedRoute><PurchaseInvoices /></ProtectedRoute>} />
      <Route path="/purchasing/aging" element={<ProtectedRoute><ApAging /></ProtectedRoute>} />
      <Route path="/hr/departments" element={<ProtectedRoute><Departments /></ProtectedRoute>} />
      <Route path="/hr/positions" element={<ProtectedRoute><Positions /></ProtectedRoute>} />
      <Route path="/hr/employees" element={<ProtectedRoute><Employees /></ProtectedRoute>} />
      <Route path="/hr/payroll" element={<ProtectedRoute><Payroll /></ProtectedRoute>} />
      <Route path="/inventory/items" element={<ProtectedRoute><Items /></ProtectedRoute>} />
      <Route path="/inventory/warehouses" element={<ProtectedRoute><Warehouses /></ProtectedRoute>} />
      <Route path="/inventory/movements" element={<ProtectedRoute><StockMovements /></ProtectedRoute>} />
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
