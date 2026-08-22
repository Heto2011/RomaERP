import type { ReactNode } from "react";
import { NavLink } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
import { useLanguage } from "../i18n/LanguageContext";

export default function Layout({ children }: { children: ReactNode }) {
  const { user, logout } = useAuth();
  const { t, lang, setLang } = useLanguage();

  const links = [
    { section: t.nav.general, items: [{ to: "/", label: t.nav.dashboard }] },
    {
      section: t.nav.assistant,
      items: [
        { to: "/assistant/chat", label: t.nav.assistantChat },
        { to: "/assistant/approvals", label: t.nav.assistantApprovals },
        { to: "/assistant/bank-reconciliation", label: t.nav.assistantBankReconciliation },
      ],
    },
    {
      section: t.nav.accounting,
      items: [
        { to: "/accounting/chart-of-accounts", label: t.nav.chartOfAccounts },
        { to: "/accounting/opening-balances", label: t.nav.openingBalances },
        { to: "/accounting/journal-entries", label: t.nav.journalEntries },
        { to: "/accounting/trial-balance", label: t.nav.trialBalance },
        { to: "/accounting/income-statement", label: t.nav.incomeStatement },
        { to: "/accounting/balance-sheet", label: t.nav.balanceSheet },
        { to: "/accounting/fiscal-periods", label: t.nav.fiscalPeriods },
      ],
    },
    {
      section: t.nav.sales,
      items: [
        { to: "/sales/customers", label: t.nav.customers },
        { to: "/sales/invoices", label: t.nav.salesInvoices },
      ],
    },
    {
      section: t.nav.purchasing,
      items: [
        { to: "/purchasing/vendors", label: t.nav.vendors },
        { to: "/purchasing/invoices", label: t.nav.purchaseInvoices },
      ],
    },
    {
      section: t.nav.hr,
      items: [
        { to: "/hr/departments", label: t.nav.departments },
        { to: "/hr/positions", label: t.nav.positions },
        { to: "/hr/employees", label: t.nav.employees },
        { to: "/hr/payroll", label: t.nav.payroll },
      ],
    },
    {
      section: t.nav.inventory,
      items: [
        { to: "/inventory/items", label: t.nav.items },
        { to: "/inventory/warehouses", label: t.nav.warehouses },
        { to: "/inventory/movements", label: t.nav.stockMovements },
      ],
    },
  ];

  return (
    <div className="app-shell">
      <aside className="sidebar">
        <div className="sidebar-brand" style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
          <span>{t.appName}</span>
          <button
            className="btn btn-secondary btn-sm"
            onClick={() => setLang(lang === "ar" ? "en" : "ar")}
            title={t.language}
          >
            {lang === "ar" ? "EN" : "AR"}
          </button>
        </div>
        {links.map((group) => (
          <div key={group.section}>
            <div className="sidebar-section">{group.section}</div>
            {group.items.map((item) => (
              <NavLink
                key={item.to}
                to={item.to}
                end={item.to === "/"}
                className={({ isActive }) => "sidebar-link" + (isActive ? " active" : "")}
              >
                {item.label}
              </NavLink>
            ))}
          </div>
        ))}
        <div className="sidebar-footer">
          <div style={{ fontSize: 13, marginBottom: 8 }}>{user?.fullName}</div>
          <button className="btn btn-secondary btn-sm" onClick={logout}>
            {t.logout}
          </button>
        </div>
      </aside>
      <main className="main-content">{children}</main>
    </div>
  );
}
