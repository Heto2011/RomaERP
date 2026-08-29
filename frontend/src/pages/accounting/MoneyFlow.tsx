import { useState } from "react";
import { FinancialReportsApi } from "../../api/services";
import type { IncomeStatement } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";

function firstDayOfMonth() {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-01`;
}

export default function MoneyFlowPage() {
  const { t } = useLanguage();
  const [fromDate, setFromDate] = useState(firstDayOfMonth());
  const [toDate, setToDate] = useState(new Date().toISOString().slice(0, 10));
  const [report, setReport] = useState<IncomeStatement | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function load() {
    setError(null);
    try {
      const res = await FinancialReportsApi.incomeStatement(fromDate, toDate);
      setReport(res.data);
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  const revenue = report?.totalRevenue ?? 0;
  const pct = (amount: number) => (revenue > 0 ? (amount / revenue) * 100 : 0);
  const netMargin = pct(report?.netIncome ?? 0);

  return (
    <div>
      <div className="page-header">
        <h1>{t.accounting.moneyFlowTitle}</h1>
      </div>
      <p className="text-muted">{t.accounting.moneyFlowIntro}</p>

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

      {report && revenue === 0 && <div className="card text-muted">{t.common.noData}</div>}

      {report && revenue > 0 && (
        <div className="card">
          <div style={{ marginBottom: 6, fontSize: 13, color: "var(--color-muted)" }}>
            {t.dashboard.totalSales}: <strong>{revenue.toLocaleString()}</strong>
          </div>

          {report.expenseLines.map((l) => (
            <div className="dash-bar-row" key={l.accountCode}>
              <span className="dash-bar-label">{l.accountName}</span>
              <div className="dash-bar-track">
                <div className="dash-bar-fill dash-bar-accent" style={{ width: `${Math.min(pct(l.amount), 100)}%` }} />
              </div>
              <span className="dash-bar-value">{pct(l.amount).toFixed(1)}% · {l.amount.toLocaleString()}</span>
            </div>
          ))}

          <div className="dash-bar-row" style={{ marginTop: 10 }}>
            <span className="dash-bar-label"><strong>{t.accounting.netProfitMargin}</strong></span>
            <div className="dash-bar-track">
              <div
                className="dash-bar-fill dash-bar-primary"
                style={{ width: `${Math.min(Math.max(netMargin, 0), 100)}%` }}
              />
            </div>
            <span className="dash-bar-value">
              <strong className={netMargin >= 0 ? "text-success" : "text-danger"}>
                {netMargin.toFixed(1)}% · {report.netIncome.toLocaleString()}
              </strong>
            </span>
          </div>
        </div>
      )}
    </div>
  );
}
