import { useEffect, useState } from "react";
import { useLocation } from "react-router-dom";
import { FinancialReportsApi } from "../../api/services";
import type { ItemProfitabilityReport } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";

function firstDayOfMonth() {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-01`;
}

export default function ItemProfitabilityPage() {
  const { t } = useLanguage();
  const location = useLocation();
  const [fromDate, setFromDate] = useState(firstDayOfMonth());
  const [toDate, setToDate] = useState(new Date().toISOString().slice(0, 10));
  const [report, setReport] = useState<ItemProfitabilityReport | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function load() {
    setError(null);
    try {
      const res = await FinancialReportsApi.itemProfitability(fromDate, toDate);
      setReport(res.data);
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    if (!report || !location.hash) return;
    const el = document.getElementById(location.hash.slice(1));
    el?.scrollIntoView({ behavior: "smooth", block: "start" });
  }, [report, location.hash]);

  const topWinners = report ? [...report.items].sort((a, b) => b.grossProfit - a.grossProfit).slice(0, 10) : [];
  const topLosers = report ? [...report.items].sort((a, b) => a.grossProfit - b.grossProfit).slice(0, 10) : [];

  function renderTable(rows: typeof topWinners) {
    return (
      <table>
        <thead>
          <tr>
            <th>{t.common.code}</th>
            <th>{t.common.description}</th>
            <th>{t.accounting.quantitySold}</th>
            <th>{t.accounting.revenue}</th>
            <th>{t.accounting.cost}</th>
            <th>{t.accounting.grossProfit}</th>
            <th>{t.accounting.margin}</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((l) => (
            <tr key={l.itemId}>
              <td>{l.itemCode}</td>
              <td>{l.itemName}</td>
              <td>{l.quantitySold.toLocaleString()}</td>
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
        <h1>{t.accounting.itemProfitabilityTitle}</h1>
      </div>
      <p className="text-muted">{t.accounting.itemProfitabilityIntro}</p>

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

      {report && report.items.length === 0 && <div className="card text-muted">{t.common.noData}</div>}

      {report && report.items.length > 0 && (
        <>
          <div className="card" id="top-winners">
            <h3 style={{ marginTop: 0 }}>🟢 {t.accounting.topWinners}</h3>
            {renderTable(topWinners)}
          </div>
          <div className="card" id="top-losers">
            <h3 style={{ marginTop: 0 }}>🔴 {t.accounting.topLosers}</h3>
            {renderTable(topLosers)}
          </div>
        </>
      )}
    </div>
  );
}
