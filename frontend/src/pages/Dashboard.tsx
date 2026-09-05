import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import {
  AccountsApi,
  AiAssistantApi,
  EmployeesApi,
  FinancialReportsApi,
  ItemsApi,
  JournalEntriesApi,
  PurchasingApi,
  SalesApi,
} from "../api/services";
import { useLanguage } from "../i18n/LanguageContext";
import {
  IconChat,
  IconList,
  IconUsers,
  IconTruck,
  IconBook,
  IconBox,
  IconCart,
  IconUser,
} from "../components/icons";

const COGS_ACCOUNT_CODE = "5500";

function firstDayOfMonth() {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-01`;
}

function addDays(dateStr: string, days: number) {
  const d = new Date(dateStr);
  d.setDate(d.getDate() + days);
  return d.toISOString().slice(0, 10);
}

interface Alert {
  to: string;
  badge: string;
  label: string;
  severity: 1 | 2 | 3;
}

export default function Dashboard() {
  const { t } = useLanguage();
  const [accountsCount, setAccountsCount] = useState(0);
  const [employeesCount, setEmployeesCount] = useState(0);
  const [totalDebit, setTotalDebit] = useState(0);
  const [totalCredit, setTotalCredit] = useState(0);
  const [totalSales, setTotalSales] = useState(0);
  const [totalPurchases, setTotalPurchases] = useState(0);
  const [arOutstanding, setArOutstanding] = useState(0);
  const [apOutstanding, setApOutstanding] = useState(0);

  const [netProfit, setNetProfit] = useState(0);
  const [profitMargin, setProfitMargin] = useState(0);
  const [cogs, setCogs] = useState(0);
  const [cashFlowNet, setCashFlowNet] = useState(0);
  const [availableLiquidity, setAvailableLiquidity] = useState(0);
  const [breakEvenSales, setBreakEvenSales] = useState(0);
  const [collectionRate, setCollectionRate] = useState(0);
  const [inventoryValue, setInventoryValue] = useState(0);
  const [healthScore, setHealthScore] = useState<number | null>(null);
  const [smartAlerts, setSmartAlerts] = useState<Alert[]>([]);

  useEffect(() => {
    AccountsApi.getAll().then((r) => setAccountsCount(r.data.length));
    EmployeesApi.getAll().then((r) => setEmployeesCount(r.data.length));
    JournalEntriesApi.trialBalance().then((r) => {
      setTotalDebit(r.data.reduce((sum, l) => sum + l.totalDebit, 0));
      setTotalCredit(r.data.reduce((sum, l) => sum + l.totalCredit, 0));
    });
    PurchasingApi.getInvoices().then((r) => {
      setTotalPurchases(r.data.reduce((sum, i) => sum + i.totalAmount, 0));
      setApOutstanding(r.data.reduce((sum, i) => sum + i.outstandingAmount, 0));
    });
    ItemsApi.getAll().then((r) => {
      setInventoryValue(r.data.reduce((sum, i) => sum + i.quantityOnHand * i.averageCost, 0));
    });

    const today = new Date().toISOString().slice(0, 10);
    const monthStart = firstDayOfMonth();
    const priorMonthEnd = addDays(monthStart, -1);
    const monthLengthSoFar = Math.max(1, Math.round((new Date(today).getTime() - new Date(monthStart).getTime()) / 86400000) + 1);
    const priorMonthStart = addDays(priorMonthEnd, -(monthLengthSoFar - 1));

    Promise.all([
      FinancialReportsApi.incomeStatement(monthStart, today),
      FinancialReportsApi.incomeStatement(priorMonthStart, priorMonthEnd),
      FinancialReportsApi.cashFlow(monthStart, today),
      FinancialReportsApi.cashFlow(priorMonthStart, priorMonthEnd),
      FinancialReportsApi.itemProfitability(monthStart, today),
      SalesApi.getInvoices(),
      SalesApi.getAging(),
      PurchasingApi.getAging(),
      AiAssistantApi.getPendingApproval(),
    ]).then(([currentRes, priorRes, cashRes, priorCashRes, itemProfitRes, invoicesRes, arAgingRes, apAgingRes, pendingApprovalRes]) => {
      const current = currentRes.data;
      const prior = priorRes.data;
      const cash = cashRes.data;
      const priorCash = priorCashRes.data;

      setTotalSales(invoicesRes.data.reduce((sum, i) => sum + i.totalAmount, 0));
      setArOutstanding(invoicesRes.data.reduce((sum, i) => sum + i.outstandingAmount, 0));

      const overdueCustomersCount = arAgingRes.data.filter((c) => c.days31To60 + c.days61To90 + c.over90Days > 0).length;
      const overdueVendorsCount = apAgingRes.data.filter((v) => v.days31To60 + v.days61To90 + v.over90Days > 0).length;
      const pendingApprovalsCount = pendingApprovalRes.data.length;

      const currentCogs = current.expenseLines.find((l) => l.accountCode === COGS_ACCOUNT_CODE)?.amount ?? 0;
      const priorCogs = prior.expenseLines.find((l) => l.accountCode === COGS_ACCOUNT_CODE)?.amount ?? 0;
      const currentCogsRatio = current.totalRevenue > 0 ? currentCogs / current.totalRevenue : 0;
      const priorCogsRatio = prior.totalRevenue > 0 ? priorCogs / prior.totalRevenue : 0;

      const currentMargin = current.totalRevenue > 0 ? (current.netIncome / current.totalRevenue) * 100 : 0;
      const priorMargin = prior.totalRevenue > 0 ? (prior.netIncome / prior.totalRevenue) * 100 : 0;

      const fixedCosts = current.totalExpense - currentCogs;
      const contributionMarginRatio = current.totalRevenue > 0 ? (current.totalRevenue - currentCogs) / current.totalRevenue : 0;
      const breakEven = contributionMarginRatio > 0 ? fixedCosts / contributionMarginRatio : 0;

      const monthInvoices = invoicesRes.data.filter((i) => i.invoiceDate >= monthStart && i.invoiceDate <= today);
      const monthInvoiceTotal = monthInvoices.reduce((sum, i) => sum + i.totalAmount, 0);
      const monthInvoicePaid = monthInvoices.reduce((sum, i) => sum + i.paidAmount, 0);
      const collection = monthInvoiceTotal > 0 ? (monthInvoicePaid / monthInvoiceTotal) * 100 : 100;

      setNetProfit(current.netIncome);
      setProfitMargin(currentMargin);
      setCogs(currentCogs);
      setCashFlowNet(cash.netCashChange);
      setAvailableLiquidity(cash.endingCash);
      setBreakEvenSales(breakEven);
      setCollectionRate(collection);

      // Health score: average of 4 signals normalized 0-100 against reasonable small-business targets.
      const profitabilityScore = Math.max(0, Math.min(100, (currentMargin / 20) * 100));
      const daysCovered = current.totalExpense > 0 ? cash.endingCash / (current.totalExpense / monthLengthSoFar) : 30;
      const liquidityScore = Math.max(0, Math.min(100, (daysCovered / 30) * 100));
      const collectionScore = Math.max(0, Math.min(100, collection));
      const costControlScore = Math.max(0, Math.min(100, 100 - Math.max(0, currentCogsRatio * 100 - 35) * 4));
      setHealthScore(Math.round((profitabilityScore + liquidityScore + collectionScore + costControlScore) / 4));

      const alerts: Alert[] = [];
      const cogsRatioDeltaPct = (currentCogsRatio - priorCogsRatio) * 100;
      if (prior.totalRevenue > 0 && cogsRatioDeltaPct > 2) {
        alerts.push({ to: "/accounting/bottleneck", badge: `+${cogsRatioDeltaPct.toFixed(1)}%`, label: t.dashboard.alertCogsUp, severity: 1 });
      }
      const marginDelta = currentMargin - priorMargin;
      if (prior.totalRevenue > 0 && marginDelta < -2) {
        alerts.push({ to: "/accounting/money-flow", badge: `${marginDelta.toFixed(1)}pt`, label: t.dashboard.alertMarginDown, severity: 1 });
      }
      if (priorCash.netCashChange > 0 && cash.netCashChange < priorCash.netCashChange) {
        alerts.push({ to: "/accounting/cash-flow", badge: "↓", label: t.dashboard.alertCashDown, severity: 2 });
      }
      const lowMarginItems = itemProfitRes.data.items.filter((i) => i.marginPercent < 10 && i.quantitySold > 0);
      if (lowMarginItems.length > 0) {
        alerts.push({
          to: "/accounting/item-profitability",
          badge: String(lowMarginItems.length),
          label: t.dashboard.alertLowMarginItems,
          severity: 2,
        });
      }
      if (overdueCustomersCount > 0) {
        alerts.push({ to: "/sales/aging", badge: String(overdueCustomersCount), label: t.dashboard.overdueCustomers, severity: 2 });
      }
      if (overdueVendorsCount > 0) {
        alerts.push({ to: "/purchasing/aging", badge: String(overdueVendorsCount), label: t.dashboard.overdueVendors, severity: 3 });
      }
      if (pendingApprovalsCount > 0) {
        alerts.push({ to: "/assistant/approvals", badge: String(pendingApprovalsCount), label: t.dashboard.pendingApprovals, severity: 3 });
      }

      alerts.sort((a, b) => a.severity - b.severity);
      setSmartAlerts(alerts.slice(0, 5));
    });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const quickLinks = [
    { to: "/assistant/chat", label: t.nav.assistantChat, icon: <IconChat /> },
    { to: "/accounting/journal-entries", label: t.nav.journalEntries, icon: <IconBook /> },
    { to: "/sales/customers", label: t.nav.customers, icon: <IconUsers /> },
    { to: "/sales/invoices", label: t.nav.salesInvoices, icon: <IconList /> },
    { to: "/purchasing/vendors", label: t.nav.vendors, icon: <IconTruck /> },
    { to: "/inventory/items", label: t.nav.items, icon: <IconBox /> },
    { to: "/restaurant/pos", label: t.nav.restaurantPos, icon: <IconCart /> },
    { to: "/my-profile", label: t.nav.myProfile, icon: <IconUser /> },
  ];

  const salesVsPurchasesMax = Math.max(totalSales, totalPurchases, 1);
  const arVsApMax = Math.max(arOutstanding, apOutstanding, 1);

  const severityDot: Record<Alert["severity"], string> = { 1: "🔴", 2: "🟠", 3: "🟡" };

  return (
    <div>
      <div className="page-header">
        <h1>{t.dashboard.title}</h1>
      </div>

      <div className="dash-app-grid">
        {quickLinks.map((item) => (
          <Link key={item.to} to={item.to} className="dash-app-tile">
            <span className="dash-app-tile-icon">{item.icon}</span>
            <span>{item.label}</span>
          </Link>
        ))}
      </div>

      <div className="dash-hero-grid" style={{ marginTop: 24 }}>
        <div className="stat-grid" style={{ flex: 1 }}>
          <div className="stat-card">
            <div className="label">{t.dashboard.totalSales}</div>
            <div className="value">{totalSales.toLocaleString()}</div>
          </div>
          <div className="stat-card">
            <div className="label">{t.dashboard.netProfit}</div>
            <div className="value" style={{ color: netProfit >= 0 ? "var(--color-success)" : "var(--color-danger)" }}>
              {netProfit.toLocaleString()}
            </div>
          </div>
          <div className="stat-card">
            <div className="label">{t.dashboard.profitMargin}</div>
            <div className="value">{profitMargin.toFixed(1)}%</div>
          </div>
          <div className="stat-card">
            <div className="label">{t.dashboard.cogsLabel}</div>
            <div className="value">{cogs.toLocaleString()}</div>
          </div>
        </div>
        <div className="dash-health-card">
          <div className="dash-health-score">{healthScore ?? "…"}</div>
          <div className="dash-health-label">{t.dashboard.healthScore}</div>
          <div className="dash-health-note">{t.dashboard.healthScoreNote}</div>
        </div>
      </div>

      <div className="stat-grid" style={{ marginTop: 16 }}>
        <div className="stat-card">
          <div className="label">{t.dashboard.cashFlowNet}</div>
          <div className="value" style={{ color: cashFlowNet >= 0 ? "var(--color-success)" : "var(--color-danger)" }}>
            {cashFlowNet.toLocaleString()}
          </div>
        </div>
        <div className="stat-card">
          <div className="label">{t.dashboard.availableLiquidity}</div>
          <div className="value">{availableLiquidity.toLocaleString()}</div>
        </div>
        <div className="stat-card">
          <div className="label">{t.dashboard.breakEvenLabel}</div>
          <div className="value">{breakEvenSales.toLocaleString()}</div>
        </div>
        <div className="stat-card">
          <div className="label">{t.dashboard.collectionRate}</div>
          <div className="value">{collectionRate.toFixed(1)}%</div>
        </div>
        <div className="stat-card">
          <div className="label">{t.dashboard.inventoryValue}</div>
          <div className="value">{inventoryValue.toLocaleString()}</div>
        </div>
      </div>

      <h3 style={{ marginTop: 24 }}>🚨 {t.dashboard.needsAttention}</h3>
      <div className="dash-alerts">
        {smartAlerts.length === 0 && <div className="dash-alert dash-alert-clear">✓ {t.dashboard.allClear}</div>}
        {smartAlerts.map((a) => (
          <Link key={a.to + a.label} to={a.to} className="dash-alert">
            <span>{severityDot[a.severity]}</span>
            <span className="dash-alert-count">{a.badge}</span>
            <span>{a.label}</span>
          </Link>
        ))}
      </div>

      <div className="stat-grid" style={{ marginTop: 24 }}>
        <div className="stat-card">
          <div className="label">{t.dashboard.accountsCount}</div>
          <div className="value">{accountsCount}</div>
        </div>
        <div className="stat-card">
          <div className="label">{t.dashboard.employeesCount}</div>
          <div className="value">{employeesCount}</div>
        </div>
        <div className="stat-card">
          <div className="label">{t.dashboard.totalDebit}</div>
          <div className="value">{totalDebit.toLocaleString()}</div>
        </div>
        <div className="stat-card">
          <div className="label">{t.dashboard.totalCredit}</div>
          <div className="value">{totalCredit.toLocaleString()}</div>
        </div>
        <div className="stat-card">
          <div className="label">{t.dashboard.totalPurchases}</div>
          <div className="value">{totalPurchases.toLocaleString()}</div>
        </div>
        <div className="stat-card">
          <div className="label">{t.dashboard.arOutstanding}</div>
          <div className="value">{arOutstanding.toLocaleString()}</div>
        </div>
        <div className="stat-card">
          <div className="label">{t.dashboard.apOutstanding}</div>
          <div className="value">{apOutstanding.toLocaleString()}</div>
        </div>
      </div>

      <h3 style={{ marginTop: 24 }}>{t.dashboard.financialSnapshot}</h3>
      <div className="dash-snapshot-grid">
        <div className="card">
          <div className="dash-snapshot-title">{t.dashboard.salesVsPurchases}</div>
          <div className="dash-bar-row">
            <span className="dash-bar-label">{t.dashboard.totalSales}</span>
            <div className="dash-bar-track">
              <div className="dash-bar-fill dash-bar-primary" style={{ width: `${(totalSales / salesVsPurchasesMax) * 100}%` }} />
            </div>
            <span className="dash-bar-value">{totalSales.toLocaleString()}</span>
          </div>
          <div className="dash-bar-row">
            <span className="dash-bar-label">{t.dashboard.totalPurchases}</span>
            <div className="dash-bar-track">
              <div className="dash-bar-fill dash-bar-accent" style={{ width: `${(totalPurchases / salesVsPurchasesMax) * 100}%` }} />
            </div>
            <span className="dash-bar-value">{totalPurchases.toLocaleString()}</span>
          </div>
        </div>
        <div className="card">
          <div className="dash-snapshot-title">{t.dashboard.arVsAp}</div>
          <div className="dash-bar-row">
            <span className="dash-bar-label">{t.dashboard.arOutstanding}</span>
            <div className="dash-bar-track">
              <div className="dash-bar-fill dash-bar-primary" style={{ width: `${(arOutstanding / arVsApMax) * 100}%` }} />
            </div>
            <span className="dash-bar-value">{arOutstanding.toLocaleString()}</span>
          </div>
          <div className="dash-bar-row">
            <span className="dash-bar-label">{t.dashboard.apOutstanding}</span>
            <div className="dash-bar-track">
              <div className="dash-bar-fill dash-bar-accent" style={{ width: `${(apOutstanding / arVsApMax) * 100}%` }} />
            </div>
            <span className="dash-bar-value">{apOutstanding.toLocaleString()}</span>
          </div>
        </div>
      </div>
    </div>
  );
}
