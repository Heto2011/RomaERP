import { Navigate, Route, Routes } from "react-router-dom";
import { useAuth } from "./context/AuthContext";
import Layout from "./components/Layout";
import Login from "./pages/Login";
import Dashboard from "./pages/Dashboard";
import ChartOfAccounts from "./pages/accounting/ChartOfAccounts";
import JournalEntries from "./pages/accounting/JournalEntries";
import TrialBalance from "./pages/accounting/TrialBalance";
import Departments from "./pages/hr/Departments";
import Positions from "./pages/hr/Positions";
import Employees from "./pages/hr/Employees";
import Payroll from "./pages/hr/Payroll";

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
      <Route path="/accounting/chart-of-accounts" element={<ProtectedRoute><ChartOfAccounts /></ProtectedRoute>} />
      <Route path="/accounting/journal-entries" element={<ProtectedRoute><JournalEntries /></ProtectedRoute>} />
      <Route path="/accounting/trial-balance" element={<ProtectedRoute><TrialBalance /></ProtectedRoute>} />
      <Route path="/hr/departments" element={<ProtectedRoute><Departments /></ProtectedRoute>} />
      <Route path="/hr/positions" element={<ProtectedRoute><Positions /></ProtectedRoute>} />
      <Route path="/hr/employees" element={<ProtectedRoute><Employees /></ProtectedRoute>} />
      <Route path="/hr/payroll" element={<ProtectedRoute><Payroll /></ProtectedRoute>} />
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
