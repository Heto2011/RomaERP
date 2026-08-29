import { useState } from "react";
import { FinancialReportsApi } from "../../api/services";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";

function firstDayOfMonth() {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-01`;
}

interface DriverRow {
  code: string;
  label: string;
  current: number;
  prior: number;
  delta: number;
  impact: number;
}

interface Result {
  currentNetIncome: number;
  priorNetIncome: number;
  netIncomeChange: number;
  drivers: DriverRow[];
}

function addDays(dateStr: string, days: number) {
  const d = new Date(dateStr);
  d.setDate(d.getDate() + days);
  return d.toISOString().slice(0, 10);
}

export default function BottleneckPage() {
  const { t } = useLanguage();
  const [fromDate, setFromDate] = useState(firstDayOfMonth());
  const [toDate, setToDate] = useState(new Date().toISOString().slice(0, 10));
  const [result, setResult] = useState<Result | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function load() {
    setError(null);
    try {
      const periodDays = Math.max(1, Math.round((new Date(toDate).getTime() - new Date(fromDate).getTime()) / 86400000) + 1);
      const priorTo = addDays(fromDate, -1);
      const priorFrom = addDays(priorTo, -(periodDays - 1));

      const [currentRes, priorRes] = await Promise.all([
        FinancialReportsApi.incomeStatement(fromDate, toDate),
        FinancialReportsApi.incomeStatement(priorFrom, priorTo),
      ]);
      const current = currentRes.data;
      const prior = priorRes.data;

      const drivers: DriverRow[] = [];

      const revenueDelta = current.totalRevenue - prior.totalRevenue;
      drivers.push({
        code: "REVENUE",
        label: t.dashboard.totalSales,
        current: current.totalRevenue,
        prior: prior.totalRevenue,
        delta: revenueDelta,
        impact: revenueDelta,
      });

      const codes = new Set([...current.expenseLines.map((l) => l.accountCode), ...prior.expenseLines.map((l) => l.accountCode)]);
      for (const code of codes) {
        const currentLine = current.expenseLines.find((l) => l.accountCode === code);
        const priorLine = prior.expenseLines.find((l) => l.accountCode === code);
        const currentAmount = currentLine?.amount ?? 0;
        const priorAmount = priorLine?.amount ?? 0;
        const delta = currentAmount - priorAmount;
        drivers.push({
          code,
          label: currentLine?.accountName ?? priorLine?.accountName ?? code,
          current: currentAmount,
          prior: priorAmount,
          delta,
          impact: -delta,
        });
      }

      drivers.sort((a, b) => a.impact - b.impact);

      setResult({
        currentNetIncome: current.netIncome,
        priorNetIncome: prior.netIncome,
        netIncomeChange: current.netIncome - prior.netIncome,
        drivers,
      });
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  return (
    <div>
      <div className="page-header">
        <h1>{t.accounting.bottleneckTitle}</h1>
      </div>
      <p className="text-muted">{t.accounting.bottleneckIntro}</p>

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

      {result && (
        <div className="card">
          <table style={{ maxWidth: 480, marginBottom: 16 }}>
            <tbody>
              <tr><td>{t.accounting.priorPeriodProfit}</td><td style={{ textAlign: "end" }}>{result.priorNetIncome.toLocaleString()}</td></tr>
              <tr><td>{t.accounting.currentPeriodProfit}</td><td style={{ textAlign: "end" }}>{result.currentNetIncome.toLocaleString()}</td></tr>
              <tr>
                <td><strong>{t.accounting.profitChange}</strong></td>
                <td style={{ textAlign: "end" }} className={result.netIncomeChange >= 0 ? "text-success" : "text-danger"}>
                  <strong>{result.netIncomeChange >= 0 ? "+" : ""}{result.netIncomeChange.toLocaleString()}</strong>
                </td>
              </tr>
            </tbody>
          </table>

          <div className="text-muted" style={{ marginBottom: 6 }}>{t.accounting.topDrivers}</div>
          <table>
            <thead>
              <tr>
                <th>{t.common.description}</th>
                <th>{t.accounting.priorPeriodProfit}</th>
                <th>{t.common.total}</th>
                <th>{t.accounting.changeAmount}</th>
                <th>{t.accounting.impactOnProfit}</th>
              </tr>
            </thead>
            <tbody>
              {result.drivers.slice(0, 8).map((d) => (
                <tr key={d.code}>
                  <td>{d.label}</td>
                  <td>{d.prior.toLocaleString()}</td>
                  <td>{d.current.toLocaleString()}</td>
                  <td>{d.delta >= 0 ? "+" : ""}{d.delta.toLocaleString()}</td>
                  <td className={d.impact >= 0 ? "text-success" : "text-danger"}>
                    {d.impact >= 0 ? "+" : ""}{d.impact.toLocaleString()}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
