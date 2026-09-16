import { useState } from "react";
import { FinancialReportsApi, ManualProfitEntriesApi } from "../../api/services";
import { ManualProfitDimension, type IncomeStatement } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";
import InfoTooltip from "../../components/InfoTooltip";

const COGS_ACCOUNT_CODE = "5500";
const SALARIES_ACCOUNT_CODE = "5100";

function firstDayOfMonth(monthsAgo: number) {
  const d = new Date();
  d.setMonth(d.getMonth() - monthsAgo);
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-01`;
}

function lastDayOfMonth(monthsAgo: number) {
  const d = new Date();
  d.setMonth(d.getMonth() - monthsAgo + 1);
  d.setDate(0);
  return d.toISOString().slice(0, 10);
}

interface Metrics {
  revenue: number;
  cogs: number;
  labor: number;
  grossProfit: number;
  grossMarginPct: number;
  netIncome: number;
  netMarginPct: number;
}

function computeMetrics(income: IncomeStatement): Metrics {
  const cogs = income.expenseLines.find((l) => l.accountCode === COGS_ACCOUNT_CODE)?.amount ?? 0;
  const labor = income.expenseLines.find((l) => l.accountCode === SALARIES_ACCOUNT_CODE)?.amount ?? 0;
  const grossProfit = income.totalRevenue - cogs;
  return {
    revenue: income.totalRevenue,
    cogs,
    labor,
    grossProfit,
    grossMarginPct: income.totalRevenue !== 0 ? (grossProfit / income.totalRevenue) * 100 : 0,
    netIncome: income.netIncome,
    netMarginPct: income.totalRevenue !== 0 ? (income.netIncome / income.totalRevenue) * 100 : 0,
  };
}

function pctChange(a: number, b: number): number | null {
  if (b === 0) return null;
  return ((a - b) / Math.abs(b)) * 100;
}

function currentMonth() {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-01`;
}

function sameMonth(isoDate: string, monthValue: string) {
  return isoDate.slice(0, 7) === monthValue.slice(0, 7);
}

interface DimensionRow {
  name: string;
  revenueA: number;
  costA: number;
  profitA: number;
  marginA: number;
  revenueB: number;
  costB: number;
  profitB: number;
  marginB: number;
}

