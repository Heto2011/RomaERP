import { useEffect, useMemo, useState, type ReactNode } from "react";
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
import { getErrorMessage } from "../api/client";
import { useLanguage } from "../i18n/LanguageContext";
import { useAuth } from "../context/AuthContext";
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


interface Bucket {
  label: string;
  value: number;
  tone: "ok" | "warn" | "late" | "bad";
}

function useCountUp(target: number, ms = 900) {
  const [value, setValue] = useState(0);
  useEffect(() => {
    if (typeof window === "undefined" || window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
      setValue(target);
      return;
    }
    let raf = 0;
    const start = performance.now();
    const tick = (now: number) => {
      const p = Math.min(1, (now - start) / ms);
      setValue(target * (1 - Math.pow(1 - p, 3)));
      if (p < 1) raf = requestAnimationFrame(tick);
    };
    raf = requestAnimationFrame(tick);
    return () => cancelAnimationFrame(raf);
  }, [target, ms]);
  return value;
}

function Delta({ current, previous, points }: { current: number; previous: number; points?: boolean }) {
  if (!previous && !points) return null;
  const diff = points ? current - previous : ((current - previous) / Math.abs(previous)) * 100;
  if (!Number.isFinite(diff)) return null;
  const up = diff >= 0;
  return (
    <span className={`d2-delta ${up ? "up" : "down"}`}>
      {up ? "▲" : "▼"} {Math.abs(diff).toFixed(1)}
      {points ? "pt" : "%"}
    </span>
  );
}

function Kpi({ icon, label, value, suffix, tone, delta, delay }: { icon: ReactNode; label: string; value: number; suffix?: string; tone?: "good" | "bad"; delta?: ReactNode; delay: number }) {
  const shown = useCountUp(value);
  const decimals = suffix === "%" ? 1 : 0;
  return (
    <div className={`d2-kpi ${tone ?? ""}`} style={{ animationDelay: `${delay}ms` }}>
      <div className="d2-kpi-top">
        <span className="d2-kpi-icon">{icon}</span>
        {delta}
      </div>
      <div className="d2-kpi-value">
        {shown.toLocaleString(undefined, { maximumFractionDigits: decimals, minimumFractionDigits: decimals })}
        {suffix ? <small>{suffix}</small> : null}
      </div>
      <div className="d2-kpi-label">{label}</div>
    </div>
  );
}

function Ring({ score }: { score: number | null }) {
  const r = 52;
  const c = 2 * Math.PI * r;
  const v = score ?? 0;
  const tone = v >= 70 ? "ok" : v >= 40 ? "warn" : "bad";
  return (
    <svg className={`d2-ring ${tone}`} viewBox="0 0 128 128" role="img" aria-label={String(score ?? "")}>
      <circle className="d2-ring-track" cx="64" cy="64" r={r} />
      <circle className="d2-ring-bar" cx="64" cy="64" r={r} strokeDasharray={c} strokeDashoffset={c - (c * v) / 100} />
      <text x="64" y="72" textAnchor="middle" className="d2-ring-text">
        {score ?? "…"}
      </text>
    </svg>
  );
}

function BarChart({ items, empty }: { items: { label: string; value: number; tone?: Bucket["tone"] }[]; empty: string }) {
  const max = Math.max(...items.map((i) => i.value), 0);
  if (max <= 0) return <div className="d2-empty">{empty}</div>;
  return (
    <div className="d2-bars">
      {items.map((i, idx) => (
        <div key={i.label} className="d2-bar-col" title={`${i.label}: ${i.value.toLocaleString()}`}>
          <span className="d2-bar-val">{i.value > 0 ? Math.round(i.value).toLocaleString() : ""}</span>
          <div className="d2-bar-track">
            <div className={`d2-bar ${i.tone ?? "brand"}`} style={{ height: `${Math.max(3, (i.value / max) * 100)}%`, animationDelay: `${idx * 70}ms` }} />
          </div>
          <span className="d2-bar-label">{i.label}</span>
        </div>
      ))}
    </div>
  );
}

