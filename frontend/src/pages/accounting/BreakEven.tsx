import { useState } from "react";
import { FinancialReportsApi, SalesApi } from "../../api/services";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";
import InfoTooltip from "../../components/InfoTooltip";
import EmptyReportState from "../../components/EmptyReportState";

const COGS_ACCOUNT_CODE = "5500";

function firstDayOfMonth() {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-01`;
}

interface Result {
  fixedCosts: number;
  contributionMarginRatio: number;
  breakEvenSales: number;
  targetSales: number;
  dailyBreakEven: number;
  weeklyBreakEven: number;
  avgTicket: number;
  requiredInvoicesPerDay: number | null;
}

export default function BreakEvenPage() {
  const { t } = useLanguage();
  const [fromDate, setFromDate] = useState(firstDayOfMonth());
  const [toDate, setToDate] = useState(new Date().toISOString().slice(0, 10));
  const [targetProfit, setTargetProfit] = useState(0);
  const [result, setResult] = useState<Result | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [emptyReason, setEmptyReason] = useState<"revenue" | "expense" | null>(null);

  async function load() {
    setError(null);
    setEmptyReason(null);
    try {
      const [incomeRes, invoicesRes] = await Promise.all([
        FinancialReportsApi.incomeStatement(fromDate, toDate),
        SalesApi.getInvoices(),
      ]);
      const income = incomeRes.data;

      if (income.totalRevenue === 0) {
        setEmptyReason("revenue");
        setResult(null);
        return;
      }
      if (income.totalExpense === 0) {
        setEmptyReason("expense");
        setResult(null);
        return;
      }

      const cogs = income.expenseLines.find((l) => l.accountCode === COGS_ACCOUNT_CODE)?.amount ?? 0;
      const fixedCosts = income.totalExpense - cogs;
      const contributionMarginRatio = income.totalRevenue > 0 ? (income.totalRevenue - cogs) / income.totalRevenue : 0;
      const breakEvenSales = contributionMarginRatio > 0 ? fixedCosts / contributionMarginRatio : 0;
      const targetSales = contributionMarginRatio > 0 ? (fixedCosts + targetProfit) / contributionMarginRatio : 0;

      const days = Math.max(1, Math.round((new Date(toDate).getTime() - new Date(fromDate).getTime()) / 86400000) + 1);
      const dailyBreakEven = breakEvenSales / days;
      const weeklyBreakEven = dailyBreakEven * 7;

      const invoicesInRange = invoicesRes.data.filter((i) => i.invoiceDate >= fromDate && i.invoiceDate <= toDate);
      const avgTicket = invoicesInRange.length > 0
        ? invoicesInRange.reduce((sum, i) => sum + i.totalAmount, 0) / invoicesInRange.length
        : 0;
      const requiredInvoicesPerDay = avgTicket > 0 ? Math.ceil(dailyBreakEven / avgTicket) : null;

      setResult({ fixedCosts, contributionMarginRatio, breakEvenSales, targetSales, dailyBreakEven, weeklyBreakEven, avgTicket, requiredInvoicesPerDay });
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  return (
    <div>
      <div className="page-header">
        <h1>{t.accounting.breakEvenTitle}<InfoTooltip text={t.accounting.breakEvenIntro} /></h1>
      </div>
      <p className="text-muted">{t.accounting.breakEvenIntro}</p>

      <div className="card toolbar">
        <div className="form-field">
          <label>{t.common.from}</label>
          <input type="date" value={fromDate} onChange={(e) => setFromDate(e.target.value)} />
        </div>
        <div className="form-field">
          <label>{t.common.to}</label>
          <input type="date" value={toDate} onChange={(e) => setToDate(e.target.value)} />
        </div>
        <div className="form-field">
          <label>{t.accounting.targetProfit}</label>
          <input type="number" value={targetProfit} onChange={(e) => setTargetProfit(Number(e.target.value))} />
        </div>
        <button className="btn" style={{ alignSelf: "flex-end" }} onClick={load}>
          {t.common.viewReport}
        </button>
      </div>

      {error && <div className="alert-error">{error}</div>}

      {emptyReason === "revenue" && (
        <EmptyReportState
          message={t.accounting.emptyNeedsRevenue}
          actions={[{ label: t.nav.salesInvoices, to: "/sales/invoices" }]}
        />
      )}
      {emptyReason === "expense" && (
        <EmptyReportState
          message={t.accounting.emptyNeedsExpense}
          actions={[
            { label: t.hr.payrollRunsTitle, to: "/hr/payroll" },
            { label: t.accounting.journalEntriesTitle, to: "/accounting/journal-entries" },
          ]}
        />
      )}

      {result && (
        <div className="card">
          <table style={{ maxWidth: 480 }}>
            <tbody>
              <tr><td>{t.accounting.fixedCosts}</td><td style={{ textAlign: "end" }}>{result.fixedCosts.toLocaleString()}</td></tr>
              <tr><td>{t.accounting.contributionMarginRatio}</td><td style={{ textAlign: "end" }}>{(result.contributionMarginRatio * 100).toFixed(1)}%</td></tr>
              <tr>
                <td><strong>{t.accounting.breakEvenSales}</strong></td>
                <td style={{ textAlign: "end" }}><strong>{result.breakEvenSales.toLocaleString()}</strong></td>
              </tr>
              <tr>
                <td><strong>{t.accounting.targetSales}</strong></td>
                <td style={{ textAlign: "end" }}><strong>{result.targetSales.toLocaleString()}</strong></td>
              </tr>
            </tbody>
          </table>

          <div className="text-muted" style={{ marginTop: 16 }}>{t.accounting.breakEvenBreakdown}</div>
          <table style={{ maxWidth: 480 }}>
            <tbody>
              <tr><td>{t.accounting.weeklyTarget}</td><td style={{ textAlign: "end" }}>{result.weeklyBreakEven.toLocaleString()}</td></tr>
              <tr><td>{t.accounting.dailyTarget}</td><td style={{ textAlign: "end" }}>{result.dailyBreakEven.toLocaleString()}</td></tr>
              <tr><td>{t.accounting.avgTicket}</td><td style={{ textAlign: "end" }}>{result.avgTicket.toLocaleString()}</td></tr>
              <tr>
                <td>{t.accounting.requiredInvoicesPerDay}</td>
                <td style={{ textAlign: "end" }}>{result.requiredInvoicesPerDay ?? t.common.noData}</td>
              </tr>
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
