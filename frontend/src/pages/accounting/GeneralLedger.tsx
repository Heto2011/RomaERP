import { useEffect, useRef, useState } from "react";
import { AccountsApi, FinancialReportsApi } from "../../api/services";
import type { Account, GeneralLedger as GeneralLedgerData } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";
import ReportExportBar from "../../components/ReportExportBar";

function firstDayOfYear() {
  return `${new Date().getFullYear()}-01-01`;
}

export default function GeneralLedger() {
  const { t } = useLanguage();
  const exportRef = useRef<HTMLDivElement>(null);
  const [accounts, setAccounts] = useState<Account[]>([]);
  const [accountId, setAccountId] = useState("");
  const [fromDate, setFromDate] = useState(firstDayOfYear());
  const [toDate, setToDate] = useState(new Date().toISOString().slice(0, 10));
  const [data, setData] = useState<GeneralLedgerData | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    AccountsApi.getAll()
      .then((r) => setAccounts(r.data.filter((a) => a.isActive).sort((a, b) => a.code.localeCompare(b.code))))
      .catch((err) => setError(getErrorMessage(err)));
  }, []);

  async function load() {
    if (!accountId) return;
    setError(null);
    try {
      const res = await FinancialReportsApi.generalLedger(accountId, fromDate, toDate);
      setData(res.data);
    } catch (err) {
      setData(null);
      setError(getErrorMessage(err));
    }
  }

  return (
    <div>
      <div className="page-header">
        <h1>{t.accounting.generalLedgerTitle}</h1>
      </div>

      <div className="card toolbar">
        <div className="form-field" style={{ minWidth: 260 }}>
          <label>{t.common.account}</label>
          <select value={accountId} onChange={(e) => setAccountId(e.target.value)}>
            <option value="">{t.accounting.selectAccount}</option>
            {accounts.map((a) => (
              <option key={a.id} value={a.id}>
                {a.code} - {a.nameAr}
              </option>
            ))}
          </select>
        </div>
        <div className="form-field">
          <label>{t.common.from}</label>
          <input type="date" value={fromDate} onChange={(e) => setFromDate(e.target.value)} />
        </div>
        <div className="form-field">
          <label>{t.common.to}</label>
          <input type="date" value={toDate} onChange={(e) => setToDate(e.target.value)} />
        </div>
        <button className="btn" style={{ alignSelf: "flex-end" }} onClick={load} disabled={!accountId}>
          {t.common.viewReport}
        </button>
      </div>

      {error && <div className="alert-error">{error}</div>}
      {!data && !error && <p className="text-muted">{t.accounting.pickFirstHint}</p>}

      {data && (
        <>
          <ReportExportBar targetRef={exportRef} fileName={`general-ledger-${data.accountCode}`} />
          <div className="card" ref={exportRef}>
            <h3 style={{ marginTop: 0 }}>
              {data.accountCode} - {data.accountName}
            </h3>
            <table>
              <thead>
                <tr>
                  <th>{t.common.date}</th>
                  <th>{t.accounting.entryNumber}</th>
                  <th>{t.common.description}</th>
                  <th>{t.accounting.debit}</th>
                  <th>{t.accounting.credit}</th>
                  <th>{t.common.balance}</th>
                </tr>
              </thead>
              <tbody>
                <tr>
                  <td colSpan={5}>
                    <strong>{t.accounting.openingBalance}</strong>
                  </td>
                  <td>
                    <strong>{data.openingBalance.toLocaleString()}</strong>
                  </td>
                </tr>
                {data.lines.map((l, i) => (
                  <tr key={`${l.entryNumber}-${i}`}>
                    <td>{new Date(l.entryDate).toLocaleDateString()}</td>
                    <td>{l.entryNumber}</td>
                    <td>{l.description ?? ""}</td>
                    <td>{l.debit ? l.debit.toLocaleString() : ""}</td>
                    <td>{l.credit ? l.credit.toLocaleString() : ""}</td>
                    <td>{l.balance.toLocaleString()}</td>
                  </tr>
                ))}
                {data.lines.length === 0 && (
                  <tr>
                    <td colSpan={6} className="text-muted">
                      {t.accounting.noMovements}
                    </td>
                  </tr>
                )}
              </tbody>
              <tfoot>
                <tr>
                  <td colSpan={3}>
                    <strong>{t.common.total}</strong>
                  </td>
                  <td>
                    <strong>{data.totalDebit.toLocaleString()}</strong>
                  </td>
                  <td>
                    <strong>{data.totalCredit.toLocaleString()}</strong>
                  </td>
                  <td>
                    <strong>
                      {t.accounting.closingBalance}: {data.closingBalance.toLocaleString()}
                    </strong>
                  </td>
                </tr>
              </tfoot>
            </table>
          </div>
        </>
      )}
    </div>
  );
}
