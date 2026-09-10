import { useState } from "react";
import { FinancialReportsApi } from "../../api/services";
import type { CostCenterAnalysis } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";

function firstDayOfYear() {
  return `${new Date().getFullYear()}-01-01`;
}

export default function CostCenterAnalysisPage() {
  const { t } = useLanguage();
  const [fromDate, setFromDate] = useState(firstDayOfYear());
  const [toDate, setToDate] = useState(new Date().toISOString().slice(0, 10));
  const [report, setReport] = useState<CostCenterAnalysis | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function load() {
    setError(null);
    try {
      const res = await FinancialReportsApi.costCenterAnalysis(fromDate, toDate);
      setReport(res.data);
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  return (
    <div>
      <div className="page-header">
        <h1>{t.accounting.costCenterAnalysisTitle}</h1>
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

      {report && report.costCenters.length === 0 && (
        <div className="card text-muted">{t.common.noData}</div>
      )}

      {report && report.costCenters.map((cc) => (
        <div className="card" key={cc.costCenterId ?? "unassigned"}>
          <h3 style={{ marginTop: 0 }}>
            {cc.costCenterId ? `${cc.costCenterCode} - ${cc.costCenterName}` : t.accounting.unassignedCostCenter}
          </h3>

          {cc.revenueBreakdown.length > 0 && (
            <>
              <div className="text-muted">{t.accounting.revenues}</div>
              <table>
                <tbody>
                  {cc.revenueBreakdown.map((l) => (
                    <tr key={l.accountCode}>
                      <td>{l.accountCode}</td>
                      <td>{l.accountName}</td>
                      <td>{l.amount.toLocaleString()}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </>
          )}

          {cc.expenseBreakdown.length > 0 && (
            <>
              <div className="text-muted" style={{ marginTop: 10 }}>{t.accounting.expenses}</div>
              <table>
                <tbody>
                  {cc.expenseBreakdown.map((l) => (
                    <tr key={l.accountCode}>
                      <td>{l.accountCode}</td>
                      <td>{l.accountName}</td>
                      <td>{l.amount.toLocaleString()}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </>
          )}

          <table style={{ marginTop: 10, width: 320 }}>
            <tbody>
              <tr><td>{t.accounting.totalRevenue}</td><td style={{ textAlign: "end" }}>{cc.totalRevenue.toLocaleString()}</td></tr>
              <tr><td>{t.accounting.totalExpense}</td><td style={{ textAlign: "end" }}>{cc.totalExpense.toLocaleString()}</td></tr>
              <tr>
                <td><strong>{t.accounting.netAmount}</strong></td>
                <td style={{ textAlign: "end" }} className={cc.netAmount >= 0 ? "text-success" : "text-danger"}>
                  <strong>{cc.netAmount.toLocaleString()}</strong>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      ))}
    </div>
  );
}
