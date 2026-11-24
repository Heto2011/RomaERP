import { useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import { NavLink, useLocation, useNavigate } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
import { ProductScope } from "../api/types";
import { hiddenSectionsFor, SHOW_ALL_KEY, type NavSectionKey } from "../businessType";
import { useTheme } from "../context/ThemeContext";
import { useLanguage } from "../i18n/LanguageContext";
import HelpButton from "./HelpButton";
import FeedbackButton from "./FeedbackButton";
import GlobalSearch from "./GlobalSearch";
import UsageIndicator from "./UsageIndicator";
import { isEmployeeWithExtraModules, isPureCashier } from "../utils/userHome";
import {
  IconArchive,
  IconBuilding,
  IconBriefcase,
  IconBell,
  IconGrid,
  IconStar,
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
  const navigate = useNavigate();
  const navRef = useRef<HTMLDivElement>(null);
  const iconsRef = useRef<HTMLDivElement>(null);
  // Someone given only the Purchasing module records invoices; vendors, aging and statements stay with approvers.
  const canApprovePurchases = user?.roles.some((r) => r === "Admin" || r === "Accountant") ?? false;
  const [openDropdown, setOpenDropdown] = useState<string | null>(null);
  // When a tap reaches the page as a mouse (a phone in "desktop site" mode), it first "hovers" the tab, which opens its list, and then clicks it.
  // That click must not close the list straight away.
  const hoverOpenedAt = useRef(0);

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
      const target = e.target as Node;
      if (navRef.current && !navRef.current.contains(target) && !(iconsRef.current && iconsRef.current.contains(target))) setOpenDropdown(null);
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
    { to: "/accounting/general-ledger", label: t.nav.generalLedger },
    { to: "/accounting/income-statement", label: t.nav.incomeStatement },
    { to: "/accounting/balance-sheet", label: t.nav.balanceSheet },
    { to: "/accounting/cash-flow", label: t.nav.cashFlow },
    { to: "/accounting/vat-summary", label: t.nav.vatSummary },
    { to: "/accounting/cost-center-analysis", label: t.nav.costCenterAnalysis },
    { to: "/accounting/branch-profit", label: t.nav.branchProfit },
    { to: "/accounting/cost-allocation", label: t.nav.costAllocation },
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
    { to: "/inventory/reports/stock-valuation", label: t.inventory.navCostPerUnit },
    { to: "/inventory/reports/recipe-cost", label: t.inventory.navRealProductCost },
    { to: "/inventory/reports/purchase-price-variance", label: t.inventory.purchasePriceVarianceTitle },
  ];

  const inventoryReportItems: NavSubGroupItem["subItems"] = [
    { to: "/inventory/reports/stock-valuation", label: t.inventory.stockValuationTitle },
    { to: "/inventory/reports/reorder-suggestions", label: t.inventory.reorderSuggestionsTitle },
    { to: "/inventory/reports/movement-analysis", label: t.inventory.inventoryReports },
    { to: "/inventory/reports/waste-analysis", label: t.inventory.wasteAnalysisTitle },
  ];

  const canManageHr = (user?.roles.includes("Admin") ?? false) || (user?.modules.includes("HR") ?? false);

  // A user whose only role is Employee is a cashier — the nav collapses to just what a cashier needs,
  // so they never see accounting/HR/purchasing data even if they bypass the standalone POS login and
  // land in the regular app shell.
  const isCashierOnly = isPureCashier(user);
  // An Employee given extra modules (e.g. Purchasing) keeps the cashier nav and also sees the sections they were granted.
  const isEmployeeWithExtras = isEmployeeWithExtraModules(user);

  const cashierLinks: { section: string; items: NavItem[] }[] = [
    {
      section: t.nav.general,
      items: [
        { to: "/my-profile", label: t.nav.myProfile, icon: <IconUser /> },
        { to: "/hr/attendance", label: t.hr.attendanceTitle, icon: <IconClock /> },
        { to: "/hr/my-requests", label: t.hr.myRequests, icon: <IconCheck /> },
        { to: "/hr/my-schedule", label: t.hr.mySchedule, icon: <IconCalendar /> },
        { to: "/hr/my-reviews", label: t.hr.myReviews, icon: <IconCheck /> },
        { to: "/hr/my-surveys", label: t.hr.mySurveys, icon: <IconChat /> },
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
        { to: "/hr/my-schedule", label: t.hr.mySchedule, icon: <IconCalendar /> },
        { to: "/hr/my-reviews", label: t.hr.myReviews, icon: <IconCheck /> },
        { to: "/hr/my-surveys", label: t.hr.mySurveys, icon: <IconChat /> },
        ...(user?.roles.includes("Admin") ? [openPeoplePortalLink, openRestaurantPortalLink, openInventoryPortalLink] : []),
      ],
    },
    {
      section: t.nav.assistant,
      items: [
        { to: "/assistant/actions", label: lang === "ar" ? "اطلب من المساعد" : "Ask the assistant to do it", icon: <IconChat /> },
        { to: "/assistant/chat", label: t.nav.assistantChat, icon: <IconChat /> },
        { to: "/assistant/approvals", label: t.nav.assistantApprovals, icon: <IconCheck /> },
        { to: "/assistant/recurring", label: t.nav.assistantRecurring, icon: <IconRefresh /> },
        ...(user?.roles.includes("Admin") ? [{ to: "/assistant/policies", label: t.nav.assistantPolicies, icon: <IconCheck /> }] : []),
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
      ],
    },
    {
      // Review: what an accountant checks and adjusts after the day-to-day work (as in the system customers are used to).
      section: t.nav.review,
      items: [
        { subGroup: lang === "ar" ? "الفحص والإقفال" : "Checks and closing", icon: <IconCheck />, subItems: [
          { to: "/review/close-checklist", label: lang === "ar" ? "قائمة إقفال الشهر" : "Month-end close checklist" },
          { to: "/review/reconciliation", label: lang === "ar" ? "تسوية الحسابات مع التفاصيل" : "Ledger against detail" },
          { to: "/review/approvals", label: lang === "ar" ? "سجل الاعتمادات" : "Approvals log" },
          { to: "/review/unusual-entries", label: lang === "ar" ? "القيود غير المعتادة" : "Unusual entries" },
          { to: "/review/number-gaps", label: lang === "ar" ? "ثغرات الترقيم" : "Numbering gaps" },
        ] },
        { subGroup: lang === "ar" ? "الرقابة" : "Control", icon: <IconCheck />, subItems: [
          { to: "/accounting/journal-entries", label: t.nav.journalEntries },
          { to: "/accounting/general-ledger", label: t.nav.generalLedger },
          { to: "/accounting/trial-balance", label: t.nav.trialBalance },
          { to: "/accounting/bank-feed-reconciliation", label: t.nav.bankFeedReconciliation },
        ] },
        { subGroup: lang === "ar" ? "الأصول والقروض" : "Assets and loans", icon: <IconBox />, subItems: [
          { to: "/accounting/fixed-assets", label: t.nav.fixedAssets },
          { to: "/accounting/depreciation-runs", label: t.nav.depreciationRuns },
          { to: "/accounting/loans", label: t.nav.loans },
        ] },
        { subGroup: lang === "ar" ? "قيود التسوية" : "Regularization entries", icon: <IconTrendDown />, subItems: [
          { to: "/accounting/deferrals", label: t.nav.deferrals },
        ] },
        ...(user?.roles.includes("Admin") ? [{ subGroup: lang === "ar" ? "السجلات" : "Logs", icon: <IconList />, subItems: [
          { to: "/audit-log", label: t.nav.auditLog },
          { to: "/login-history", label: t.nav.loginHistory },
        ] }] : []),
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
        { to: "/sales/customers/new", label: t.nav.addCustomerFull, icon: <IconUser /> },
        { to: "/sales/quotations", label: t.nav.quotations, icon: <IconFile /> },
        { to: "/sales/invoices", label: t.nav.salesInvoices, icon: <IconFile /> },
        { to: "/sales/notes", label: t.nav.salesNotes, icon: <IconEdit /> },
        { to: "/sales/aging", label: t.nav.arAging, icon: <IconClock /> },
        { to: "/sales/follow-ups", label: t.nav.customerFollowUps, icon: <IconClock /> },
        { to: "/sales/statement", label: t.nav.customerStatement, icon: <IconFile /> },
      ],
    },
    {
      section: t.nav.inventory,
      items: [
        { to: "/inventory/items", label: t.nav.items, icon: <IconBox /> },
        { to: "/inventory/warehouses", label: t.nav.warehouses, icon: <IconArchive /> },
        { to: "/inventory/transfers", label: t.nav.stockTransfers, icon: <IconSwap /> },
        { to: "/inventory/adjustments", label: t.nav.stockAdjustments, icon: <IconSwap /> },
        { to: "/inventory/warehouse-stock", label: t.nav.warehouseStock, icon: <IconArchive /> },
        { to: "/inventory/branch-minimums", label: t.nav.branchMinimums, icon: <IconArchive /> },
        { to: "/inventory/movements", label: t.nav.stockMovements, icon: <IconSwap /> },
        { to: "/inventory/physical-stock-counts", label: t.inventory.physicalStockCountsTitle, icon: <IconCheck /> },
        { to: "/inventory/waste-entries", label: t.inventory.wasteEntriesTitle, icon: <IconTrendDown /> },
        { to: "/inventory/manufacturing", label: t.inventory.manufacturingTitle, icon: <IconRefresh /> },
        { to: "/inventory/expiring-stock", label: t.inventory.expiringStockTitle, icon: <IconClock /> },
        { subGroup: t.inventory.inventoryReports, icon: <IconBarChart />, subItems: inventoryReportItems },
      ],
    },
    {
      section: t.nav.restaurant,
      items: [
        { to: "/restaurant/pos", label: t.nav.restaurantPos, icon: <IconCart /> },
        { to: "/restaurant/setup", label: t.restaurant.setupTitle, icon: <IconStar /> },
        { to: "/restaurant/kitchen", label: t.nav.kitchenDisplay, icon: <IconClock /> },
        { to: "/restaurant/reservations", label: t.nav.restaurantReservations, icon: <IconCalendar /> },
        { to: "/restaurant/tables", label: t.nav.restaurantTables, icon: <IconGrid /> },
        { to: "/restaurant/loyalty", label: t.nav.restaurantLoyalty, icon: <IconStar /> },
        { to: "/restaurant/menu", label: t.nav.restaurantMenu, icon: <IconBook /> },
        { to: "/restaurant/purchase-receiving", label: t.restaurant.purchaseReceivingTitle, icon: <IconTruck /> },
        { to: "/restaurant/delivery-reconciliation", label: t.inventory.deliveryReconciliationTitle, icon: <IconSwap /> },
        { to: "/restaurant/delivery-platforms", label: t.restaurant.deliveryPlatformsTitle, icon: <IconRefresh /> },
      ],
    },
    {
      section: t.nav.purchasing,
      items: [
        { to: "/purchasing/vendors", label: t.nav.vendors, icon: <IconTruck /> },
        { to: "/purchasing/vendors/new", label: t.nav.addVendorFull, icon: <IconUser /> },
        { to: "/purchasing/orders", label: t.nav.purchaseOrders, icon: <IconCart /> },
        { to: "/purchasing/invoices", label: t.nav.purchaseInvoices, icon: <IconCart /> },
        { to: "/purchasing/aging", label: t.nav.apAging, icon: <IconClock /> },
        { to: "/purchasing/statement", label: t.nav.vendorStatement, icon: <IconFile /> },
      ].filter((item) => canApprovePurchases || item.to === "/purchasing/invoices"),
    },
    ...(canManageHr
      ? [
          {
            section: t.nav.hr,
            items: [
              { to: "/hr/employees", label: t.nav.employees, icon: <IconUsers /> },
              { to: "/hr/departments", label: t.nav.departments, icon: <IconBuilding /> },
              { to: "/hr/positions", label: t.nav.positions, icon: <IconBriefcase /> },
              { to: "/hr/employee-contracts", label: t.hr.employeeContractsTitle, icon: <IconFile /> },
              { to: "/hr/salary-components", label: t.hr.salaryComponentsTitle, icon: <IconWallet /> },
              { to: "/hr/payroll", label: t.nav.payroll, icon: <IconDollar /> },
              { to: "/hr/payroll-settings", label: t.hr.payrollSettingsTitle, icon: <IconWallet /> },
              { to: "/hr/employee-debts", label: t.nav.employeeDebts, icon: <IconWallet /> },
              { to: "/hr/labor-report", label: t.hr.laborReportTitle, icon: <IconBarChart /> },
              { to: "/hr/work-locations", label: t.hr.workLocationsTitle, icon: <IconGrid /> },
              { to: "/hr/shifts", label: t.hr.shiftsTitle, icon: <IconCalendar /> },
              { to: "/hr/recruitment", label: t.hr.recruitmentTitle, icon: <IconUsers /> },
              { to: "/hr/performance", label: t.hr.performanceTitle, icon: <IconBarChart /> },
              { to: "/hr/surveys", label: t.hr.surveysTitle, icon: <IconChat /> },
              { to: "/hr/employee-requests", label: t.hr.employeeRequestsTitle, icon: <IconCheck /> },
            ],
          },
        ]
      : []),
    ...(user?.roles.includes("Admin")
      ? [
          {
            section: t.nav.administration,
            items: [
              { to: "/users", label: t.nav.users, icon: <IconShield /> },
              { to: "/integrations", label: t.nav.integrations, icon: <IconRefresh /> },
              { to: "/einvoicing", label: t.nav.eInvoicing, icon: <IconFile /> },
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
  // The free Roma Books plan: accounting, reports, sales and purchasing only — no POS, inventory, HR or AI.
  const isPosOnly = user?.productScope === ProductScope.PosOnly;
  const posOnlyAllowedSections = new Set([t.nav.general, t.nav.restaurant, t.nav.inventory, t.nav.purchasing, t.nav.reports, t.nav.administration]);
  const isBooksOnly = user?.productScope === ProductScope.BooksOnly;
  const booksOnlyAllowedSections = new Set([t.nav.general, t.nav.accounting, t.nav.review, t.nav.reports, t.nav.sales, t.nav.purchasing, t.nav.administration]);

  // Each of these sections requires either the base role that's always had it, or the matching
  // per-user "module" grant (see ModulePermissions on the backend) — Admin always sees everything.
  const isAdmin = user?.roles.includes("Admin") ?? false;
  const sectionAccess: Record<string, { module: string; fallbackRoles: string[] }> = {
    [t.nav.accounting]: { module: "Accounting", fallbackRoles: ["Accountant"] },
    [t.nav.reports]: { module: "Reports", fallbackRoles: ["Accountant"] },
    [t.nav.review]: { module: "Accounting", fallbackRoles: ["Accountant"] },
    [t.nav.sales]: { module: "Sales", fallbackRoles: ["Accountant"] },
    [t.nav.purchasing]: { module: "Purchasing", fallbackRoles: ["Accountant"] },
    [t.nav.inventory]: { module: "Inventory", fallbackRoles: ["Accountant"] },
    [t.nav.restaurant]: { module: "POS", fallbackRoles: ["Accountant", "Employee"] },
  };
  // A company that said what it does sees the sections that fit it first; the rest are one click away.
  const [showAll, setShowAll] = useState(() => {
    try { return localStorage.getItem(SHOW_ALL_KEY) === "1"; } catch { return false; }
  });
  const hiddenKeys = hiddenSectionsFor(user?.businessTypes);
  const sectionKeys: Record<string, NavSectionKey> = {
    [t.nav.restaurant]: "restaurant",
    [t.nav.inventory]: "inventory",
  };
  function toggleShowAll() {
    const next = !showAll;
    setShowAll(next);
    try { localStorage.setItem(SHOW_ALL_KEY, next ? "1" : "0"); } catch { /* ignore */ }
  }
  function canSeeSection(section: string) {
    if (!showAll && sectionKeys[section] && hiddenKeys.includes(sectionKeys[section])) return false;
    if (isPeopleOnly && !peopleOnlyAllowedSections.has(section)) return false;
    if (isBooksOnly && !booksOnlyAllowedSections.has(section)) return false;
    if (isPosOnly && !posOnlyAllowedSections.has(section)) return false;
    const access = sectionAccess[section];
    if (!access) return true;
    return isAdmin || access.fallbackRoles.some((r) => user?.roles.includes(r)) || (user?.modules.includes(access.module) ?? false);
  }

  const extraSections = new Set([t.nav.accounting, t.nav.review, t.nav.reports, t.nav.sales, t.nav.purchasing, t.nav.inventory]);
  const links = (isCashierOnly
    ? cashierLinks
    : isEmployeeWithExtras
      ? [...cashierLinks, ...fullLinks.filter((group) => extraSections.has(group.section))]
      : fullLinks
  ).filter((group) => canSeeSection(group.section)).map((group) => {
    // The free Books plan keeps only the basic reports: the financial statements and item/customer profitability.
    if (!isBooksOnly || group.section !== t.nav.reports) return group;
    const keep = new Set([t.nav.financialStatementsGroup, t.nav.profitabilityReports]);
    const basicProfit = new Set(["/accounting/item-profitability", "/accounting/customer-profitability"]);
    return {
      ...group,
      items: group.items
        .filter((item) => !("subGroup" in item) || keep.has(item.subGroup))
        .map((item) => "subGroup" in item && item.subGroup === t.nav.profitabilityReports
          ? { ...item, subItems: item.subItems.filter((sub) => basicProfit.has(sub.to)) }
          : item),
    };
  });

  // Inventory lives inside the restaurant/POS product (one circle): its screens become groups of the Restaurant section.
  // Someone who may use inventory but not the restaurant keeps Inventory as a section of its own.
  const restaurantGroup = links.find((g) => g.section === t.nav.restaurant);
  const inventoryGroup = links.find((g) => g.section === t.nav.inventory);
  const mergedLinks = restaurantGroup && inventoryGroup
    ? links.filter((g) => g !== inventoryGroup).map((g) => g !== restaurantGroup ? g : {
        ...g,
        items: [
          ...g.items,
          { subGroup: t.nav.inventory, icon: <IconBox />, subItems: inventoryGroup.items.filter((i): i is NavLeafItem => !("subGroup" in i)).map((i) => ({ to: i.to, label: i.label })) },
          ...inventoryGroup.items.filter((i): i is NavSubGroupItem => "subGroup" in i),
        ],
      })
    : links;

  // Main tabs in the order the owner asked for; the HR and Restaurant products are icons next to the language button.
  const iconSections = [t.nav.hr, t.nav.restaurant];
  const mainOrder = [t.nav.general, t.nav.assistant, t.nav.accounting, t.nav.purchasing, t.nav.sales, t.nav.review, t.nav.reports, t.nav.administration];
  const mainLinks = [
    ...mainOrder.map((s) => mergedLinks.find((g) => g.section === s)).filter((g): g is (typeof mergedLinks)[number] => !!g),
    ...mergedLinks.filter((g) => !mainOrder.includes(g.section) && !iconSections.includes(g.section)),
  ];
  const iconLinks = iconSections.map((s) => mergedLinks.find((g) => g.section === s)).filter((g): g is (typeof mergedLinks)[number] => !!g);

  const activeSection = useMemo(() => {
    for (const group of mergedLinks) {
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
  }, [mergedLinks, location.pathname]);

  // A section's screens as a vertical list that drops straight down from its own tab.
  function renderDropdown(group: (typeof mergedLinks)[number]) {
    const flatItems = group.items.filter((item): item is NavLeafItem => !("subGroup" in item));
              const subGroups = group.items.filter((item): item is NavSubGroupItem => "subGroup" in item);
    return (
      <div className="topnav-dropdown">
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
  }

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
            {hiddenKeys.length > 0 && (
              <button className="btn btn-secondary btn-sm" onClick={toggleShowAll}>
                {showAll ? t.nav.showMySections : t.nav.showAllSections}
              </button>
            )}
            <UsageIndicator />
            <span className="topnav-user">{user?.fullName}</span>
            {/* The HR and Restaurant products sit here as icons; each opens its own screens. */}
            <span ref={iconsRef} style={{ display: "inline-flex", gap: 6 }}>
              {iconLinks.map((group) => (
                <button
                  key={group.section}
                  className={"sidebar-toggle" + (activeSection === group.section ? " active" : "")}
                  title={group.section}
                  aria-label={group.section}
                  onClick={() => {
                    // each product opens its own page, which holds all of its screens
                    setOpenDropdown(null);
                    navigate(group.section === t.nav.hr ? "/people" : isCashierOnly ? "/restaurant/pos" : "/restaurant-portal");
                  }}
                  style={activeSection === group.section ? { outline: "2px solid currentColor", outlineOffset: -2 } : undefined}
                >
                  {group.section === t.nav.hr ? <IconUsers /> : <IconCart />}
                </button>
              ))}
            </span>
            <HelpButton />
            <FeedbackButton />
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
        <div ref={navRef} className="topnav-menu" onPointerLeave={(e) => { if (e.pointerType === "mouse") setOpenDropdown(null); }}>
          <nav className="topnav-links">
            {mainLinks.map((group) => (
              <div key={group.section} className="topnav-item">
              <button
                className={"topnav-link" + (activeSection === group.section ? " active" : "")}
                onPointerEnter={(e) => { if (e.pointerType === "mouse") { hoverOpenedAt.current = Date.now(); setOpenDropdown(group.section); } }}
                onClick={() => {
                  // The first tab is the dashboard itself: with a mouse, clicking it goes there (hovering shows its list). On a phone there is no hover,
                  // so a tap opens its list like every other tab, and the first item in that list is the dashboard.
                  const phone = window.matchMedia("(pointer: coarse)").matches;
                  if (group.section === t.nav.general && !phone) { navigate("/"); setOpenDropdown(null); return; }
                  setOpenDropdown((cur) => (cur === group.section && Date.now() - hoverOpenedAt.current > 700 ? null : group.section));
                }}
              >
                <span>{group.section}</span>
              </button>
              {openDropdown === group.section && renderDropdown(group)}
              </div>
            ))}
          </nav>
        </div>
      </header>
      <main className="topnav-content">{children}</main>
    </div>
  );
}
