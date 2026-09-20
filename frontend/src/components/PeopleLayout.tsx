import type { ReactNode } from "react";
import { Link, NavLink } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
import { useTheme } from "../context/ThemeContext";
import { useLanguage } from "../i18n/LanguageContext";
import { usePortalManifest } from "../utils/pwa";
import { IconUsers, IconCheck, IconClock, IconBuilding, IconBriefcase, IconFile, IconWallet, IconDollar, IconBarChart, IconGrid, IconCalendar, IconSun, IconMoon } from "./icons";

/// <summary>A distinct-branded shell for the same HR pages the main app already has under /hr/* — same
/// components, same data, same login, just its own accent color and a top navigation bar (rather than the
/// side sidebar the main ERP and other portals use) scoped to HR so it reads as its own product ("ROMA
/// People") rather than a corner of the full ERP.</summary>
export default function PeopleLayout({ children }: { children: ReactNode }) {
  const { user, logout } = useAuth();
  const { t, lang, setLang } = useLanguage();
  const { theme, toggleTheme } = useTheme();
  usePortalManifest("people");

  const isAdmin = user?.roles.includes("Admin") ?? false;
  const isHr = isAdmin || user?.roles.includes("HR") || (user?.modules.includes("HR") ?? false);

  const selfServiceLinks = [
    { to: "/people", label: t.nav.dashboard, icon: <IconGrid />, end: true },
    { to: "/people/calendar", label: t.hr.hrCalendarTitle, icon: <IconCalendar /> },
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
    <div className="topnav-shell people-theme">
      <header className="topnav-header">
        <div className="topnav-brand-row">
          <span className="brand-text">{t.peopleAppName}</span>
          <div className="topnav-actions">
            <span className="topnav-user">{user?.fullName}</span>
            <button className="sidebar-toggle" onClick={toggleTheme} title={theme === "dark" ? t.lightMode : t.darkMode}>
              {theme === "dark" ? <IconSun /> : <IconMoon />}
            </button>
            <button className="btn btn-secondary btn-sm" onClick={() => setLang(lang === "ar" ? "en" : "ar")} title={t.language}>
              {lang === "ar" ? "EN" : "AR"}
            </button>
            <Link to="/" className="btn btn-secondary btn-sm">
              {t.backToRomaErp}
            </Link>
            <button className="btn btn-secondary btn-sm" onClick={logout}>
              {t.logout}
            </button>
          </div>
        </div>
        <nav className="topnav-links">
          {selfServiceLinks.map((item) => (
            <NavLink key={item.to} to={item.to} end={item.end} className={({ isActive }) => "topnav-link" + (isActive ? " active" : "")}>
              {item.icon}
              <span>{item.label}</span>
            </NavLink>
          ))}
          {isHr && (
            <>
              <span className="topnav-divider" />
              {managerLinks.map((item) => (
                <NavLink key={item.to} to={item.to} className={({ isActive }) => "topnav-link" + (isActive ? " active" : "")}>
                  {item.icon}
                  <span>{item.label}</span>
                </NavLink>
              ))}
            </>
          )}
        </nav>
      </header>
      <main className="topnav-content">{children}</main>
      <footer style={{ textAlign: "center", padding: "12px 0", fontSize: 12 }} className="text-muted">
        {t.poweredByRomaErp}
      </footer>
    </div>
  );
}
