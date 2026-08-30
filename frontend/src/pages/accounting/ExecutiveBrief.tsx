import { useState } from "react";
import { FinancialReportsApi } from "../../api/services";
import type { CashFlowIntelligence, HiddenProfitReport, IncomeStatement, ItemProfitabilityReport } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";

const COGS_ACCOUNT_CODE = "5500";
const SALARIES_ACCOUNT_CODE = "5100";

function firstDayOfMonth() {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-01`;
}

function toIso(d: Date) {
  return d.toISOString().slice(0, 10);
}

function priorPeriod(fromDate: string, toDate: string) {
  const from = new Date(fromDate);
  const to = new Date(toDate);
  const lengthDays = Math.round((to.getTime() - from.getTime()) / 86400000) + 1;
  const priorTo = new Date(from);
  priorTo.setDate(priorTo.getDate() - 1);
  const priorFrom = new Date(priorTo);
  priorFrom.setDate(priorFrom.getDate() - (lengthDays - 1));
  return { fromDate: toIso(priorFrom), toDate: toIso(priorTo) };
}

function pctChange(current: number, prior: number): number | null {
  if (prior === 0) return null;
  return ((current - prior) / Math.abs(prior)) * 100;
}

interface Brief {
  current: IncomeStatement;
  prior: IncomeStatement;
  items: ItemProfitabilityReport;
  cash: CashFlowIntelligence;
  hiddenProfit: HiddenProfitReport;
}

export default function ExecutiveBriefPage() {
  const { t } = useLanguage();
  const [fromDate, setFromDate] = useState(firstDayOfMonth());
  const [toDate, setToDate] = useState(new Date().toISOString().slice(0, 10));
  const [brief, setBrief] = useState<Brief | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function load() {
    setError(null);
    try {
      const prior = priorPeriod(fromDate, toDate);
      const [currentRes, priorRes, itemsRes, cashRes, hiddenRes] = await Promise.all([
        FinancialReportsApi.incomeStatement(fromDate, toDate),
        FinancialReportsApi.incomeStatement(prior.fromDate, prior.toDate),
        FinancialReportsApi.itemProfitability(fromDate, toDate),
        FinancialReportsApi.cashFlowIntelligence(new Date().toISOString().slice(0, 10)),
        FinancialReportsApi.hiddenProfit(fromDate, toDate),
      ]);
      setBrief({ current: currentRes.data, prior: priorRes.data, items: itemsRes.data, cash: cashRes.data, hiddenProfit: hiddenRes.data });
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  function deltaLabel(current: number, prior: number) {
    const pct = pctChange(current, prior);
    if (pct === null) return t.accounting.noPriorPeriodData;
    const sign = pct >= 0 ? "+" : "";
    return `${sign}${pct.toFixed(1)}% ${t.accounting.vsPriorPeriod}`;
  }

  const cogsAmount = brief?.current.expenseLines.find((l) => l.accountCode === COGS_ACCOUNT_CODE)?.amount ?? 0;
  const laborAmount = brief?.current.expenseLines.find((l) => l.accountCode === SALARIES_ACCOUNT_CODE)?.amount ?? 0;
  const revenue = brief?.current.totalRevenue ?? 0;
  const grossProfit = revenue - cogsAmount;
  const grossMarginPct = revenue !== 0 ? (grossProfit / revenue) * 100 : 0;
  const netMarginPct = revenue !== 0 ? ((brief?.current.netIncome ?? 0) / revenue) * 100 : 0;
  const cogsPctOfRevenue = revenue !== 0 ? (cogsAmount / revenue) * 100 : 0;
  const laborPctOfRevenue = revenue !== 0 ? (laborAmount / revenue) * 100 : 0;

  const soldItems = brief ? brief.items.items.filter((i) => i.quantitySold > 0) : [];
  const topItems = [...soldItems].sort((a, b) => b.grossProfit - a.grossProfit).slice(0, 5);
  const bottomItems = [...soldItems].sort((a, b) => a.marginPercent - b.marginPercent).slice(0, 5);

  return (
    <div>
      <div className="page-header">
        <h1>{t.accounting.executiveBriefTitle}</h1>
      </div>
      <p className="text-muted">{t.accounting.executiveBriefIntro}</p>

      <div className="card toolbar">
        <div className="form-field">
          <label>{t.common.from}</label>
          <input type="date" value={fromDate} onChange={(e) => setFromDate(e.target.value)} />
        </div>
        <div className="form-field">
          <label>{t.common.to}</label>
          <input type="date" value={toDate} onChange={(e) => setToDate(e.target.value)} />
        </div>
        <button className="btn" style={{ alignSelf: "flex-end" }} onClick={load}>
          {t.common.viewReport}
        </button>
      </div>

      {error && <div className="alert-error">{error}</div>}

      {brief && (
        <>
          {brief.cash.firstWeekBelowZero && (
            <div className="alert-error">
              {t.accounting.cashFlowNegativeAlert} — {new Date(brief.cash.firstWeekBelowZero).toLocaleDateString()}
            </div>
          )}

          <div className="stat-grid" style={{ marginTop: 16, marginBottom: 16 }}>
            <div className="stat-card">
              <div className="label">{t.accounting.totalRevenue}</div>
              <div className="value">{revenue.toLocaleString()}</div>
              <div className="text-muted" style={{ fontSize: 12, marginTop: 4 }}>{deltaLabel(revenue, brief.prior.totalRevenue)}</div>
            </div>
            <div className="stat-card">
              <div className="label">{t.accounting.netIncome}</div>
              <div className="value" style={{ color: brief.current.netIncome >= 0 ? "var(--color-success)" : "var(--color-danger)" }}>
                {brief.current.netIncome.toLocaleString()}
              </div>
              <div className="text-muted" style={{ fontSize: 12, marginTop: 4 }}>{deltaLabel(brief.current.netIncome, brief.prior.netIncome)}</div>
            </div>
            <div className="stat-card">
              <div className="label">{t.accounting.grossMarginPercent}</div>
              <div className="value">{grossMarginPct.toFixed(1)}%</div>
            </div>
            <div className="stat-card">
              <div className="label">{t.accounting.netMarginPercent}</div>
              <div className="value">{netMarginPct.toFixed(1)}%</div>
            </div>
            <div className="stat-card">
              <div className="label">{t.accounting.cogsPercentOfRevenue}</div>
              <div className="value">{cogsPctOfRevenue.toFixed(1)}%</div>
            </div>
            <div className="stat-card">
              <div className="label">{t.accounting.laborPercentOfRevenue}</div>
              <div className="value">{laborPctOfRevenue.toFixed(1)}%</div>
            </div>
            <div className="stat-card">
              <div className="label">{t.accounting.currentCashBalance}</div>
              <div className="value">{brief.cash.currentCashBalance.toLocaleString()}</div>
            </div>
            <div className="stat-card">
              <div className="label">{t.accounting.hiddenProfitTotal}</div>
              <div className="value" style={{ color: brief.hiddenProfit.totalImpact >= 0 ? "var(--color-success)" : "var(--color-danger)" }}>
                {brief.hiddenProfit.totalImpact.toLocaleString()}
              </div>
            </div>
          </div>

          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 16 }}>
            <div className="card">
              <h3>{t.accounting.topPerformingItems}</h3>
              {topItems.length === 0 && <div className="text-muted">{t.common.noData}</div>}
              {topItems.length > 0 && (
                <table>
                  <thead>
                    <tr>
                      <th>{t.common.description}</th>
                      <th>{t.accounting.grossProfit}</th>
                      <th>{t.accounting.margin}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {topItems.map((i) => (
                      <tr key={i.itemId}>
                        <td>{i.itemName}</td>
                        <td className="text-success">{i.grossProfit.toLocaleString()}</td>
                        <td>{i.marginPercent.toFixed(1)}%</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </div>

            <div className="card">
              <h3>{t.accounting.bottomPerformingItems}</h3>
              {bottomItems.length === 0 && <div className="text-muted">{t.common.noData}</div>}
              {bottomItems.length > 0 && (
                <table>
                  <thead>
                    <tr>
                      <th>{t.common.description}</th>
                      <th>{t.accounting.grossProfit}</th>
                      <th>{t.accounting.margin}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {bottomItems.map((i) => (
                      <tr key={i.itemId}>
                        <td>{i.itemName}</td>
                        <td className={i.grossProfit >= 0 ? "text-success" : "text-danger"}>{i.grossProfit.toLocaleString()}</td>
                        <td>{i.marginPercent.toFixed(1)}%</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </div>
          </div>
        </>
      )}
    </div>
  );
}
