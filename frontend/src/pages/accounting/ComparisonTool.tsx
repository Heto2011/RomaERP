import { useState } from "react";
import { FinancialReportsApi } from "../../api/services";
import type { IncomeStatement } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";

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

export default function ComparisonToolPage() {
  const { t } = useLanguage();
  const [fromA, setFromA] = useState(firstDayOfMonth(1));
  const [toA, setToA] = useState(lastDayOfMonth(1));
  const [fromB, setFromB] = useState(firstDayOfMonth(0));
  const [toB, setToB] = useState(new Date().toISOString().slice(0, 10));
  const [metricsA, setMetricsA] = useState<Metrics | null>(null);
  const [metricsB, setMetricsB] = useState<Metrics | null>(null);
  const [error, setError] = useState<string | null>(null);

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
        <h1>{t.accounting.comparisonToolTitle}</h1>
      </div>
      <p className="text-muted">{t.accounting.comparisonToolIntro}</p>

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
    </div>
  );
}
