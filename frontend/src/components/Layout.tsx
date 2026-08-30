import { useEffect, useState, type ReactNode } from "react";
import { NavLink, useLocation } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
import { useTheme } from "../context/ThemeContext";
import { useLanguage } from "../i18n/LanguageContext";
import GlobalSearch from "./GlobalSearch";
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
  IconBuilding,
  IconBriefcase,
  IconDollar,
  IconArchive,
  IconSwap,
  IconEdit,
  IconShield,
  IconMenuToggle,
  IconChevron,
  IconSun,
  IconMoon,
} from "./icons";

const SIDEBAR_COLLAPSED_KEY = "romaerp:sidebarCollapsed";
const SIDEBAR_OPEN_SECTIONS_KEY = "romaerp:sidebarOpenSections";
const SIDEBAR_OPEN_SUBGROUPS_KEY = "romaerp:sidebarOpenSubgroups";

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
  const [collapsed, setCollapsed] = useState(() => localStorage.getItem(SIDEBAR_COLLAPSED_KEY) === "1");
  const [openSections, setOpenSections] = useState<Record<string, boolean>>(() => {
    try {
      return JSON.parse(localStorage.getItem(SIDEBAR_OPEN_SECTIONS_KEY) || "{}");
    } catch {
      return {};
    }
  });
  const [openSubGroups, setOpenSubGroups] = useState<Record<string, boolean>>(() => {
    try {
      return JSON.parse(localStorage.getItem(SIDEBAR_OPEN_SUBGROUPS_KEY) || "{}");
    } catch {
      return {};
    }
  });

  function toggleCollapsed() {
    setCollapsed((prev) => {
      const next = !prev;
      localStorage.setItem(SIDEBAR_COLLAPSED_KEY, next ? "1" : "0");
      return next;
    });
  }

  function toggleSection(section: string) {
    setOpenSections((prev) => {
      const next = { ...prev, [section]: !prev[section] };
      localStorage.setItem(SIDEBAR_OPEN_SECTIONS_KEY, JSON.stringify(next));
      return next;
    });
  }

  function toggleSubGroup(subGroup: string) {
    setOpenSubGroups((prev) => {
      const next = { ...prev, [subGroup]: !prev[subGroup] };
      localStorage.setItem(SIDEBAR_OPEN_SUBGROUPS_KEY, JSON.stringify(next));
      return next;
    });
  }

  function matchesPath(to: string) {
    if (!to) return false;
    const path = to.split("#")[0];
    return path === "/" ? location.pathname === "/" : location.pathname.startsWith(path);
  }

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

  const inventoryReportItems: NavSubGroupItem["subItems"] = [
    { to: "/inventory/reports/stock-valuation", label: t.inventory.stockValuationTitle },
    { to: "/inventory/reports/movement-analysis#slow", label: t.inventory.navSlowMovingItems },
    { to: "/inventory/reports/movement-analysis#dead", label: t.inventory.navDeadStock },
    { to: "/inventory/reports/movement-analysis#fast", label: t.inventory.navFastMovingItems },
    { to: "/inventory/reports/movement-analysis", label: t.inventory.navInventoryDays },
    { to: "/inventory/reports/movement-analysis", label: t.inventory.navInventoryTurnover },
    { to: "/inventory/physical-stock-counts", label: t.inventory.navStockCountVariance },
    { to: "/inventory/waste-entries", label: t.inventory.navWaste },
    { to: "/inventory/waste-entries", label: t.inventory.navDamaged },
    { to: "/inventory/reports/waste-analysis", label: t.inventory.wasteAnalysisTitle },
    { to: "", label: t.inventory.navAbnormalConsumption, comingSoon: true },
    { to: "/inventory/reports/movement-analysis#atrisk", label: t.inventory.navAtRiskItems },
    { to: "/inventory/reports/movement-analysis#excess", label: t.inventory.navExcessStock },
  ];

  const links: { section: string; items: NavItem[] }[] = [
    {
      section: t.nav.general,
      items: [
        { to: "/", label: t.nav.dashboard, icon: <IconGrid /> },
        { to: "/alerts", label: t.alerts.title, icon: <IconBell /> },
        { to: "/my-profile", label: t.nav.myProfile, icon: <IconUser /> },
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
        { to: "/accounting/fixed-assets", label: t.nav.fixedAssets, icon: <IconBox /> },
        { to: "/accounting/depreciation-runs", label: t.nav.depreciationRuns, icon: <IconTrendDown /> },
      ],
    },
    {
      section: t.nav.reports,
      items: [
        { to: "/accounting/executive-brief", label: t.accounting.executiveBriefTitle, icon: <IconBarChart /> },
        { to: "/accounting/comparisons", label: t.accounting.comparisonToolTitle, icon: <IconSwap /> },
        { to: "/accounting/smart-pricing", label: t.accounting.smartPricingTitle, icon: <IconDollar /> },
        { to: "/accounting/trial-balance", label: t.nav.trialBalance, icon: <IconBarChart /> },
        { to: "/accounting/income-statement", label: t.nav.incomeStatement, icon: <IconBarChart /> },
        { to: "/accounting/balance-sheet", label: t.nav.balanceSheet, icon: <IconFile /> },
        { to: "/accounting/cash-flow", label: t.nav.cashFlow, icon: <IconSwap /> },
        { to: "/accounting/cash-flow-intelligence", label: t.accounting.cashFlowIntelligenceTitle, icon: <IconSwap /> },
        { to: "/accounting/vat-summary", label: t.nav.vatSummary, icon: <IconEdit /> },
        { to: "/accounting/cost-center-analysis", label: t.nav.costCenterAnalysis, icon: <IconBarChart /> },
        { subGroup: t.nav.profitabilityReports, icon: <IconTrendDown />, subItems: profitabilityReportItems },
        { subGroup: t.inventory.costReportsGroup, icon: <IconTrendDown />, subItems: costReportItems },
        { to: "/accounting/break-even", label: t.nav.breakEven, icon: <IconDollar /> },
        { to: "/accounting/bottleneck", label: t.nav.bottleneck, icon: <IconClock /> },
        { to: "/accounting/forecast", label: t.accounting.forecastTitle, icon: <IconTrendDown /> },
        { to: "", label: t.nav.healthScoreSoon, icon: <IconShield />, comingSoon: true },
        { to: "", label: t.nav.whatIfSoon, icon: <IconRefresh />, comingSoon: true },
      ],
    },
    {
      section: t.nav.sales,
      items: [
        { to: "/sales/customers", label: t.nav.customers, icon: <IconUsers /> },
        { to: "/sales/invoices", label: t.nav.salesInvoices, icon: <IconFile /> },
        { to: "/sales/notes", label: t.nav.salesNotes, icon: <IconEdit /> },
        { to: "/sales/aging", label: t.nav.arAging, icon: <IconClock /> },
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
    {
      section: t.nav.hr,
      items: [
        { to: "/hr/departments", label: t.nav.departments, icon: <IconBuilding /> },
        { to: "/hr/positions", label: t.nav.positions, icon: <IconBriefcase /> },
        { to: "/hr/employees", label: t.nav.employees, icon: <IconUsers /> },
        { to: "/hr/payroll", label: t.nav.payroll, icon: <IconDollar /> },
      ],
    },
    {
      section: t.nav.inventory,
      items: [
        { to: "/inventory/items", label: t.nav.items, icon: <IconBox /> },
        { to: "/inventory/warehouses", label: t.nav.warehouses, icon: <IconArchive /> },
        { to: "/inventory/movements", label: t.nav.stockMovements, icon: <IconSwap /> },
        { to: "/inventory/physical-stock-counts", label: t.inventory.physicalStockCountsTitle, icon: <IconCheck /> },
        { to: "/inventory/waste-entries", label: t.inventory.wasteEntriesTitle, icon: <IconTrendDown /> },
        { subGroup: t.inventory.inventoryReports, icon: <IconBarChart />, subItems: inventoryReportItems },
      ],
    },
    {
      section: t.nav.restaurant,
      items: [
        { to: "/restaurant/pos", label: t.nav.restaurantPos, icon: <IconCart /> },
        { to: "/restaurant/tables", label: t.nav.restaurantTables, icon: <IconGrid /> },
        { to: "/restaurant/menu", label: t.nav.restaurantMenu, icon: <IconBook /> },
        { to: "/restaurant/delivery-reconciliation", label: t.inventory.deliveryReconciliationTitle, icon: <IconSwap /> },
      ],
    },
    ...(user?.roles.includes("Admin")
      ? [
          {
            section: t.nav.administration,
            items: [
              { to: "/users", label: t.nav.users, icon: <IconShield /> },
              { to: "/einvoicing", label: t.nav.eInvoicing, icon: <IconFile /> },
            ],
          },
        ]
      : []),
  ];

  useEffect(() => {
    let activeSection: string | null = null;
    let activeSubGroup: string | null = null;
    outer: for (const group of links) {
      for (const item of group.items) {
        if ("subGroup" in item) {
          if (item.subItems.some((sub) => matchesPath(sub.to))) {
            activeSection = group.section;
            activeSubGroup = item.subGroup;
            break outer;
          }
        } else if (matchesPath(item.to)) {
          activeSection = group.section;
          break outer;
        }
      }
    }
    if (activeSection && !openSections[activeSection]) {
      const section = activeSection;
      setOpenSections((prev) => {
        const next = { ...prev, [section]: true };
        localStorage.setItem(SIDEBAR_OPEN_SECTIONS_KEY, JSON.stringify(next));
        return next;
      });
    }
    if (activeSubGroup && !openSubGroups[activeSubGroup]) {
      const subGroup = activeSubGroup;
      setOpenSubGroups((prev) => {
        const next = { ...prev, [subGroup]: true };
        localStorage.setItem(SIDEBAR_OPEN_SUBGROUPS_KEY, JSON.stringify(next));
        return next;
      });
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [location.pathname]);

  return (
    <div className="app-shell">
      <aside className={"sidebar" + (collapsed ? " collapsed" : "")}>
        <div className="sidebar-brand">
          {!collapsed && <span className="brand-text">{t.appName}</span>}
          <div style={{ display: "flex", gap: 4 }}>
            <button className="sidebar-toggle" onClick={toggleTheme} title={theme === "dark" ? t.lightMode : t.darkMode}>
              {theme === "dark" ? <IconSun /> : <IconMoon />}
            </button>
            <button className="sidebar-toggle" onClick={toggleCollapsed} title={collapsed ? t.expandSidebar : t.collapseSidebar}>
              <IconMenuToggle collapsed={collapsed} />
            </button>
          </div>
        </div>
        {!collapsed && (
          <div style={{ padding: "0 20px 12px" }}>
            <button className="btn btn-secondary btn-sm" onClick={() => setLang(lang === "ar" ? "en" : "ar")} title={t.language}>
              {lang === "ar" ? "EN" : "AR"}
            </button>
          </div>
        )}
        <div className="sidebar-scroll">
          {links.map((group) => {
            const isOpen = collapsed || !!openSections[group.section];
            return (
              <div key={group.section}>
                {!collapsed && (
                  <button className="sidebar-section-toggle" onClick={() => toggleSection(group.section)}>
                    <span>{group.section}</span>
                    <IconChevron open={isOpen} />
                  </button>
                )}
                {isOpen &&
                  group.items.map((item) => {
                    if ("subGroup" in item) {
                      if (collapsed) return null;
                      const subOpen = !!openSubGroups[item.subGroup];
                      return (
                        <div key={item.subGroup}>
                          <button className="sidebar-subgroup-toggle" onClick={() => toggleSubGroup(item.subGroup)}>
                            <span className="sidebar-icon">{item.icon}</span>
                            <span className="link-text">{item.subGroup}</span>
                            <IconChevron open={subOpen} />
                          </button>
                          {subOpen &&
                            item.subItems.map((sub) =>
                              sub.comingSoon ? (
                                <span key={sub.label} className="sidebar-link sidebar-link-soon sidebar-sublink">
                                  <span className="link-text">
                                    {sub.label} <span className="sidebar-soon-badge">{t.accounting.comingSoon}</span>
                                  </span>
                                </span>
                              ) : (
                                <NavLink
                                  key={sub.label}
                                  to={sub.to}
                                  className={({ isActive }) => "sidebar-link sidebar-sublink" + (isActive ? " active" : "")}
                                >
                                  <span className="link-text">{sub.label}</span>
                                </NavLink>
                              )
                            )}
                        </div>
                      );
                    }
                    return item.comingSoon ? (
                      <span key={item.label} className="sidebar-link sidebar-link-soon" title={collapsed ? item.label : undefined}>
                        <span className="sidebar-icon">{item.icon}</span>
                        {!collapsed && (
                          <span className="link-text">
                            {item.label} <span className="sidebar-soon-badge">{t.accounting.comingSoon}</span>
                          </span>
                        )}
                      </span>
                    ) : (
                      <NavLink
                        key={item.to}
                        to={item.to}
                        end={item.to === "/"}
                        className={({ isActive }) => "sidebar-link" + (isActive ? " active" : "")}
                        title={collapsed ? item.label : undefined}
                      >
                        <span className="sidebar-icon">{item.icon}</span>
                        {!collapsed && <span className="link-text">{item.label}</span>}
                      </NavLink>
                    );
                  })}
              </div>
            );
          })}
        </div>
        <div className="sidebar-footer">
          {!collapsed && <div style={{ fontSize: 13, marginBottom: 8 }}>{user?.fullName}</div>}
          <button className="btn btn-secondary btn-sm" onClick={logout} title={collapsed ? t.logout : undefined}>
            {collapsed ? "⏻" : t.logout}
          </button>
        </div>
      </aside>
      <div className="main-column">
        <header className="topbar">
          <GlobalSearch />
        </header>
        <main className="main-content">{children}</main>
      </div>
    </div>
  );
}
