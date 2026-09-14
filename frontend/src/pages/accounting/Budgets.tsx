import { useEffect, useState } from "react";
import { AccountsApi, BudgetsApi, FiscalPeriodsAdminApi } from "../../api/services";
import { AccountType, type Account, type BudgetLine, type BudgetVsActualReport, type FiscalYearDetail } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";
import { bilingualName } from "../../i18n/bilingual";

export default function Budgets() {
  const { t, lang } = useLanguage();
  const [years, setYears] = useState<FiscalYearDetail[]>([]);
  const [accounts, setAccounts] = useState<Account[]>([]);
  const [fiscalYearId, setFiscalYearId] = useState("");
  const [lines, setLines] = useState<BudgetLine[]>([]);
  const [report, setReport] = useState<BudgetVsActualReport | null>(null);
  const [error, setError] = useState<string | null>(null);

  const [accountId, setAccountId] = useState("");
  const [fiscalPeriodId, setFiscalPeriodId] = useState("");
  const [amount, setAmount] = useState(0);

  const budgetableAccounts = accounts.filter(
    (a) => !a.isControlAccount && (a.accountType === AccountType.Revenue || a.accountType === AccountType.Expense)
  );
  const selectedYear = years.find((y) => y.id === fiscalYearId);

  async function loadStatic() {
    const [yearsRes, accountsRes] = await Promise.all([FiscalPeriodsAdminApi.getAllYears(), AccountsApi.getAll()]);
    setYears(yearsRes.data);
    setAccounts(accountsRes.data);
    if (yearsRes.data.length > 0) setFiscalYearId(yearsRes.data[0].id);
  }

  async function loadYearData(yearId: string) {
    const [linesRes, reportRes] = await Promise.all([BudgetsApi.getLines(yearId), BudgetsApi.getVsActual(yearId)]);
    setLines(linesRes.data);
    setReport(reportRes.data);
  }

  useEffect(() => {
    loadStatic();
  }, []);

  useEffect(() => {
    if (fiscalYearId) loadYearData(fiscalYearId);
  }, [fiscalYearId]);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    try {
      await BudgetsApi.setLine({ accountId, fiscalPeriodId, amount });
      setAmount(0);
      await loadYearData(fiscalYearId);
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  function renderVarianceLine(line: { accountCode: string; accountName: string; budgetedAmount: number; actualAmount: number; varianceAmount: number; variancePercent: number | null }, favorableWhenPositive: boolean) {
    const isFavorable = favorableWhenPositive ? line.varianceAmount >= 0 : line.varianceAmount <= 0;
    return (
      <tr key={line.accountCode}>
        <td>{line.accountCode} - {line.accountName}</td>
        <td>{line.budgetedAmount.toLocaleString()}</td>
        <td>{line.actualAmount.toLocaleString()}</td>
        <td className={isFavorable ? "text-success" : "text-danger"}>{line.varianceAmount.toLocaleString()}</td>
        <td className={isFavorable ? "text-success" : "text-danger"}>{line.variancePercent === null ? "—" : `${line.variancePercent}%`}</td>
      </tr>
    );
  }

  return (
    <div>
      <div className="page-header">
        <h1>{t.accounting.budgetsTitle}</h1>
      </div>
      <p className="text-muted">{t.accounting.budgetsIntro}</p>

      {error && <div className="alert-error">{error}</div>}

      <div className="card">
        <div className="form-field" style={{ maxWidth: 260 }}>
          <label>{t.common.fiscalPeriod}</label>
          <select value={fiscalYearId} onChange={(e) => setFiscalYearId(e.target.value)}>
            {years.map((y) => (
              <option key={y.id} value={y.id}>{y.name}</option>
            ))}
          </select>
        </div>
      </div>

      <div className="card">
        <h3>{t.accounting.setBudgetLineTitle}</h3>
        <form onSubmit={handleSubmit}>
          <div className="form-grid">
            <div className="form-field">
              <label>{t.common.account}</label>
              <select value={accountId} onChange={(e) => setAccountId(e.target.value)} required>
                <option value="" disabled>-</option>
                {budgetableAccounts.map((a) => (
                  <option key={a.id} value={a.id}>{a.code} - {bilingualName(a.nameAr, a.nameEn, lang)}</option>
                ))}
              </select>
            </div>
            <div className="form-field">
              <label>{t.accounting.period}</label>
              <select value={fiscalPeriodId} onChange={(e) => setFiscalPeriodId(e.target.value)} required>
                <option value="" disabled>-</option>
                {selectedYear?.periods.map((p) => (
                  <option key={p.id} value={p.id}>{p.name}</option>
                ))}
              </select>
            </div>
            <div className="form-field">
              <label>{t.accounting.budgetedAmount}</label>
              <input type="number" min={0} step="0.01" value={amount} onChange={(e) => setAmount(Number(e.target.value))} required />
            </div>
          </div>
          <button className="btn" type="submit" style={{ marginTop: 14 }}>{t.common.save}</button>
        </form>

        {lines.length > 0 && (
          <table style={{ marginTop: 16 }}>
            <thead>
              <tr>
                <th>{t.common.account}</th>
                <th>{t.accounting.period}</th>
                <th>{t.accounting.budgetedAmount}</th>
              </tr>
            </thead>
            <tbody>
              {lines.map((l) => (
                <tr key={l.id}>
                  <td>{l.accountCode} - {l.accountName}</td>
                  <td>{l.fiscalPeriodName}</td>
                  <td>{l.amount.toLocaleString()}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {report && (
        <div className="card">
          <h3>{t.accounting.budgetVsActualTitle} — {report.fiscalYearName}</h3>

          <h4>{t.accounting.revenueSection}</h4>
          <table>
            <thead>
              <tr>
                <th>{t.common.account}</th>
                <th>{t.accounting.budgetedAmount}</th>
                <th>{t.accounting.actualAmount}</th>
                <th>{t.accounting.variance}</th>
                <th>{t.accounting.variancePercent}</th>
              </tr>
            </thead>
            <tbody>
              {report.revenueLines.length === 0 && (
                <tr><td colSpan={5} className="text-muted" style={{ textAlign: "center", padding: 12 }}>{t.common.noData}</td></tr>
              )}
              {report.revenueLines.map((l) => renderVarianceLine(l, true))}
            </tbody>
            <tfoot>
              <tr style={{ fontWeight: 700 }}>
                <td>{t.common.total}</td>
                <td>{report.totalBudgetedRevenue.toLocaleString()}</td>
                <td>{report.totalActualRevenue.toLocaleString()}</td>
                <td colSpan={2}>{(report.totalActualRevenue - report.totalBudgetedRevenue).toLocaleString()}</td>
              </tr>
            </tfoot>
          </table>

          <h4 style={{ marginTop: 20 }}>{t.accounting.expenseSection}</h4>
          <table>
            <thead>
              <tr>
                <th>{t.common.account}</th>
                <th>{t.accounting.budgetedAmount}</th>
                <th>{t.accounting.actualAmount}</th>
                <th>{t.accounting.variance}</th>
                <th>{t.accounting.variancePercent}</th>
              </tr>
            </thead>
            <tbody>
              {report.expenseLines.length === 0 && (
                <tr><td colSpan={5} className="text-muted" style={{ textAlign: "center", padding: 12 }}>{t.common.noData}</td></tr>
              )}
              {report.expenseLines.map((l) => renderVarianceLine(l, false))}
            </tbody>
            <tfoot>
              <tr style={{ fontWeight: 700 }}>
                <td>{t.common.total}</td>
                <td>{report.totalBudgetedExpense.toLocaleString()}</td>
                <td>{report.totalActualExpense.toLocaleString()}</td>
                <td colSpan={2}>{(report.totalActualExpense - report.totalBudgetedExpense).toLocaleString()}</td>
              </tr>
            </tfoot>
          </table>

          <div className="card" style={{ marginTop: 16, maxWidth: 420, marginInlineStart: "auto" }}>
            <div style={{ display: "flex", justifyContent: "space-between" }}><span>{t.accounting.budgetedNetIncome}</span><strong>{report.budgetedNetIncome.toLocaleString()}</strong></div>
            <div style={{ display: "flex", justifyContent: "space-between", marginTop: 6 }}><span>{t.accounting.actualNetIncome}</span><strong>{report.actualNetIncome.toLocaleString()}</strong></div>
          </div>
        </div>
      )}
    </div>
  );
}
