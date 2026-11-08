import { useEffect, useState } from "react";
import { AccountsApi, DeferralsApi } from "../../api/services";
import { AccountType, type Account, type DeferralSchedule } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";

const money = (n: number) => n.toLocaleString(undefined, { maximumFractionDigits: 2 });
const thisMonth = () => new Date().toISOString().slice(0, 7);

export default function Deferrals() {
  const { t } = useLanguage();
  const d = t.accounting;
  const [rows, setRows] = useState<DeferralSchedule[]>([]);
  const [accounts, setAccounts] = useState<Account[]>([]);
  const [kind, setKind] = useState(0);
  const [description, setDescription] = useState("");
  const [total, setTotal] = useState("");
  const [entryDate, setEntryDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [startMonth, setStartMonth] = useState(thisMonth());
  const [months, setMonths] = useState("12");
  const [targetId, setTargetId] = useState("");
  const [settlementId, setSettlementId] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function load() {
    try {
      const [list, accs] = await Promise.all([DeferralsApi.getAll(), AccountsApi.getAll()]);
      setRows(list.data);
      setAccounts(accs.data.filter((a) => a.isActive && !a.isControlAccount).sort((x, y) => x.code.localeCompare(y.code)));
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  useEffect(() => {
    load();
  }, []);

  const prepaid = kind === 0;
  const targets = accounts.filter((a) => a.accountType === (prepaid ? AccountType.Expense : AccountType.Revenue));
  const settlements = accounts.filter((a) => a.accountType === AccountType.Asset || a.accountType === AccountType.Liability);
  const monthly = (Number(total) || 0) / (Number(months) || 1);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    setMessage(null);
    try {
      await DeferralsApi.create({
        kind,
        description,
        totalAmount: Number(total),
        entryDate,
        startMonth: `${startMonth}-01`,
        months: Number(months),
        targetAccountId: targetId,
        settlementAccountId: settlementId,
      });
      setDescription("");
      setTotal("");
      setMessage(d.deferralCreated);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setBusy(false);
    }
  }

  async function run() {
    setBusy(true);
    setError(null);
    setMessage(null);
    try {
      const res = (await DeferralsApi.run()).data;
      setMessage(d.deferralRunDone.replace("{n}", String(res.entriesPosted)).replace("{amount}", money(res.amountReleased)));
      if (res.skipped.length > 0) setError(res.skipped.join(" — "));
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setBusy(false);
    }
  }

  async function cancel(id: string) {
    if (!window.confirm(d.deferralCancelConfirm)) return;
    setError(null);
    try {
      await DeferralsApi.cancel(id);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  return (
    <div>
      <div className="page-header">
        <h1>{d.deferralsTitle}</h1>
        <button className="btn" onClick={run} disabled={busy}>{d.deferralRun}</button>
      </div>
      <p className="text-muted" style={{ marginTop: 0 }}>{d.deferralsHint}</p>

      {error && <div className="alert alert-error">{error}</div>}
      {message && <div className="alert alert-success">{message}</div>}

      <form className="card" onSubmit={submit}>
        <div className="form-grid">
          <div className="form-field">
            <label>{d.deferralKind}</label>
            <select value={kind} onChange={(e) => { setKind(Number(e.target.value)); setTargetId(""); }}>
              <option value={0}>{d.deferralPrepaid}</option>
              <option value={1}>{d.deferralRevenue}</option>
            </select>
          </div>
          <div className="form-field">
            <label>{d.deferralDescription}</label>
            <input value={description} maxLength={200} onChange={(e) => setDescription(e.target.value)} required />
          </div>
          <div className="form-field">
            <label>{d.deferralTotal}</label>
            <input type="number" min="0.01" step="0.01" value={total} onChange={(e) => setTotal(e.target.value)} required />
          </div>
          <div className="form-field">
            <label>{d.deferralMonths}</label>
            <input type="number" min="2" max="120" value={months} onChange={(e) => setMonths(e.target.value)} required />
          </div>
          <div className="form-field">
            <label>{prepaid ? d.deferralPaidOn : d.deferralCollectedOn}</label>
            <input type="date" value={entryDate} onChange={(e) => setEntryDate(e.target.value)} required />
          </div>
          <div className="form-field">
            <label>{d.deferralStart}</label>
            <input type="month" value={startMonth} onChange={(e) => setStartMonth(e.target.value)} required />
          </div>
          <div className="form-field">
            <label>{prepaid ? d.deferralExpenseAccount : d.deferralRevenueAccount}</label>
            <select value={targetId} onChange={(e) => setTargetId(e.target.value)} required>
              <option value="">-</option>
              {targets.map((a) => (
                <option key={a.id} value={a.id}>{a.code} - {a.nameAr}</option>
              ))}
            </select>
          </div>
          <div className="form-field">
            <label>{prepaid ? d.deferralPaidFrom : d.deferralCollectedInto}</label>
            <select value={settlementId} onChange={(e) => setSettlementId(e.target.value)} required>
              <option value="">-</option>
              {settlements.map((a) => (
                <option key={a.id} value={a.id}>{a.code} - {a.nameAr}</option>
              ))}
            </select>
          </div>
        </div>
        {monthly > 0 && <div className="text-muted" style={{ margin: "8px 0" }}>{d.deferralMonthly.replace("{amount}", money(monthly))}</div>}
        <button className="btn" disabled={busy || !targetId || !settlementId}>{d.deferralSave}</button>
      </form>

      <div className="card" style={{ marginTop: 16 }}>
        {rows.length === 0 ? (
          <p className="text-muted">{d.deferralNone}</p>
        ) : (
          <div style={{ overflowX: "auto" }}>
            <table className="table">
              <thead>
                <tr>
                  <th>{d.deferralKind}</th>
                  <th>{d.deferralDescription}</th>
                  <th>{d.deferralTotal}</th>
                  <th>{d.deferralProgress}</th>
                  <th>{d.deferralRemaining}</th>
                  <th>{t.common.status}</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {rows.map((r) => (
                  <tr key={r.id} style={r.status !== 0 ? { opacity: 0.6 } : undefined}>
                    <td>{r.kind === 0 ? d.deferralPrepaid : d.deferralRevenue}</td>
                    <td>{r.description}<div className="text-muted">{r.targetAccountName}</div></td>
                    <td>{money(r.totalAmount)}</td>
                    <td>{r.recognizedMonths} / {r.months}</td>
                    <td>{money(r.remaining)}</td>
                    <td>{r.status === 0 ? d.deferralActive : r.status === 1 ? d.deferralDone : d.deferralCancelled}</td>
                    <td>
                      {r.status === 0 && <button className="btn btn-secondary btn-sm" onClick={() => cancel(r.id)}>{d.deferralCancel}</button>}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}
