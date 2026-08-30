import { useState } from "react";
import { FinancialReportsApi } from "../../api/services";
import type { LaborReport } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";

function firstDayOfMonth() {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-01`;
}

export default function LaborReportPage() {
  const { t } = useLanguage();
  const [fromDate, setFromDate] = useState(firstDayOfMonth());
  const [toDate, setToDate] = useState(new Date().toISOString().slice(0, 10));
  const [report, setReport] = useState<LaborReport | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function load() {
    setError(null);
    try {
      const res = await FinancialReportsApi.laborReport(fromDate, toDate);
      setReport(res.data);
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  return (
    <div>
      <div className="page-header">
        <h1>{t.hr.laborReportTitle}</h1>
      </div>
      <p className="text-muted">{t.hr.laborReportIntro}</p>

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
        <>
          <div className="stat-grid" style={{ marginTop: 16, marginBottom: 16 }}>
            <div className="stat-card">
              <div className="label">{t.accounting.totalRevenue}</div>
              <div className="value">{report.totalSalesRevenue.toLocaleString()}</div>
            </div>
            <div className="stat-card">
              <div className="label">{t.hr.totalPayroll}</div>
              <div className="value">{report.totalPayroll.toLocaleString()}</div>
            </div>
            <div className="stat-card">
              <div className="label">{t.hr.laborCostPercent}</div>
              <div className="value">{report.laborCostPercent != null ? `${report.laborCostPercent.toFixed(1)}%` : t.common.noData}</div>
            </div>
          </div>

          <div className="card">
            <h3>{t.hr.salesByEmployee}</h3>
            <p className="text-muted" style={{ fontSize: 13 }}>{t.hr.salesByEmployeeNote}</p>
            {report.salesByEmployee.length === 0 && <div className="text-muted">{t.common.noData}</div>}
            {report.salesByEmployee.length > 0 && (
              <table>
                <thead>
                  <tr>
                    <th>{t.common.description}</th>
                    <th>{t.hr.ordersHandled}</th>
                    <th>{t.common.total}</th>
                  </tr>
                </thead>
                <tbody>
                  {report.salesByEmployee.map((l) => (
                    <tr key={l.employeeId}>
                      <td>{l.employeeName}</td>
                      <td>{l.orderCount}</td>
                      <td>{l.salesTotal.toLocaleString()}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        </>
      )}
    </div>
  );
}