interface Alert {
  to: string;
  badge: string;
  label: string;
  severity: 1 | 2 | 3;
}

export default function Dashboard() {
  const { t, lang } = useLanguage();
  const [qaQuestion, setQaQuestion] = useState("");
  const [qaAnswer, setQaAnswer] = useState<string | null>(null);
  const [qaError, setQaError] = useState<string | null>(null);
  const [qaLoading, setQaLoading] = useState(false);
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
  const [monthRevenue, setMonthRevenue] = useState(0);
  const [priorRevenue, setPriorRevenue] = useState(0);
  const [priorProfit, setPriorProfit] = useState(0);
  const [priorMarginPct, setPriorMarginPct] = useState(0);
  const [monthlySales, setMonthlySales] = useState<{ key: string; value: number }[]>([]);
  const [arBuckets, setArBuckets] = useState<number[]>([0, 0, 0, 0]);
  const [apBuckets, setApBuckets] = useState<number[]>([0, 0, 0, 0]);
  const { user } = useAuth();
  const [smartAlerts, setSmartAlerts] = useState<Alert[]>([]);

  useEffect(() => {
    // A company's product may not include every area (HR, journal screens, ...): a refused call just leaves that figure empty.
    const ignore = () => undefined;
    AccountsApi.getAll().then((r) => setAccountsCount(r.data.length)).catch(ignore);
    EmployeesApi.getAll().then((r) => setEmployeesCount(r.data.length)).catch(ignore);
    JournalEntriesApi.trialBalance().then((r) => {
      setTotalDebit(r.data.reduce((sum, l) => sum + l.totalDebit, 0));
      setTotalCredit(r.data.reduce((sum, l) => sum + l.totalCredit, 0));
    }).catch(ignore);
    PurchasingApi.getInvoices().then((r) => {
      setTotalPurchases(r.data.reduce((sum, i) => sum + i.totalAmount, 0));
      setApOutstanding(r.data.reduce((sum, i) => sum + i.outstandingAmount, 0));
    }).catch(ignore);
    ItemsApi.getAll().then((r) => {
      setInventoryValue(r.data.reduce((sum, i) => sum + i.quantityOnHand * i.averageCost, 0));
    }).catch(ignore);

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
      SalesApi.getAging().catch(() => ({ data: [] as never[] })),
      PurchasingApi.getAging().catch(() => ({ data: [] as never[] })),
      AiAssistantApi.getPendingApproval().catch(() => ({ data: [] as never[] })),
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

      setMonthRevenue(current.totalRevenue);
      setPriorRevenue(prior.totalRevenue);
      setPriorProfit(prior.netIncome);
      setPriorMarginPct(priorMargin);
      const buckets = (rows: { current: number; days31To60: number; days61To90: number; over90Days: number }[]) =>
        rows.reduce((acc, r) => [acc[0] + r.current, acc[1] + r.days31To60, acc[2] + r.days61To90, acc[3] + r.over90Days], [0, 0, 0, 0]);
      setArBuckets(buckets(arAgingRes.data));
      setApBuckets(buckets(apAgingRes.data));
      const monthTotals = new Map<string, number>();
      for (const inv of invoicesRes.data) {
        const key = inv.invoiceDate.slice(0, 7);
        monthTotals.set(key, (monthTotals.get(key) ?? 0) + inv.totalAmount);
      }
      const now = new Date();
      setMonthlySales(
        Array.from({ length: 6 }, (_, i) => {
          const d = new Date(now.getFullYear(), now.getMonth() - (5 - i), 1);
          const key = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}`;
          return { key, value: monthTotals.get(key) ?? 0 };
        })
      );
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

  async function handleAskQuestion(e: React.FormEvent) {
    e.preventDefault();
    if (!qaQuestion.trim()) return;
    setQaLoading(true);
    setQaError(null);
    setQaAnswer(null);
    try {
      const res = await AiAssistantApi.ask(qaQuestion);
      setQaAnswer(res.data.answer);
    } catch (err) {
      setQaError(getErrorMessage(err));
    } finally {
      setQaLoading(false);
    }
  }

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

  const severityTone: Record<Alert["severity"], string> = { 1: "bad", 2: "warn", 3: "info" };
  const hour = new Date().getHours();
  const greeting = hour < 12 ? t.dashboard.greetingMorning : t.dashboard.greetingEvening;
  const firstName = (user?.fullName ?? "").split(" ")[0];
  const today = new Date().toLocaleDateString(lang === "ar" ? "ar" : "en", { weekday: "long", day: "numeric", month: "long", year: "numeric" });
  const monthLabels = useMemo(
    () => monthlySales.map((m) => ({ label: new Date(`${m.key}-01T12:00:00`).toLocaleDateString(lang === "ar" ? "ar" : "en", { month: "short" }), value: m.value })),
    [monthlySales, lang]
  );
  const bucketLabels = [t.dashboard.bucketCurrent, t.dashboard.bucket31, t.dashboard.bucket61, t.dashboard.bucket90];
  const tones: Bucket["tone"][] = ["ok", "warn", "late", "bad"];
  const toBuckets = (values: number[]) => values.map((value, i) => ({ label: bucketLabels[i], value, tone: tones[i] }));

  return (
    <div className="d2">
      <section className="d2-hero">
        <div className="d2-hero-text">
          <div className="d2-date">{today}</div>
          <h1>
            {greeting}
            {firstName ? `، ${firstName}` : ""}
          </h1>
          <p>{t.dashboard.greetingSub}</p>
          <form onSubmit={handleAskQuestion} className="d2-ask">
            <input
              type="text"
              value={qaQuestion}
              onChange={(e) => setQaQuestion(e.target.value)}
              placeholder={t.dashboard.askQuestionPlaceholder}
              disabled={qaLoading}
              aria-label={t.dashboard.askQuestionTitle}
            />
            <button type="submit" disabled={qaLoading || !qaQuestion.trim()}>
              {qaLoading ? t.common.loading : t.dashboard.askQuestionSubmit}
            </button>
          </form>
          {qaError && <div className="d2-ask-note bad">{qaError}</div>}
          {qaAnswer && <div className="d2-ask-note">{qaAnswer}</div>}
        </div>
        <div className="d2-hero-health">
          <Ring score={healthScore} />
          <div className="d2-hero-health-label">{t.dashboard.healthScore}</div>
          <div className="d2-hero-health-note">{t.dashboard.healthScoreNote}</div>
        </div>
      </section>

      <section className="d2-kpis">
        <Kpi delay={0} icon={<IconList />} label={t.dashboard.monthRevenue} value={monthRevenue} delta={<Delta current={monthRevenue} previous={priorRevenue} />} />
        <Kpi delay={60} icon={<IconBook />} label={t.dashboard.netProfit} value={netProfit} tone={netProfit >= 0 ? "good" : "bad"} delta={<Delta current={netProfit} previous={priorProfit} />} />
        <Kpi delay={120} icon={<IconChat />} label={t.dashboard.profitMargin} value={profitMargin} suffix="%" delta={<Delta current={profitMargin} previous={priorMarginPct} points />} />
        <Kpi delay={180} icon={<IconCart />} label={t.dashboard.availableLiquidity} value={availableLiquidity} tone={cashFlowNet >= 0 ? "good" : "bad"} delta={<span className={`d2-delta ${cashFlowNet >= 0 ? "up" : "down"}`}>{cashFlowNet >= 0 ? "▲" : "▼"} {Math.abs(cashFlowNet).toLocaleString()}</span>} />
      </section>

      <section className="d2-grid">
        <div className="d2-panel d2-span-2">
          <header>
            <h3>{t.dashboard.sales6Months}</h3>
          </header>
          <BarChart items={monthLabels} empty={t.dashboard.noChartData} />
        </div>
        <div className="d2-panel">
          <header>
            <h3>🚨 {t.dashboard.needsAttention}</h3>
          </header>
          <div className="d2-alerts">
            {smartAlerts.length === 0 && <div className="d2-alert clear">✓ {t.dashboard.allClear}</div>}
            {smartAlerts.map((a) => (
              <Link key={a.to + a.label} to={a.to} className={`d2-alert ${severityTone[a.severity]}`}>
                <span className="d2-alert-count">{a.badge}</span>
                <span className="d2-alert-label">{a.label}</span>
                <span className="d2-alert-go">›</span>
              </Link>
            ))}
          </div>
        </div>

        <div className="d2-panel">
          <header>
            <h3>{t.dashboard.receivablesAging}</h3>
            <span className="d2-panel-total">{arBuckets.reduce((a, b) => a + b, 0).toLocaleString()}</span>
          </header>
          <BarChart items={toBuckets(arBuckets)} empty={t.dashboard.noChartData} />
        </div>
        <div className="d2-panel">
          <header>
            <h3>{t.dashboard.payablesAging}</h3>
            <span className="d2-panel-total">{apBuckets.reduce((a, b) => a + b, 0).toLocaleString()}</span>
          </header>
          <BarChart items={toBuckets(apBuckets)} empty={t.dashboard.noChartData} />
        </div>
        <div className="d2-panel">
          <header>
            <h3>{t.dashboard.salesVsPurchases}</h3>
          </header>
          <div className="d2-hbars">
            <div className="d2-hbar">
              <span>{t.dashboard.totalSales}</span>
              <div className="d2-hbar-track"><div className="d2-hbar-fill brand" style={{ width: `${(totalSales / salesVsPurchasesMax) * 100}%` }} /></div>
              <b>{totalSales.toLocaleString()}</b>
            </div>
            <div className="d2-hbar">
              <span>{t.dashboard.totalPurchases}</span>
              <div className="d2-hbar-track"><div className="d2-hbar-fill gold" style={{ width: `${(totalPurchases / salesVsPurchasesMax) * 100}%` }} /></div>
              <b>{totalPurchases.toLocaleString()}</b>
            </div>
            <div className="d2-hbar">
              <span>{t.dashboard.arOutstanding}</span>
              <div className="d2-hbar-track"><div className="d2-hbar-fill brand" style={{ width: `${(arOutstanding / arVsApMax) * 100}%` }} /></div>
              <b>{arOutstanding.toLocaleString()}</b>
            </div>
            <div className="d2-hbar">
              <span>{t.dashboard.apOutstanding}</span>
              <div className="d2-hbar-track"><div className="d2-hbar-fill gold" style={{ width: `${(apOutstanding / arVsApMax) * 100}%` }} /></div>
              <b>{apOutstanding.toLocaleString()}</b>
            </div>
          </div>
        </div>
      </section>

      <section className="d2-section">
        <h3>{t.dashboard.quickActions}</h3>
        <div className="d2-quick">
          {quickLinks.map((item, i) => (
            <Link key={item.to} to={item.to} className="d2-quick-tile" style={{ animationDelay: `${i * 40}ms` }}>
              <span className="d2-quick-icon">{item.icon}</span>
              <span>{item.label}</span>
            </Link>
          ))}
        </div>
      </section>

      <section className="d2-section">
        <h3>{t.dashboard.financialSnapshot}</h3>
        <div className="d2-facts">
          {[
            [t.dashboard.cogsLabel, cogs.toLocaleString()],
            [t.dashboard.breakEvenLabel, breakEvenSales.toLocaleString()],
            [t.dashboard.collectionRate, `${collectionRate.toFixed(1)}%`],
            [t.dashboard.inventoryValue, inventoryValue.toLocaleString()],
            [t.dashboard.accountsCount, String(accountsCount)],
            [t.dashboard.employeesCount, String(employeesCount)],
            [t.dashboard.totalDebit, totalDebit.toLocaleString()],
            [t.dashboard.totalCredit, totalCredit.toLocaleString()],
          ].map(([label, value]) => (
            <div key={label} className="d2-fact">
              <span>{label}</span>
              <b>{value}</b>
            </div>
          ))}
        </div>
      </section>
    </div>
  );
}
