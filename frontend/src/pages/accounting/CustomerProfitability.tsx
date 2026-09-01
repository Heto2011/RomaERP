import { useState } from "react";
import { FinancialReportsApi } from "../../api/services";
import type { CustomerProfitabilityReport } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";
import InfoTooltip from "../../components/InfoTooltip";

function firstDayOfMonth() {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-01`;
}

export default function CustomerProfitabilityPage() {
  const { t } = useLanguage();
  const [fromDate, setFromDate] = useState(firstDayOfMonth());
  const [toDate, setToDate] = useState(new Date().toISOString().slice(0, 10));
  const [report, setReport] = useState<CustomerProfitabilityReport | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function load() {
    setError(null);
    try {
      const res = await FinancialReportsApi.customerProfitability(fromDate, toDate);
      setReport(res.data);
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  const topWinners = report ? [...report.customers].sort((a, b) => b.grossProfit - a.grossProfit).slice(0, 10) : [];
  const topLosers = report ? [...report.customers].sort((a, b) => a.grossProfit - b.grossProfit).slice(0, 10) : [];

  function renderTable(rows: typeof topWinners) {
    return (
      <table>
        <thead>
          <tr>
            <th>{t.common.description}</th>
            <th>{t.accounting.revenue}</th>
            <th>{t.accounting.cost}</th>
            <th>{t.accounting.grossProfit}</th>
            <th>{t.accounting.margin}</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((l) => (
            <tr key={l.customerId}>
              <td>{l.customerName}</td>
              <td>{l.revenue.toLocaleString()}</td>
              <td>{l.cost.toLocaleString()}</td>
              <td className={l.grossProfit >= 0 ? "text-success" : "text-danger"}>{l.grossProfit.toLocaleString()}</td>
              <td>{l.marginPercent.toFixed(1)}%</td>
            </tr>
          ))}
        </tbody>
      </table>
    );
  }

  return (
    <div>
      <div className="page-header">
        <h1>{t.accounting.customerProfitabilityTitle}<InfoTooltip text={t.accounting.customerProfitabilityIntro} /></h1>
      </div>
      <p className="text-muted">{t.accounting.customerProfitabilityIntro}</p>

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

      {report && report.customers.length === 0 && <div className="card text-muted">{t.common.noData}</div>}

      {report && report.customers.length > 0 && (
        <>
          <div className="card">
            <h3 style={{ marginTop: 0 }}>🟢 {t.accounting.topWinners}</h3>
            {renderTable(topWinners)}
          </div>
          <div className="card">
            <h3 style={{ marginTop: 0 }}>🔴 {t.accounting.topLosers}</h3>
            {renderTable(topLosers)}
          </div>
        </>
      )}
    </div>
  );
}
