import { useState } from "react";
import { FinancialReportsApi } from "../../api/services";
import type { IncomeStatement } from "../../api/types";
import { getErrorMessage } from "../../api/client";

function firstDayOfYear() {
  return `${new Date().getFullYear()}-01-01`;
}

export default function IncomeStatementPage() {
  const [fromDate, setFromDate] = useState(firstDayOfYear());
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

  return (
    <div>
      <div className="page-header">
        <h1>قائمة الدخل</h1>
      </div>

      <div className="card toolbar">
        <div className="form-field">
          <label>من تاريخ</label>
          <input type="date" value={fromDate} onChange={(e) => setFromDate(e.target.value)} />
        </div>
        <div className="form-field">
          <label>إلى تاريخ</label>
          <input type="date" value={toDate} onChange={(e) => setToDate(e.target.value)} />
        </div>
        <button className="btn" style={{ alignSelf: "flex-end" }} onClick={load}>
          عرض التقرير
        </button>
      </div>

      {error && <div className="alert-error">{error}</div>}

      {report && (
        <div className="card">
          <h3 style={{ marginTop: 0 }}>الإيرادات</h3>
          <table>
            <tbody>
              {report.revenueLines.map((l) => (
                <tr key={l.accountCode}>
                  <td>{l.accountCode}</td>
                  <td>{l.accountName}</td>
                  <td>{l.amount.toLocaleString()}</td>
                </tr>
              ))}
              <tr>
                <td colSpan={2}><strong>إجمالي الإيرادات</strong></td>
                <td><strong>{report.totalRevenue.toLocaleString()}</strong></td>
              </tr>
            </tbody>
          </table>

          <h3>المصروفات</h3>
          <table>
            <tbody>
              {report.expenseLines.map((l) => (
                <tr key={l.accountCode}>
                  <td>{l.accountCode}</td>
                  <td>{l.accountName}</td>
                  <td>{l.amount.toLocaleString()}</td>
                </tr>
              ))}
              <tr>
                <td colSpan={2}><strong>إجمالي المصروفات</strong></td>
                <td><strong>{report.totalExpense.toLocaleString()}</strong></td>
              </tr>
            </tbody>
          </table>

          <table style={{ marginTop: 10 }}>
            <tbody>
              <tr>
                <td><strong>صافي الربح / الخسارة</strong></td>
                <td className={report.netIncome >= 0 ? "text-success" : "text-danger"}>
                  <strong>{report.netIncome.toLocaleString()}</strong>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
