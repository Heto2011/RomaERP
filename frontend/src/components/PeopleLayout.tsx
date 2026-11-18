import { useState, type ReactNode } from "react";
import { Link, NavLink } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
import { ProductScope } from "../api/types";
import { useTheme } from "../context/ThemeContext";
import { useLanguage } from "../i18n/LanguageContext";
import { usePortalManifest } from "../utils/pwa";
import { IconUsers, IconCheck, IconClock, IconBuilding, IconBriefcase, IconFile, IconWallet, IconDollar, IconBarChart, IconGrid, IconCalendar, IconSun, IconMoon, IconShield, IconChat, IconMenuToggle } from "./icons";

/// <summary>A distinct-branded shell for the same HR pages the main app already has under /hr/* — same
/// components, same data, same login, just its own accent color and a side navigation menu (like the restaurant portal) scoped to HR so it reads as its own product ("ROMA
/// People") rather than a corner of the full ERP.</summary>
export default function PeopleLayout({ children }: { children: ReactNode }) {
  const { user, logout } = useAuth();
  const { t, lang, setLang } = useLanguage();
  const { theme, toggleTheme } = useTheme();
  usePortalManifest("people");
  const [mobileNavOpen, setMobileNavOpen] = useState(false);
  const closeMobileNav = () => setMobileNavOpen(false);

  // A tenant that signed up for ROMA People only has no ERP to go back to — the portal stands alone for them.
  const peopleOnly = user?.productScope === ProductScope.PeopleOnly;
  const isAdmin = user?.roles.includes("Admin") ?? false;
  const isHrManager = isAdmin || (user?.roles.includes("HR") ?? false);
  const isHr = isAdmin || user?.roles.includes("HR") || (user?.modules.includes("HR") ?? false);

  const selfServiceLinks = [
    { to: "/people", label: t.nav.dashboard, icon: <IconGrid />, end: true },
    { to: "/people/attendance", label: t.hr.attendanceTitle, icon: <IconClock /> },
    { to: "/people/my-requests", label: t.hr.myRequests, icon: <IconCheck /> },
    { to: "/people/my-schedule", label: t.hr.mySchedule, icon: <IconCalendar /> },
    { to: "/people/my-reviews", label: t.hr.myReviews, icon: <IconCheck /> },
    { to: "/people/my-surveys", label: t.hr.mySurveys, icon: <IconChat /> },
  ];

  const managerLinks = [
    { to: "/people/calendar", label: t.hr.hrCalendarTitle, icon: <IconCalendar /> },
    { to: "/people/employees", label: t.nav.employees, icon: <IconUsers /> },
    ...(isHrManager ? [{ to: "/people/users", label: t.nav.users, icon: <IconShield /> }] : []),
    { to: "/people/departments", label: t.nav.departments, icon: <IconBuilding /> },
    { to: "/people/positions", label: t.nav.positions, icon: <IconBriefcase /> },
    { to: "/people/employee-contracts", label: t.hr.employeeContractsTitle, icon: <IconFile /> },
    { to: "/people/salary-components", label: t.hr.salaryComponentsTitle, icon: <IconWallet /> },
    { to: "/people/payroll", label: t.nav.payroll, icon: <IconDollar /> },
    ...(isHrManager ? [{ to: "/people/payroll-settings", label: t.hr.payrollSettingsTitle, icon: <IconWallet /> }] : []),
    { to: "/people/employee-debts", label: t.nav.employeeDebts, icon: <IconWallet /> },
    { to: "/people/labor-report", label: t.hr.laborReportTitle, icon: <IconBarChart /> },
    { to: "/people/work-locations", label: t.hr.workLocationsTitle, icon: <IconGrid /> },
    { to: "/people/shifts", label: t.hr.shiftsTitle, icon: <IconCalendar /> },
    { to: "/people/recruitment", label: t.hr.recruitmentTitle, icon: <IconUsers /> },
    { to: "/people/performance", label: t.hr.performanceTitle, icon: <IconBarChart /> },
    { to: "/people/surveys", label: t.hr.surveysTitle, icon: <IconChat /> },
    { to: "/people/employee-requests", label: t.hr.employeeRequestsTitle, icon: <IconCheck /> },
    ...(isAdmin ? [{ to: "/people/subscription", label: t.mySubscription.title, icon: <IconDollar /> }] : []),
  ];

  const sideLink = (item: { to: string; label: string; icon: ReactNode; end?: boolean }) => (
    <NavLink key={item.to} to={item.to} end={item.end} onClick={closeMobileNav} className={({ isActive }) => "sidebar-link" + (isActive ? " active" : "")}>
      <span className="sidebar-icon">{item.icon}</span>
      <span className="link-text">{item.label}</span>
    </NavLink>
  );

  return (
    <div className="app-shell people-theme">
      {mobileNavOpen && <div className="mobile-nav-backdrop" onClick={closeMobileNav} />}
      <aside className={"sidebar" + (mobileNavOpen ? " mobile-open" : "")}>
        <div className="sidebar-brand">
          <span className="brand-text" style={{ display: "inline-flex", alignItems: "center", gap: 10 }}>
            <img src="/roma-logo.png" alt="" width={28} height={28} style={{ borderRadius: 8 }} />
            {t.peopleAppName}
          </span>
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
          {selfServiceLinks.map(sideLink)}
          {isHr && (
            <>
              <div className="text-muted" style={{ padding: "14px 20px 4px", fontSize: 12, fontWeight: 700 }}>{t.nav.hr}</div>
              {managerLinks.map(sideLink)}
            </>
          )}
        </div>
        <div className="sidebar-footer">
          <div style={{ fontSize: 13, marginBottom: 8 }}>{user?.fullName}</div>
          {!peopleOnly && (
            <Link to="/" className="btn btn-secondary btn-sm" style={{ display: "block", textAlign: "center", marginBottom: 8, textDecoration: "none" }}>
              {t.backToRomaErp}
            </Link>
          )}
          <button className="btn btn-secondary btn-sm" onClick={logout}>
            {t.logout}
          </button>
        </div>
      </aside>
      <div className="main-column">
        <button
          className="btn btn-secondary btn-sm mobile-nav-toggle"
          style={{ margin: "10px 16px 0" }}
          onClick={() => setMobileNavOpen((open) => !open)}
        >
          <IconMenuToggle collapsed={!mobileNavOpen} />
        </button>
        <main className="main-content">{children}</main>
        <footer style={{ textAlign: "center", padding: "12px 0", fontSize: 12 }} className="text-muted">
          {t.poweredByRomaErp}
          {localStorage.getItem("companyCode") && (
            <span dir="ltr" style={{ display: "block", marginTop: 4 }}>
              {t.login.companyCode}: {localStorage.getItem("companyCode")}
            </span>
          )}
        </footer>
      </div>
    </div>
  );
}