export default function ComparisonToolPage() {
  const { t } = useLanguage();
  const [mode, setMode] = useState<"period" | "dimension">("period");

  const [fromA, setFromA] = useState(firstDayOfMonth(1));
  const [toA, setToA] = useState(lastDayOfMonth(1));
  const [fromB, setFromB] = useState(firstDayOfMonth(0));
  const [toB, setToB] = useState(new Date().toISOString().slice(0, 10));
  const [metricsA, setMetricsA] = useState<Metrics | null>(null);
  const [metricsB, setMetricsB] = useState<Metrics | null>(null);
  const [error, setError] = useState<string | null>(null);

  const [dimension, setDimension] = useState<ManualProfitDimension>(ManualProfitDimension.Branch);
  const [monthA, setMonthA] = useState(firstDayOfMonth(1));
  const [monthB, setMonthB] = useState(currentMonth());
  const [dimensionRows, setDimensionRows] = useState<DimensionRow[] | null>(null);

  async function load() {
    setError(null);
    try {
      const [resA, resB] = await Promise.all([
        FinancialReportsApi.incomeStatement(fromA, toA),
        FinancialReportsApi.incomeStatement(fromB, toB),
      ]);
      setMetricsA(computeMetrics(resA.data));
      setMetricsB(computeMetrics(resB.data));
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  async function loadDimension() {
    setError(null);
    try {
      const res = await ManualProfitEntriesApi.getAll(dimension);
      const entriesA = res.data.filter((e) => sameMonth(e.periodMonth, monthA));
      const entriesB = res.data.filter((e) => sameMonth(e.periodMonth, monthB));
      const names = Array.from(new Set([...entriesA.map((e) => e.name), ...entriesB.map((e) => e.name)]));
      const rows: DimensionRow[] = names.map((name) => {
        const a = entriesA.find((e) => e.name === name);
        const b = entriesB.find((e) => e.name === name);
        return {
          name,
          revenueA: a?.revenue ?? 0,
          costA: a?.cost ?? 0,
          profitA: a?.grossProfit ?? 0,
          marginA: a?.marginPercent ?? 0,
          revenueB: b?.revenue ?? 0,
          costB: b?.cost ?? 0,
          profitB: b?.grossProfit ?? 0,
          marginB: b?.marginPercent ?? 0,
        };
      });
      rows.sort((r1, r2) => r1.name.localeCompare(r2.name));
      setDimensionRows(rows);
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  const rows: { label: string; a: (m: Metrics) => number; isPct?: boolean }[] = [
    { label: t.accounting.totalRevenue, a: (m) => m.revenue },
    { label: t.dashboard.cogsLabel, a: (m) => m.cogs },
    { label: t.accounting.laborCostLabel, a: (m) => m.labor },
    { label: t.accounting.grossProfit, a: (m) => m.grossProfit },
    { label: t.accounting.grossMarginPercent, a: (m) => m.grossMarginPct, isPct: true },
    { label: t.accounting.netIncome, a: (m) => m.netIncome },
    { label: t.accounting.netMarginPercent, a: (m) => m.netMarginPct, isPct: true },
  ];

  return (
    <div>
      <div className="page-header">
        <h1>{t.accounting.comparisonToolTitle}<InfoTooltip text={t.accounting.comparisonToolIntro} /></h1>
      </div>
      <p className="text-muted">{t.accounting.comparisonToolIntro}</p>

      <div className="toolbar" style={{ marginBottom: 12 }}>
        <button className={mode === "period" ? "btn" : "btn btn-secondary"} onClick={() => setMode("period")}>
          {t.accounting.comparisonModePeriod}
        </button>
        <button className={mode === "dimension" ? "btn" : "btn btn-secondary"} onClick={() => setMode("dimension")}>
          {t.accounting.comparisonModeDimension}
        </button>
      </div>

      {mode === "period" && (
        <>
          <div className="card toolbar" style={{ flexWrap: "wrap" }}>
            <div className="form-field">
              <label>{t.accounting.periodA}</label>
              <div style={{ display: "flex", gap: 8 }}>
                <input type="date" value={fromA} onChange={(e) => setFromA(e.target.value)} />
                <input type="date" value={toA} onChange={(e) => setToA(e.target.value)} />
              </div>
            </div>
            <div className="form-field">
              <label>{t.accounting.periodB}</label>
              <div style={{ display: "flex", gap: 8 }}>
                <input type="date" value={fromB} onChange={(e) => setFromB(e.target.value)} />
                <input type="date" value={toB} onChange={(e) => setToB(e.target.value)} />
              </div>
            </div>
            <button className="btn" style={{ alignSelf: "flex-end" }} onClick={load}>
              {t.common.viewReport}
            </button>
          </div>

          {error && <div className="alert-error">{error}</div>}

          {metricsA && metricsB && (
            <div className="card">
              <table>
                <thead>
                  <tr>
                    <th></th>
                    <th>{t.accounting.periodA}</th>
                    <th>{t.accounting.periodB}</th>
                    <th>{t.accounting.change}</th>
                  </tr>
                </thead>
                <tbody>
                  {rows.map((r) => {
                    const valA = r.a(metricsA);
                    const valB = r.a(metricsB);
                    const delta = pctChange(valB, valA);
                    return (
                      <tr key={r.label}>
                        <td>{r.label}</td>
                        <td>{r.isPct ? `${valA.toFixed(1)}%` : valA.toLocaleString()}</td>
                        <td>{r.isPct ? `${valB.toFixed(1)}%` : valB.toLocaleString()}</td>
                        <td className={delta === null ? "text-muted" : delta >= 0 ? "text-success" : "text-danger"}>
                          {delta === null ? t.accounting.noPriorPeriodData : `${delta >= 0 ? "+" : ""}${delta.toFixed(1)}%`}
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
        </>
      )}

      {mode === "dimension" && (
        <>
          <p className="text-muted">{t.accounting.comparisonDimensionIntro}</p>
          <div className="card toolbar" style={{ flexWrap: "wrap" }}>
            <div className="form-field">
              <label>{t.accounting.dimension}</label>
              <select value={dimension} onChange={(e) => setDimension(Number(e.target.value) as ManualProfitDimension)}>
                <option value={ManualProfitDimension.Branch}>{t.accounting.branchName}</option>
                <option value={ManualProfitDimension.Channel}>{t.accounting.channelName}</option>
              </select>
            </div>
            <div className="form-field">
              <label>{t.accounting.monthA}</label>
              <input type="date" value={monthA} onChange={(e) => setMonthA(e.target.value)} />
            </div>
            <div className="form-field">
              <label>{t.accounting.monthB}</label>
              <input type="date" value={monthB} onChange={(e) => setMonthB(e.target.value)} />
            </div>
            <button className="btn" style={{ alignSelf: "flex-end" }} onClick={loadDimension}>
              {t.common.viewReport}
            </button>
          </div>

          {error && <div className="alert-error">{error}</div>}

          {dimensionRows && dimensionRows.length === 0 && (
            <div className="card text-muted">{t.accounting.noManualEntryData}</div>
          )}

          {dimensionRows && dimensionRows.length > 0 && (
            <div className="card" style={{ overflowX: "auto" }}>
              <table>
                <thead>
                  <tr>
                    <th></th>
                    <th>{t.accounting.revenue} A</th>
                    <th>{t.accounting.revenue} B</th>
                    <th>{t.accounting.change}</th>
                    <th>{t.accounting.grossProfit} A</th>
                    <th>{t.accounting.grossProfit} B</th>
                    <th>{t.accounting.margin} A</th>
                    <th>{t.accounting.margin} B</th>
                  </tr>
                </thead>
                <tbody>
                  {dimensionRows.map((r) => {
                    const delta = pctChange(r.revenueB, r.revenueA);
                    return (
                      <tr key={r.name}>
                        <td>{r.name}</td>
                        <td>{r.revenueA.toLocaleString()}</td>
                        <td>{r.revenueB.toLocaleString()}</td>
                        <td className={delta === null ? "text-muted" : delta >= 0 ? "text-success" : "text-danger"}>
                          {delta === null ? t.accounting.noPriorPeriodData : `${delta >= 0 ? "+" : ""}${delta.toFixed(1)}%`}
                        </td>
                        <td className={r.profitA >= 0 ? "text-success" : "text-danger"}>{r.profitA.toLocaleString()}</td>
                        <td className={r.profitB >= 0 ? "text-success" : "text-danger"}>{r.profitB.toLocaleString()}</td>
                        <td>{r.marginA.toFixed(1)}%</td>
                        <td>{r.marginB.toFixed(1)}%</td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
        </>
      )}
    </div>
  );
}
