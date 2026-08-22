import type { ReactNode } from "react";
import { NavLink } from "react-router-dom";
import { useAuth } from "../context/AuthContext";

const links = [
  { section: "عام", items: [{ to: "/", label: "لوحة التحكم" }] },
  {
    section: "المحاسبة",
    items: [
      { to: "/accounting/chart-of-accounts", label: "شجرة الحسابات" },
      { to: "/accounting/journal-entries", label: "القيود اليومية" },
      { to: "/accounting/trial-balance", label: "ميزان المراجعة" },
      { to: "/accounting/income-statement", label: "قائمة الدخل" },
      { to: "/accounting/balance-sheet", label: "المركز المالي" },
      { to: "/accounting/fiscal-periods", label: "إقفال الفترات" },
    ],
  },
  {
    section: "الموارد البشرية",
    items: [
      { to: "/hr/departments", label: "الأقسام" },
      { to: "/hr/positions", label: "الوظائف" },
      { to: "/hr/employees", label: "الموظفون" },
      { to: "/hr/payroll", label: "الرواتب" },
    ],
  },
  {
    section: "المخزون",
    items: [
      { to: "/inventory/items", label: "الأصناف" },
      { to: "/inventory/warehouses", label: "المخازن" },
      { to: "/inventory/movements", label: "حركات المخزون" },
    ],
  },
];

export default function Layout({ children }: { children: ReactNode }) {
  const { user, logout } = useAuth();

  return (
    <div className="app-shell">
      <aside className="sidebar">
        <div className="sidebar-brand">RomaERP</div>
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
            تسجيل الخروج
          </button>
        </div>
      </aside>
      <main className="main-content">{children}</main>
    </div>
  );
}
