import { useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import { NavLink, useLocation } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
import { ProductScope } from "../api/types";
import { useTheme } from "../context/ThemeContext";
import { useLanguage } from "../i18n/LanguageContext";
import GlobalSearch from "./GlobalSearch";
import UsageIndicator from "./UsageIndicator";
import {
  IconBell,
  IconGrid,
  IconUser,
  IconUsers,
  IconChat,
  IconCheck,
  IconRefresh,
  IconList,
  IconWallet,
  IconBook,
  IconBarChart,
  IconFile,
  IconCalendar,
  IconBox,
  IconTrendDown,
  IconClock,
  IconTruck,
  IconCart,
  IconSwap,
  IconEdit,
  IconShield,
  IconChevron,
  IconSun,
  IconMoon,
  IconDollar,
} from "./icons";

interface NavLeafItem {
  to: string;
  label: string;
  icon: ReactNode;
  comingSoon?: boolean;
}
interface NavSubGroupItem {
  subGroup: string;
  icon: ReactNode;
  subItems: { to: string; label: string; comingSoon?: boolean }[];
}
type NavItem = NavLeafItem | NavSubGroupItem;

export default function Layout({ children }: { children: ReactNode }) {
  const { user, logout } = useAuth();
  const { t, lang, setLang } = useLanguage();
  const { theme, toggleTheme } = useTheme();
  const location = useLocation();
  const navRef = useRef<HTMLDivElement>(null);
  const [openDropdown, setOpenDropdown] = useState<string | null>(null);

  function matchesPath(to: string) {
    if (!to) return false;
    const path = to.split("#")[0];
    return path === "/" ? location.pathname === "/" : location.pathname.startsWith(path);
  }

  useEffect(() => {
    setOpenDropdown(null);
  }, [location.pathname]);

  useEffect(() => {
    if (!openDropdown) return;
    function onDocumentClick(e: MouseEvent) {
      if (navRef.current && !navRef.current.contains(e.target as Node)) setOpenDropdown(null);
    }
    document.addEventListener("mousedown", onDocumentClick);
    return () => document.removeEventListener("mousedown", onDocumentClick);
  }, [openDropdown]);

  const executiveReportItems: NavSubGroupItem["subItems"] = [
    { to: "/accounting/executive-brief", label: t.accounting.executiveBriefTitle },
    { to: "/accounting/comparisons", label: t.accounting.comparisonToolTitle },
    { to: "/accounting/smart-pricing", label: t.accounting.smartPricingTitle },
  ];

  const financialStatementItems: NavSubGroupItem["subItems"] = [
    { to: "/accounting/trial-balance", label: t.nav.trialBalance },
    { to: "/accounting/income-statement", label: t.nav.incomeStatement },
    { to: "/accounting/balance-sheet", label: t.nav.balanceSheet },
    { to: "/accounting/cash-flow", label: t.nav.cashFlow },
    { to: "/accounting/vat-summary", label: t.nav.vatSummary },
    { to: "/accounting/cost-center-analysis", label: t.nav.costCenterAnalysis },
  ];

  const planningForecastItems: NavSubGroupItem["subItems"] = [
    { to: "/accounting/cash-flow-intelligence", label: t.accounting.cashFlowIntelligenceTitle },
    { to: "/accounting/break-even", label: t.nav.breakEven },
    { to: "/accounting/what-if", label: t.accounting.whatIfTitle },
    { to: "/accounting/bottleneck", label: t.nav.bottleneck },
    { to: "/accounting/forecast", label: t.accounting.forecastTitle },
    { to: "", label: t.nav.healthScoreSoon, comingSoon: true },
    { to: "", label: t.nav.whatIfSoon, comingSoon: true },
  ];

  const profitabilityReportItems: NavSubGroupItem["subItems"] = [
    { to: "/accounting/money-flow", label: t.nav.moneyFlow },
    { to: "/accounting/hidden-profit", label: t.accounting.hiddenProfitTitle },
    { to: "/accounting/margin-analysis#real-profit", label: t.accounting.realProfitTitle },
    { to: "/accounting/item-profitability", label: t.nav.itemProfitability },
    { to: "/accounting/customer-profitability", label: t.nav.customerProfitability },
    { to: "/accounting/branch-profitability", label: t.nav.branchProfitability },
    { to: "/accounting/sales-channel-profitability", label: t.nav.salesChannelProfitability },
    { to: "/accounting/margin-analysis#gross-margin", label: t.accounting.grossMarginRatio },
    { to: "/accounting/margin-analysis#net-margin", label: t.accounting.netMarginRatio },
    { to: "/accounting/margin-analysis#contribution-margin", label: t.accounting.contributionMarginRatio },
    { to: "/accounting/item-profitability#top-winners", label: t.accounting.topWinners },
    { to: "/accounting/item-profitability#top-losers", label: t.accounting.topLosers },
  ];

  const costReportItems: NavSubGroupItem["subItems"] = [
    { to: "", label: t.inventory.navActualVsStandardCost, comingSoon: true },
    { to: "/inventory/reports/purchase-price-variance", label: t.inventory.purchasePriceVarianceTitle },
    { to: "", label: t.inventory.navQuantityVariance, comingSoon: true },
    { to: "", label: t.inventory.navMaterialCostVariance, comingSoon: true },
    { to: "", label: t.inventory.navLaborCostVariance, comingSoon: true },
    { to: "", label: t.inventory.navOverheadVariance, comingSoon: true },
    { to: "/inventory/reports/stock-valuation", label: t.inventory.navCostPerUnit },
    { to: "/inventory/reports/recipe-cost", label: t.inventory.navRealProductCost },
    { to: "/inventory/reports/recipe-cost", label: t.inventory.navRecipeCostSlash },
    { to: "", label: t.inventory.navRecipeVsActualUsage, comingSoon: true },
  ];

  // Inventory lives under Sales (same pages as the standalone ROMA Inventory portal).
  const inventorySubItems: NavSubGroupItem["subItems"] = [
    { to: "/inventory/items", label: t.nav.items },
    { to: "/inventory/warehouses", label: t.nav.warehouses },
    { to: "/inventory/movements", label: t.nav.stockMovements },
    { to: "/inventory/physical-stock-counts", label: t.inventory.physicalStockCountsTitle },
    { to: "/inventory/waste-entries", label: t.inventory.wasteEntriesTitle },
    { to: "/inventory/manufacturing", label: t.inventory.manufacturingTitle },
    { to: "/inventory/expiring-stock", label: t.inventory.expiringStockTitle },
    { to: "/inventory/reports/stock-valuation", label: t.inventory.stockValuationTitle },
    { to: "/inventory/reports/movement-analysis", label: t.inventory.inventoryReports },
    { to: "/inventory/reports/waste-analysis", label: t.inventory.wasteAnalysisTitle },
  ];

  // HR management lives under Sales too (same pages as the Roma HR portal); employees only see their own pages in General.
  const canManageHr = (user?.roles.includes("Admin") ?? false) || (user?.modules.includes("HR") ?? false);
  const hrSubItems: NavSubGroupItem["subItems"] = [
    { to: "/hr/employees", label: t.nav.employees },
    { to: "/hr/departments", label: t.nav.departments },
    { to: "/hr/positions", label: t.nav.positions },
    { to: "/hr/employee-contracts", label: t.hr.employeeContractsTitle },
    { to: "/hr/salary-components", label: t.hr.salaryComponentsTitle },
    { to: "/hr/payroll", label: t.nav.payroll },
    { to: "/hr/payroll-settings", label: t.hr.payrollSettingsTitle },
    { to: "/hr/labor-report", label: t.hr.laborReportTitle },
    { to: "/hr/work-locations", label: t.hr.workLocationsTitle },
    { to: "/hr/employee-requests", label: t.hr.employeeRequestsTitle },
  ];

  // A user whose only role is Employee is a cashier — the nav collapses to just what a cashier needs,
  // so they never see accounting/HR/purchasing data even if they bypass the standalone POS login and
  // land in the regular app shell.
  const isCashierOnly = user?.roles.length === 1 && user.roles[0] === "Employee";

  const cashierLinks: { section: string; items: NavItem[] }[] = [
    {
      section: t.nav.general,
      items: [
        { to: "/my-profile", label: t.nav.myProfile, icon: <IconUser /> },
        { to: "/hr/attendance", label: t.hr.attendanceTitle, icon: <IconClock /> },
        { to: "/hr/my-requests", label: t.hr.myRequests, icon: <IconCheck /> },
      ],
    },
    {
      section: t.nav.restaurant,
      items: [{ to: "/restaurant/pos", label: t.nav.restaurantPos, icon: <IconCart /> }],
    },
  ];

  const openPeoplePortalLink: NavLeafItem = { to: "/people", label: t.openPeoplePortal, icon: <IconUsers /> };
  const openRestaurantPortalLink: NavLeafItem = { to: "/restaurant-portal", label: t.openRestaurantPortal, icon: <IconCart /> };
  const openInventoryPortalLink: NavLeafItem = { to: "/inventory-portal", label: t.openInventoryPortal, icon: <IconBox /> };

  const fullLinks: { section: string; items: NavItem[] }[] = [
    {
      section: t.nav.general,
      items: [
        { to: "/", label: t.nav.dashboard, icon: <IconGrid /> },
        { to: "/alerts", label: t.alerts.title, icon: <IconBell /> },
        { to: "/support", label: t.support.title, icon: <IconChat /> },
        ...(user?.roles.includes("Admin") ? [{ to: "/my-subscription", label: t.mySubscription.title, icon: <IconDollar /> }] : []),
        { to: "/my-profile", label: t.nav.myProfile, icon: <IconUser /> },
        { to: "/hr/attendance", label: t.hr.attendanceTitle, icon: <IconClock /> },
        { to: "/hr/my-requests", label: t.hr.myRequests, icon: <IconCheck /> },
        ...(user?.roles.includes("Admin") ? [openPeoplePortalLink, openRestaurantPortalLink, openInventoryPortalLink] : []),
      ],
    },
    {
      section: t.nav.assistant,
      items: [
        { to: "/assistant/chat", label: t.nav.assistantChat, icon: <IconChat /> },
        { to: "/assistant/approvals", label: t.nav.assistantApprovals, icon: <IconCheck /> },
        { to: "/assistant/bank-reconciliation", label: t.nav.assistantBankReconciliation, icon: <IconRefresh /> },
      ],
    },
    {
      section: t.nav.accounting,
      items: [
        { to: "/accounting/chart-of-accounts", label: t.nav.chartOfAccounts, icon: <IconList /> },
        { to: "/accounting/opening-balances", label: t.nav.openingBalances, icon: <IconWallet /> },
        { to: "/accounting/journal-entries", label: t.nav.journalEntries, icon: <IconBook /> },
        { to: "/accounting/fiscal-periods", label: t.nav.fiscalPeriods, icon: <IconCalendar /> },
        { to: "/accounting/exchange-rates", label: t.nav.exchangeRates, icon: <IconSwap /> },
        { to: "/accounting/budgets", label: t.nav.budgets, icon: <IconBarChart /> },
        { to: "/accounting/bank-feed-reconciliation", label: t.nav.bankFeedReconciliation, icon: <IconRefresh /> },
        { to: "/accounting/fixed-assets", label: t.nav.fixedAssets, icon: <IconBox /> },
        { to: "/accounting/depreciation-runs", label: t.nav.depreciationRuns, icon: <IconTrendDown /> },
      ],
    },
    {
      section: t.nav.reports,
      items: [
        { subGroup: t.nav.executiveReportsGroup, icon: <IconBarChart />, subItems: executiveReportItems },
        { subGroup: t.nav.financialStatementsGroup, icon: <IconFile />, subItems: financialStatementItems },
        { subGroup: t.nav.profitabilityReports, icon: <IconTrendDown />, subItems: profitabilityReportItems },
        { subGroup: t.inventory.costReportsGroup, icon: <IconTrendDown />, subItems: costReportItems },
        { subGroup: t.nav.planningForecastGroup, icon: <IconClock />, subItems: planningForecastItems },
      ],
    },
    {
      section: t.nav.sales,
      items: [
        { to: "/sales/customers", label: t.nav.customers, icon: <IconUsers /> },
        { to: "/sales/invoices", label: t.nav.salesInvoices, icon: <IconFile /> },
        { to: "/sales/notes", label: t.nav.salesNotes, icon: <IconEdit /> },
        { to: "/sales/aging", label: t.nav.arAging, icon: <IconClock /> },
        { subGroup: t.nav.inventory, icon: <IconBox />, subItems: inventorySubItems },
        ...(canManageHr ? [{ subGroup: t.nav.hr, icon: <IconUsers />, subItems: hrSubItems }] : []),
      ],
    },
    {
      section: t.nav.purchasing,
      items: [
        { to: "/purchasing/vendors", label: t.nav.vendors, icon: <IconTruck /> },
        { to: "/purchasing/invoices", label: t.nav.purchaseInvoices, icon: <IconCart /> },
        { to: "/purchasing/aging", label: t.nav.apAging, icon: <IconClock /> },
      ],
    },
    ...(user?.roles.includes("Admin")
      ? [
          {
            section: t.nav.administration,
            items: [
              { to: "/users", label: t.nav.users, icon: <IconShield /> },
              { to: "/einvoicing", label: t.nav.eInvoicing, icon: <IconFile /> },
              { to: "/audit-log", label: t.nav.auditLog, icon: <IconList /> },
              { to: "/login-history", label: t.nav.loginHistory, icon: <IconList /> },
            ],
          },
        ]
      : []),
  ];

  // A tenant that signed up for ROMA People only never gets the full ERP nav, even for its own Admin
  // (who would otherwise bypass every module check below) — they only ever land here by typing a
  // direct URL, since HomeRoute/PeopleLogin already send them straight to /people.
  const isPeopleOnly = user?.productScope === ProductScope.PeopleOnly;
  const peopleOnlyAllowedSections = new Set([t.nav.general, t.nav.administration]);

  // Each of these sections requires either the base role that's always had it, or the matching
  // per-user "module" grant (see ModulePermissions on the backend) — Admin always sees everything.
  const isAdmin = user?.roles.includes("Admin") ?? false;
  const sectionAccess: Record<string, { module: string; fallbackRoles: string[] }> = {
    [t.nav.accounting]: { module: "Accounting", fallbackRoles: ["Accountant"] },
    [t.nav.reports]: { module: "Reports", fallbackRoles: ["Accountant"] },
    [t.nav.sales]: { module: "Sales", fallbackRoles: ["Accountant"] },
    [t.nav.purchasing]: { module: "Purchasing", fallbackRoles: ["Accountant"] },
    [t.nav.restaurant]: { module: "POS", fallbackRoles: ["Accountant", "Employee"] },
  };
  function canSeeSection(section: string) {
    if (isPeopleOnly && !peopleOnlyAllowedSections.has(section)) return false;
    const access = sectionAccess[section];
    if (!access) return true;
    return isAdmin || access.fallbackRoles.some((r) => user?.roles.includes(r)) || (user?.modules.includes(access.module) ?? false);
  }

  const links = (isCashierOnly ? cashierLinks : fullLinks).filter((group) => canSeeSection(group.section));

  const activeSection = useMemo(() => {
    for (const group of links) {
      for (const item of group.items) {
        if ("subGroup" in item) {
          if (item.subItems.some((sub) => matchesPath(sub.to))) return group.section;
        } else if (matchesPath(item.to)) {
          return group.section;
        }
      }
    }
    return null;
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [links, location.pathname]);

  return (
    <div className="topnav-shell">
      <header className="topnav-header">
        <div className="topnav-brand-row">
          <span className="brand-text" style={{ display: "inline-flex", alignItems: "center", gap: 10 }}>
            <img src="/roma-logo.png" alt="" width={28} height={28} style={{ borderRadius: 8 }} />
            {t.appName}
          </span>
          <div className="topnav-search-wrap">
            <GlobalSearch />
          </div>
          <div className="topnav-actions">
            <UsageIndicator />
            <span className="topnav-user">{user?.fullName}</span>
            <button className="sidebar-toggle" onClick={toggleTheme} title={theme === "dark" ? t.lightMode : t.darkMode}>
              {theme === "dark" ? <IconSun /> : <IconMoon />}
            </button>
            <button className="btn btn-secondary btn-sm" onClick={() => setLang(lang === "ar" ? "en" : "ar")} title={t.language}>
              {lang === "ar" ? "EN" : "AR"}
            </button>
            <button className="btn btn-secondary btn-sm" onClick={logout}>
              {t.logout}
            </button>
          </div>
        </div>
        <div ref={navRef}>
          <nav className="topnav-links">
            {links.map((group) => (
              <button
                key={group.section}
                className={"topnav-link" + (activeSection === group.section ? " active" : "")}
                onClick={() => setOpenDropdown((cur) => (cur === group.section ? null : group.section))}
              >
                <span>{group.section}</span>
                <IconChevron open={openDropdown === group.section} />
              </button>
            ))}
          </nav>
          {links
            .filter((group) => group.section === openDropdown)
            .map((group) => {
              const flatItems = group.items.filter((item): item is NavLeafItem => !("subGroup" in item));
              const subGroups = group.items.filter((item): item is NavSubGroupItem => "subGroup" in item);
              return (
                <div key={group.section} className="topnav-panel">
                  {flatItems.length > 0 && (
                    <div className="dash-app-grid">
                      {flatItems.map((item) =>
                        item.comingSoon ? (
                          <span key={item.label} className="dash-app-tile topnav-tile-soon">
                            <span className="dash-app-tile-icon">{item.icon}</span>
                            <span>{item.label}</span>
                            <span className="sidebar-soon-badge">{t.accounting.comingSoon}</span>
                          </span>
                        ) : (
                          <NavLink key={item.to} to={item.to} end={item.to === "/"} className="dash-app-tile">
                            <span className="dash-app-tile-icon">{item.icon}</span>
                            <span>{item.label}</span>
                          </NavLink>
                        )
                      )}
                    </div>
                  )}
                  {subGroups.map((group) => (
                    <div key={group.subGroup} className="topnav-tile-group">
                      <div className="topnav-tile-group-title">{group.subGroup}</div>
                      <div className="dash-app-grid">
                        {group.subItems.map((sub) =>
                          sub.comingSoon ? (
                            <span key={sub.label} className="dash-app-tile topnav-tile-soon">
                              <span className="dash-app-tile-icon">{group.icon}</span>
                              <span>{sub.label}</span>
                              <span className="sidebar-soon-badge">{t.accounting.comingSoon}</span>
                            </span>
                          ) : (
                            <NavLink key={sub.label} to={sub.to} className="dash-app-tile">
                              <span className="dash-app-tile-icon">{group.icon}</span>
                              <span>{sub.label}</span>
                            </NavLink>
                          )
                        )}
                      </div>
                    </div>
                  ))}
                </div>
              );
            })}
        </div>
      </header>
      <main className="topnav-content">{children}</main>
    </div>
  );
}
