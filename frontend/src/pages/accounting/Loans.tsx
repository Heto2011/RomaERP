import { useEffect, useState } from "react";
import { AccountsApi, LoansApi } from "../../api/services";
import { AccountType, type Account, type Loan } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";

const money = (n: number) => n.toLocaleString(undefined, { maximumFractionDigits: 2 });
const today = () => new Date().toISOString().slice(0, 10);

export default function Loans() {
  const { t } = useLanguage();
  const d = t.accounting;
  const [loans, setLoans] = useState<Loan[]>([]);
  const [accounts, setAccounts] = useState<Account[]>([]);
  const [open, setOpen] = useState<string | null>(null);
  const [description, setDescription] = useState("");
  const [lender, setLender] = useState("");
  const [principal, setPrincipal] = useState("");
  const [rate, setRate] = useState("0");
  const [term, setTerm] = useState("12");
  const [received, setReceived] = useState(today());
  const [firstDue, setFirstDue] = useState(() => {
    const dt = new Date();
    dt.setMonth(dt.getMonth() + 1);
    return dt.toISOString().slice(0, 10);
  });
  const [loanAccountId, setLoanAccountId] = useState("");
  const [bankAccountId, setBankAccountId] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function load() {
    try {
      const [list, accs] = await Promise.all([LoansApi.getAll(), AccountsApi.getAll()]);
      setLoans(list.data);
      const usable = accs.data.filter((a) => a.isActive && !a.isControlAccount).sort((x, y) => x.code.localeCompare(y.code));
      setAccounts(usable);
      setLoanAccountId((cur) => cur || usable.find((a) => a.code === "2210")?.id || "");
      setBankAccountId((cur) => cur || usable.find((a) => a.code === "1112")?.id || "");
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  useEffect(() => {
    load();
  }, []);

  const liabilities = accounts.filter((a) => a.accountType === AccountType.Liability);
  const assets = accounts.filter((a) => a.accountType === AccountType.Asset);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    setMessage(null);
    try {
      await LoansApi.create({
        description,
        lender: lender || undefined,
        principal: Number(principal),
        annualInterestPercent: Number(rate),
        termMonths: Number(term),
        disbursementDate: received,
        firstInstallmentDate: firstDue,
        loanAccountId,
        bankAccountId,
      });
      setDescription("");
      setLender("");
      setPrincipal("");
      setMessage(d.loanCreated);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setBusy(false);
    }
  }

  async function pay(installmentId: string) {
    const date = window.prompt(d.loanPayDatePrompt, today());
    if (!date) return;
    setBusy(true);
    setError(null);
    setMessage(null);
    try {
      await LoansApi.payInstallment(installmentId, date);
      setMessage(d.loanPaid);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setBusy(false);
    }
  }

  async function settle(loanId: string) {
    if (!window.confirm(d.loanSettleConfirm)) return;
    const date = window.prompt(d.loanPayDatePrompt, today());
    if (!date) return;
    const extra = window.prompt(d.loanSettleExtraPrompt, "0");
    if (extra === null) return;
    setBusy(true);
    setError(null);
    setMessage(null);
    try {
      await LoansApi.settle(loanId, date, Number(extra) || 0);
      setMessage(d.loanSettled);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setBusy(false);
    }
  }

  return (
    <div>
      <div className="page-header">
        <h1>{d.loansTitle}</h1>
      </div>
      <p className="text-muted" style={{ marginTop: 0 }}>{d.loansHint}</p>

      {error && <div className="alert alert-error">{error}</div>}
      {message && <div className="alert alert-success">{message}</div>}

      <form className="card" onSubmit={submit}>
        <div className="form-grid">
          <div className="form-field">
            <label>{d.loanDescription}</label>
            <input value={description} maxLength={200} onChange={(e) => setDescription(e.target.value)} required />
          </div>
          <div className="form-field">
            <label>{d.loanLender}</label>
            <input value={lender} maxLength={200} onChange={(e) => setLender(e.target.value)} />
          </div>
          <div className="form-field">
            <label>{d.loanPrincipal}</label>
            <input type="number" min="0.01" step="0.01" value={principal} onChange={(e) => setPrincipal(e.target.value)} required />
          </div>
          <div className="form-field">
            <label>{d.loanRate}</label>
            <input type="number" min="0" max="100" step="0.01" value={rate} onChange={(e) => setRate(e.target.value)} required />
          </div>
          <div className="form-field">
            <label>{d.loanTerm}</label>
            <input type="number" min="1" max="360" value={term} onChange={(e) => setTerm(e.target.value)} required />
          </div>
          <div className="form-field">
            <label>{d.loanReceivedOn}</label>
            <input type="date" value={received} onChange={(e) => setReceived(e.target.value)} required />
          </div>
          <div className="form-field">
            <label>{d.loanFirstDue}</label>
            <input type="date" value={firstDue} onChange={(e) => setFirstDue(e.target.value)} required />
          </div>
          <div className="form-field">
            <label>{d.loanAccount}</label>
            <select value={loanAccountId} onChange={(e) => setLoanAccountId(e.target.value)} required>
              <option value="">-</option>
              {liabilities.map((a) => (
                <option key={a.id} value={a.id}>{a.code} - {a.nameAr}</option>
              ))}
            </select>
          </div>
          <div className="form-field">
            <label>{d.loanBank}</label>
            <select value={bankAccountId} onChange={(e) => setBankAccountId(e.target.value)} required>
              <option value="">-</option>
              {assets.map((a) => (
                <option key={a.id} value={a.id}>{a.code} - {a.nameAr}</option>
              ))}
            </select>
          </div>
        </div>
        <button className="btn" disabled={busy || !loanAccountId || !bankAccountId}>{d.loanSave}</button>
      </form>

      <div style={{ marginTop: 16, display: "flex", flexDirection: "column", gap: 12 }}>
        {loans.length === 0 && <div className="card text-muted">{d.loanNone}</div>}
        {loans.map((l) => (
          <div className="card" key={l.id} style={l.status === 1 ? { opacity: 0.6 } : undefined}>
            <div style={{ display: "flex", justifyContent: "space-between", gap: 12, flexWrap: "wrap" }}>
              <div>
                <h3 style={{ margin: 0 }}>{l.description}</h3>
                <div className="text-muted">
                  {l.lender ? `${l.lender} · ` : ""}{money(l.principal)} · {l.annualInterestPercent}% · {l.termMonths} {d.loanMonthsShort}
                </div>
              </div>
              <div style={{ textAlign: "end" }}>
                <div style={{ fontSize: 20, fontWeight: 700 }}>{money(l.remaining)}</div>
                <div className="text-muted">{d.loanRemaining} ({l.paidInstallments}/{l.termMonths})</div>
              </div>
            </div>
            {l.nextDueDate && (
              <div style={{ marginTop: 8, fontWeight: 600, color: l.nextIsOverdue ? "var(--color-danger, #b42318)" : undefined }}>
                {l.nextIsOverdue ? d.loanOverdue : d.loanNextDue}: {l.nextDueDate.slice(0, 10)} — {money(l.nextDueAmount ?? 0)}
              </div>
            )}
            <button className="btn btn-secondary btn-sm" style={{ marginTop: 8 }} onClick={() => setOpen(open === l.id ? null : l.id)}>
              {open === l.id ? d.loanHideSchedule : d.loanShowSchedule}
            </button>
            {l.status === 0 && (
              <button className="btn btn-secondary btn-sm" style={{ marginTop: 8, marginInlineStart: 8 }} disabled={busy} onClick={() => settle(l.id)}>
                {d.loanSettle}
              </button>
            )}
            {open === l.id && (
              <div style={{ overflowX: "auto", marginTop: 8 }}>
                <table className="table">
                  <thead>
                    <tr>
                      <th>#</th>
                      <th>{d.loanDue}</th>
                      <th>{d.loanPrincipalPart}</th>
                      <th>{d.loanInterestPart}</th>
                      <th>{t.common.total}</th>
                      <th></th>
                    </tr>
                  </thead>
                  <tbody>
                    {l.installments.map((i) => (
                      <tr key={i.id} style={i.isPaid ? { opacity: 0.55 } : undefined}>
                        <td>{i.number}</td>
                        <td>{i.dueDate.slice(0, 10)}</td>
                        <td>{money(i.principalPart)}</td>
                        <td>{money(i.interestPart)}</td>
                        <td>{money(i.total)}</td>
                        <td>
                          {i.isPaid ? (
                            <span>{d.loanPaidOn} {i.paidDate?.slice(0, 10)}</span>
                          ) : (
                            <button className="btn btn-sm" disabled={busy} onClick={() => pay(i.id)}>{d.loanPay}</button>
                          )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        ))}
      </div>
    </div>
  );
}
