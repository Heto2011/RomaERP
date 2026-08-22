import { useState } from "react";
import { FinancialReportsApi } from "../../api/services";
import type { BalanceSheet } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";

export default function BalanceSheetPage() {
  const { t } = useLanguage();
  const [asOfDate, setAsOfDate] = useState(new Date().toISOString().slice(0, 10));
  const [report, setReport] = useState<BalanceSheet | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function load() {
    setError(null);
    try {
      const res = await FinancialReportsApi.balanceSheet(asOfDate);
      setReport(res.data);
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  return (
    <div>
      <div className="page-header">
        <h1>{t.accounting.balanceSheetTitle}</h1>
      </div>

      <div className="card toolbar">
        <div className="form-field">
          <label>{t.accounting.asOfDate}</label>
          <input type="date" value={asOfDate} onChange={(e) => setAsOfDate(e.target.value)} />
        </div>
        <button className="btn" style={{ alignSelf: "flex-end" }} onClick={load}>
          {t.common.viewReport}
        </button>
      </div>

      {error && <div className="alert-error">{error}</div>}

      {report && (
        <div className="card">
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 24 }}>
            <div>
              <h3 style={{ marginTop: 0 }}>{t.accounting.assets}</h3>
              <table>
                <tbody>
                  {report.assetLines.map((l) => (
                    <tr key={l.accountCode}>
                      <td>{l.accountCode}</td>
                      <td>{l.accountName}</td>
                      <td>{l.amount.toLocaleString()}</td>
                    </tr>
                  ))}
                  <tr>
                    <td colSpan={2}><strong>{t.accounting.totalAssets}</strong></td>
                    <td><strong>{report.totalAssets.toLocaleString()}</strong></td>
                  </tr>
                </tbody>
              </table>
            </div>

            <div>
              <h3 style={{ marginTop: 0 }}>{t.accounting.liabilities}</h3>
              <table>
                <tbody>
                  {report.liabilityLines.map((l) => (
                    <tr key={l.accountCode}>
                      <td>{l.accountCode}</td>
                      <td>{l.accountName}</td>
                      <td>{l.amount.toLocaleString()}</td>
                    </tr>
                  ))}
                  <tr>
                    <td colSpan={2}><strong>{t.accounting.totalLiabilities}</strong></td>
                    <td><strong>{report.totalLiabilities.toLocaleString()}</strong></td>
                  </tr>
                </tbody>
              </table>

              <h3>{t.accounting.equity}</h3>
              <table>
                <tbody>
                  {report.equityLines.map((l) => (
                    <tr key={l.accountCode}>
                      <td>{l.accountCode}</td>
                      <td>{l.accountName}</td>
                      <td>{l.amount.toLocaleString()}</td>
                    </tr>
                  ))}
                  <tr>
                    <td colSpan={2}>{t.accounting.currentYearNetIncome}</td>
                    <td>{report.currentYearNetIncome.toLocaleString()}</td>
                  </tr>
                  <tr>
                    <td colSpan={2}><strong>{t.accounting.totalEquity}</strong></td>
                    <td><strong>{report.totalEquity.toLocaleString()}</strong></td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>

          <table style={{ marginTop: 20 }}>
            <tbody>
              <tr>
                <td><strong>{t.accounting.totalLiabilitiesAndEquity}</strong></td>
                <td>
                  <strong className={report.isBalanced ? "text-success" : "text-danger"}>
                    {report.totalLiabilitiesAndEquity.toLocaleString()} {report.isBalanced ? t.accounting.balanced : t.accounting.notBalanced}
                  </strong>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
