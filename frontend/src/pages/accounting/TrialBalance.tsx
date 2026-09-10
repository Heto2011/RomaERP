import { useEffect, useState } from "react";
import { JournalEntriesApi } from "../../api/services";
import { AccountType, type TrialBalanceLine } from "../../api/types";
import { useLanguage } from "../../i18n/LanguageContext";

export default function TrialBalance() {
  const { t } = useLanguage();
  const typeLabels: Record<AccountType, string> = {
    [AccountType.Asset]: t.accounting.types.asset,
    [AccountType.Liability]: t.accounting.types.liability,
    [AccountType.Equity]: t.accounting.types.equity,
    [AccountType.Revenue]: t.accounting.types.revenue,
    [AccountType.Expense]: t.accounting.types.expense,
  };
  const [lines, setLines] = useState<TrialBalanceLine[]>([]);
  const [asOfDate, setAsOfDate] = useState("");

  async function load() {
    const res = await JournalEntriesApi.trialBalance(asOfDate || undefined);
    setLines(res.data);
  }

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const totalDebit = lines.reduce((sum, l) => sum + l.totalDebit, 0);
  const totalCredit = lines.reduce((sum, l) => sum + l.totalCredit, 0);

  return (
    <div>
      <div className="page-header">
        <h1>{t.accounting.trialBalanceTitle}</h1>
      </div>

      <div className="card toolbar">
        <div className="form-field">
          <label>{t.accounting.asOfDate}</label>
          <input type="date" value={asOfDate} onChange={(e) => setAsOfDate(e.target.value)} />
        </div>
        <button className="btn" style={{ alignSelf: "flex-end" }} onClick={load}>
          {t.common.refresh}
        </button>
      </div>

      <div className="card">
        <table>
          <thead>
            <tr>
              <th>{t.common.code}</th>
              <th>{t.common.account}</th>
              <th>{t.common.type}</th>
              <th>{t.accounting.debit}</th>
              <th>{t.accounting.credit}</th>
              <th>{t.common.balance}</th>
            </tr>
          </thead>
          <tbody>
            {lines.map((l) => (
              <tr key={l.accountCode}>
                <td>{l.accountCode}</td>
                <td>{l.accountName}</td>
                <td>{typeLabels[l.accountType]}</td>
                <td>{l.totalDebit.toLocaleString()}</td>
                <td>{l.totalCredit.toLocaleString()}</td>
                <td>{l.balance.toLocaleString()}</td>
              </tr>
            ))}
          </tbody>
          <tfoot>
            <tr>
              <td colSpan={3}>
                <strong>{t.common.total}</strong>
              </td>
              <td>
                <strong>{totalDebit.toLocaleString()}</strong>
              </td>
              <td>
                <strong>{totalCredit.toLocaleString()}</strong>
              </td>
              <td>
                <strong className={totalDebit !== totalCredit ? "text-danger" : "text-success"}>
                  {(totalDebit - totalCredit).toLocaleString()}
                </strong>
              </td>
            </tr>
          </tfoot>
        </table>
      </div>
    </div>
  );
}
