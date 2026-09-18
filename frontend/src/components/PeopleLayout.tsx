import { type ReactNode } from "react";
import { Link, NavLink } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
import { useTheme } from "../context/ThemeContext";
import { useLanguage } from "../i18n/LanguageContext";
import { usePortalManifest } from "../utils/pwa";
import { IconUser, IconUsers, IconCheck, IconClock, IconBuilding, IconBriefcase, IconFile, IconWallet, IconDollar, IconBarChart, IconGrid, IconSun, IconMoon } from "./icons";

/// <summary>A distinct-branded shell for the same HR pages the main app already has under /hr/* — same
/// components, same data, same login, just its own accent color and a simplified nav scoped to HR so it
/// reads as its own product ("ROMA People") rather than a corner of the full ERP.</summary>
export default function PeopleLayout({ children }: { children: ReactNode }) {
  const { user, logout } = useAuth();
  const { t, lang, setLang } = useLanguage();
  const { theme, toggleTheme } = useTheme();
  usePortalManifest("people");

  const isAdmin = user?.roles.includes("Admin") ?? false;
  const isHr = isAdmin || user?.roles.includes("HR") || (user?.modules.includes("HR") ?? false);

  const selfServiceLinks = [
    { to: "/my-profile", label: t.nav.myProfile, icon: <IconUser /> },
    { to: "/people/attendance", label: t.hr.attendanceTitle, icon: <IconClock /> },
    { to: "/people/my-requests", label: t.hr.myRequests, icon: <IconCheck /> },
  ];

  const managerLinks = [
    { to: "/people/employees", label: t.nav.employees, icon: <IconUsers /> },
    { to: "/people/departments", label: t.nav.departments, icon: <IconBuilding /> },
    { to: "/people/positions", label: t.nav.positions, icon: <IconBriefcase /> },
    { to: "/people/employee-contracts", label: t.hr.employeeContractsTitle, icon: <IconFile /> },
    { to: "/people/salary-components", label: t.hr.salaryComponentsTitle, icon: <IconWallet /> },
    { to: "/people/payroll", label: t.nav.payroll, icon: <IconDollar /> },
    ...(isAdmin ? [{ to: "/people/payroll-settings", label: t.hr.payrollSettingsTitle, icon: <IconWallet /> }] : []),
    { to: "/people/labor-report", label: t.hr.laborReportTitle, icon: <IconBarChart /> },
    { to: "/people/work-locations", label: t.hr.workLocationsTitle, icon: <IconGrid /> },
    { to: "/people/employee-requests", label: t.hr.employeeRequestsTitle, icon: <IconCheck /> },
  ];

  return (
    <div className="app-shell people-theme">
      <aside className="sidebar">
        <div className="sidebar-brand">
          <span className="brand-text">{t.peopleAppName}</span>
          <div style={{ display: "flex", gap: 4 }}>
            <button className="sidebar-toggle" onClick={toggleTheme} title={theme === "dark" ? t.lightMode : t.darkMode}>
              {theme === "dark" ? <IconSun /> : <IconMoon />}
            </button>
          </div>
        </div>
        <div style={{ padding: "0 20px 12px" }}>
          <button className="btn btn-secondary btn-sm" onClick={() => setLang(lang === "ar" ? "en" : "ar")} title={t.language}>
            {lang === "ar" ? "EN" : "AR"}
          </button>
        </div>
        <div className="sidebar-scroll">
          <div className="sidebar-section-toggle" style={{ cursor: "default" }}>
            <span>{t.nav.general}</span>
          </div>
          {selfServiceLinks.map((item) => (
            <NavLink key={item.to} to={item.to} className={({ isActive }) => "sidebar-link" + (isActive ? " active" : "")}>
              <span className="sidebar-icon">{item.icon}</span>
              <span className="link-text">{item.label}</span>
            </NavLink>
          ))}
          {isHr && (
            <>
              <div className="sidebar-section-toggle" style={{ cursor: "default" }}>
                <span>{t.nav.hr}</span>
              </div>
              {managerLinks.map((item) => (
                <NavLink key={item.to} to={item.to} className={({ isActive }) => "sidebar-link" + (isActive ? " active" : "")}>
                  <span className="sidebar-icon">{item.icon}</span>
                  <span className="link-text">{item.label}</span>
                </NavLink>
              ))}
            </>
          )}
        </div>
        <div className="sidebar-footer">
          <div style={{ fontSize: 13, marginBottom: 8 }}>{user?.fullName}</div>
          <Link to="/" className="btn btn-secondary btn-sm" style={{ display: "block", textAlign: "center", marginBottom: 8, textDecoration: "none" }}>
            {t.backToRomaErp}
          </Link>
          <button className="btn btn-secondary btn-sm" onClick={logout}>
            {t.logout}
          </button>
        </div>
      </aside>
      <div className="main-column">
        <main className="main-content">{children}</main>
        <footer style={{ textAlign: "center", padding: "12px 0", fontSize: 12 }} className="text-muted">
          {t.poweredByRomaErp}
        </footer>
      </div>
    </div>
  );
}
