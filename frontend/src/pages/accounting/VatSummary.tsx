import { useState } from "react";
import { FinancialReportsApi } from "../../api/services";
import type { VatSummary } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";

function firstDayOfMonth() {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-01`;
}

export default function VatSummaryPage() {
  const { t } = useLanguage();
  const [fromDate, setFromDate] = useState(firstDayOfMonth());
  const [toDate, setToDate] = useState(new Date().toISOString().slice(0, 10));
  const [report, setReport] = useState<VatSummary | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function load() {
    setError(null);
    try {
      const res = await FinancialReportsApi.vatSummary(fromDate, toDate);
      setReport(res.data);
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  return (
    <div>
      <div className="page-header">
        <h1>{t.accounting.vatSummaryTitle}</h1>
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
              <tr><td>{t.accounting.outputVat}</td><td style={{ textAlign: "end" }}>{report.outputVat.toLocaleString()}</td></tr>
              <tr><td>{t.accounting.inputVat}</td><td style={{ textAlign: "end" }}>{report.inputVat.toLocaleString()}</td></tr>
              <tr>
                <td><strong>{report.netVatPayable >= 0 ? t.accounting.netVatPayable : t.accounting.netVatRefundable}</strong></td>
                <td style={{ textAlign: "end" }} className={report.netVatPayable >= 0 ? "text-danger" : "text-success"}>
                  <strong>{Math.abs(report.netVatPayable).toLocaleString()}</strong>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
