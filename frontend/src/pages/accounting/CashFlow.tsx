import { useState } from "react";
import { FinancialReportsApi } from "../../api/services";
import type { CashFlowStatement } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";

function firstDayOfMonth() {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-01`;
}

export default function CashFlowPage() {
  const { t } = useLanguage();
  const [fromDate, setFromDate] = useState(firstDayOfMonth());
  const [toDate, setToDate] = useState(new Date().toISOString().slice(0, 10));
  const [report, setReport] = useState<CashFlowStatement | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function load() {
    setError(null);
    try {
      const res = await FinancialReportsApi.cashFlow(fromDate, toDate);
      setReport(res.data);
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  return (
    <div>
      <div className="page-header">
        <h1>{t.accounting.cashFlowTitle}</h1>
      </div>

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

      {report && (
        <div className="card">
          <table style={{ maxWidth: 480 }}>
            <tbody>
              <tr><td>{t.accounting.beginningCash}</td><td style={{ textAlign: "end" }}>{report.beginningCash.toLocaleString()}</td></tr>
            </tbody>
          </table>

          <div className="text-muted" style={{ marginTop: 16 }}>{t.accounting.cashIn}</div>
          {report.cashInLines.length === 0 && <div className="text-muted">{t.common.noData}</div>}
          {report.cashInLines.length > 0 && (
            <table>
              <tbody>
                {report.cashInLines.map((l) => (
                  <tr key={l.categoryCode}>
                    <td>{l.categoryCode}</td>
                    <td>{l.categoryName}</td>
                    <td className="text-success" style={{ textAlign: "end" }}>+{l.amount.toLocaleString()}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}

          <div className="text-muted" style={{ marginTop: 16 }}>{t.accounting.cashOut}</div>
          {report.cashOutLines.length === 0 && <div className="text-muted">{t.common.noData}</div>}
          {report.cashOutLines.length > 0 && (
            <table>
              <tbody>
                {report.cashOutLines.map((l) => (
                  <tr key={l.categoryCode}>
                    <td>{l.categoryCode}</td>
                    <td>{l.categoryName}</td>
                    <td className="text-danger" style={{ textAlign: "end" }}>-{l.amount.toLocaleString()}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}

          <table style={{ marginTop: 16, maxWidth: 480 }}>
            <tbody>
              <tr><td>{t.accounting.totalCashIn}</td><td className="text-success" style={{ textAlign: "end" }}>+{report.totalCashIn.toLocaleString()}</td></tr>
              <tr><td>{t.accounting.totalCashOut}</td><td className="text-danger" style={{ textAlign: "end" }}>-{report.totalCashOut.toLocaleString()}</td></tr>
              <tr>
                <td><strong>{t.accounting.netCashChange}</strong></td>
                <td style={{ textAlign: "end" }} className={report.netCashChange >= 0 ? "text-success" : "text-danger"}>
                  <strong>{report.netCashChange.toLocaleString()}</strong>
                </td>
              </tr>
              <tr>
                <td><strong>{t.accounting.endingCash}</strong></td>
                <td style={{ textAlign: "end" }}><strong>{report.endingCash.toLocaleString()}</strong></td>
              </tr>
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
