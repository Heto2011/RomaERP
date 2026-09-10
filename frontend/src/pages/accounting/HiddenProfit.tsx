import { useState } from "react";
import { FinancialReportsApi } from "../../api/services";
import type { HiddenProfitReport } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";
import InfoTooltip from "../../components/InfoTooltip";

function firstDayOfMonth() {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-01`;
}

export default function HiddenProfitPage() {
  const { t } = useLanguage();
  const [fromDate, setFromDate] = useState(firstDayOfMonth());
  const [toDate, setToDate] = useState(new Date().toISOString().slice(0, 10));
  const [report, setReport] = useState<HiddenProfitReport | null>(null);
  const [error, setError] = useState<string | null>(null);

  const reasonLabel: Record<string, string> = {
    "stock-variance": t.accounting.reasonStockVariance,
    "waste": t.accounting.reasonWaste,
    "below-cost": t.accounting.reasonBelowCost,
  };

  async function load() {
    setError(null);
    try {
      const res = await FinancialReportsApi.hiddenProfit(fromDate, toDate);
      setReport(res.data);
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  return (
    <div>
      <div className="page-header">
        <h1>{t.accounting.hiddenProfitTitle}<InfoTooltip text={t.accounting.hiddenProfitIntro} /></h1>
      </div>
      <p className="text-muted">{t.accounting.hiddenProfitIntro}</p>

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
          <div style={{ textAlign: "center", marginBottom: 16 }}>
            <div className="text-muted">{t.accounting.totalHiddenProfit}</div>
            <div style={{ fontSize: 36, fontWeight: 700 }} className={report.totalImpact >= 0 ? "text-success" : "text-danger"}>
              {report.totalImpact.toLocaleString()}
            </div>
          </div>
          <table>
            <thead>
              <tr>
                <th>{t.common.description}</th>
                <th>{t.accounting.impactOnProfit}</th>
              </tr>
            </thead>
            <tbody>
              {report.lines.map((l) => (
                <tr key={l.reasonCode}>
                  <td>{reasonLabel[l.reasonCode] ?? l.reasonCode}</td>
                  <td className={l.amount >= 0 ? "text-success" : "text-danger"}>{l.amount.toLocaleString()}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
