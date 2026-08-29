import { useEffect, useState } from "react";
import { useLocation } from "react-router-dom";
import { FinancialReportsApi } from "../../api/services";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";

const COGS_ACCOUNT_CODE = "5500";

function firstDayOfMonth() {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-01`;
}

interface Result {
  revenue: number;
  cogs: number;
  grossProfit: number;
  grossMarginRatio: number;
  operatingExpenses: number;
  netProfit: number;
  netMarginRatio: number;
  contributionMarginRatio: number;
}

export default function MarginAnalysisPage() {
  const { t } = useLanguage();
  const location = useLocation();
  const [fromDate, setFromDate] = useState(firstDayOfMonth());
  const [toDate, setToDate] = useState(new Date().toISOString().slice(0, 10));
  const [result, setResult] = useState<Result | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function load() {
    setError(null);
    try {
      const res = await FinancialReportsApi.incomeStatement(fromDate, toDate);
      const income = res.data;
      const cogs = income.expenseLines.find((l) => l.accountCode === COGS_ACCOUNT_CODE)?.amount ?? 0;
      const grossProfit = income.totalRevenue - cogs;
      const grossMarginRatio = income.totalRevenue > 0 ? grossProfit / income.totalRevenue : 0;
      const operatingExpenses = income.totalExpense - cogs;
      const netProfit = income.netIncome;
      const netMarginRatio = income.totalRevenue > 0 ? netProfit / income.totalRevenue : 0;
      const contributionMarginRatio = income.totalRevenue > 0 ? (income.totalRevenue - cogs) / income.totalRevenue : 0;

      setResult({ revenue: income.totalRevenue, cogs, grossProfit, grossMarginRatio, operatingExpenses, netProfit, netMarginRatio, contributionMarginRatio });
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    if (!result || !location.hash) return;
    const el = document.getElementById(location.hash.slice(1));
    el?.scrollIntoView({ behavior: "smooth", block: "start" });
  }, [result, location.hash]);

  return (
    <div>
      <div className="page-header">
        <h1>{t.accounting.marginAnalysisTitle}</h1>
      </div>
      <p className="text-muted">{t.accounting.marginAnalysisIntro}</p>

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

      {result && (
        <>
          <div className="card">
            <table style={{ maxWidth: 480 }}>
              <tbody>
                <tr id="gross-margin"><td>{t.accounting.grossMarginRatio}</td><td style={{ textAlign: "end" }}>{(result.grossMarginRatio * 100).toFixed(1)}%</td></tr>
                <tr id="net-margin"><td>{t.accounting.netMarginRatio}</td><td style={{ textAlign: "end" }}>{(result.netMarginRatio * 100).toFixed(1)}%</td></tr>
                <tr id="contribution-margin"><td>{t.accounting.contributionMarginRatio}</td><td style={{ textAlign: "end" }}>{(result.contributionMarginRatio * 100).toFixed(1)}%</td></tr>
              </tbody>
            </table>
          </div>

          <div className="card" id="real-profit">
            <h3 style={{ marginTop: 0 }}>💰 {t.accounting.realProfitTitle}</h3>
            <div className="text-muted" style={{ marginBottom: 8 }}>{t.accounting.profitWaterfall}</div>
            <table style={{ maxWidth: 480 }}>
              <tbody>
                <tr><td>{t.accounting.revenue}</td><td style={{ textAlign: "end" }}>{result.revenue.toLocaleString()}</td></tr>
                <tr><td>− {t.accounting.cost} (COGS)</td><td style={{ textAlign: "end" }}>{result.cogs.toLocaleString()}</td></tr>
                <tr>
                  <td><strong>= {t.accounting.grossProfit}</strong></td>
                  <td style={{ textAlign: "end" }}><strong>{result.grossProfit.toLocaleString()}</strong></td>
                </tr>
                <tr><td>− {t.accounting.operatingExpenses}</td><td style={{ textAlign: "end" }}>{result.operatingExpenses.toLocaleString()}</td></tr>
                <tr>
                  <td><strong>= {t.dashboard.netProfit}</strong></td>
                  <td style={{ textAlign: "end" }} className={result.netProfit >= 0 ? "text-success" : "text-danger"}>
                    <strong>{result.netProfit.toLocaleString()}</strong>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </>
      )}
    </div>
  );
}
