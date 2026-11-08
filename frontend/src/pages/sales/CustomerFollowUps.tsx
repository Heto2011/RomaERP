import { useEffect, useState } from "react";
import { CustomerFollowUpsApi } from "../../api/services";
import type { OverdueCustomer } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";

const money = (n: number) => n.toLocaleString(undefined, { maximumFractionDigits: 2 });
const today = () => new Date().toISOString().slice(0, 10);

export default function CustomerFollowUps() {
  const { t } = useLanguage();
  const f = t.sales;
  const [rows, setRows] = useState<OverdueCustomer[]>([]);
  const [open, setOpen] = useState<string | null>(null);
  const [method, setMethod] = useState("");
  const [note, setNote] = useState("");
  const [promised, setPromised] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function load() {
    try {
      setRows((await CustomerFollowUpsApi.overdue()).data);
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  useEffect(() => {
    load();
  }, []);

  async function save(customerId: string) {
    setBusy(true);
    setError(null);
    setMessage(null);
    try {
      await CustomerFollowUpsApi.add({ customerId, followUpDate: today(), method, note: note || undefined, promisedPayDate: promised || undefined });
      setMethod("");
      setNote("");
      setPromised("");
      setOpen(null);
      setMessage(f.followUpSaved);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setBusy(false);
    }
  }

  async function sendEmail(customerId: string) {
    setBusy(true);
    setError(null);
    setMessage(null);
    try {
      const res = await CustomerFollowUpsApi.emailReminder(customerId);
      if (res.data.sent) setMessage(res.data.message ?? f.followUpEmailSent);
      else setError(res.data.message ?? f.followUpEmailFailed);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setBusy(false);
    }
  }

  const total = rows.reduce((s, r) => s + r.totalOverdue, 0);

  return (
    <div>
      <div className="page-header">
        <h1>{f.followUpTitle}</h1>
      </div>
      <p className="text-muted" style={{ marginTop: 0 }}>{f.followUpHint}</p>

      {error && <div className="alert alert-error">{error}</div>}
      {message && <div className="alert alert-success">{message}</div>}

      <div className="card" style={{ marginBottom: 16 }}>
        <strong>{f.followUpTotal}:</strong> {money(total)} — {rows.length} {f.followUpCustomers}
      </div>

      {rows.length === 0 && <div className="card text-muted">{f.followUpNone}</div>}

      {rows.map((r) => (
        <div className="card" key={r.customerId} style={{ marginBottom: 12 }}>
          <div style={{ display: "flex", justifyContent: "space-between", gap: 12, flexWrap: "wrap" }}>
            <div>
              <h3 style={{ margin: 0 }}>{r.customerName}</h3>
              <div className="text-muted">
                {r.phone && <span dir="ltr">{r.phone}</span>}
                {r.phone && r.email && " · "}
                {r.email && <span dir="ltr">{r.email}</span>}
                {!r.phone && !r.email && f.followUpNoContact}
              </div>
            </div>
            <div style={{ textAlign: "end" }}>
              <div style={{ fontSize: 22, fontWeight: 700 }}>{money(r.totalOverdue)}</div>
              <div className="text-muted">{f.followUpOldest.replace("{n}", String(r.oldestDaysOverdue))}</div>
            </div>
          </div>

          <div style={{ overflowX: "auto", marginTop: 10 }}>
            <table className="table">
              <thead>
                <tr>
                  <th>{f.followUpInvoice}</th>
                  <th>{f.followUpDue}</th>
                  <th>{f.followUpDays}</th>
                  <th>{f.followUpLeft}</th>
                </tr>
              </thead>
              <tbody>
                {r.invoices.map((i) => (
                  <tr key={i.invoiceId}>
                    <td>{i.invoiceNumber}</td>
                    <td>{i.dueDate.slice(0, 10)}</td>
                    <td>{i.daysOverdue}</td>
                    <td>{money(i.outstanding)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {r.lastFollowUp && (
            <div className="text-muted" style={{ marginTop: 8 }}>
              {f.followUpLast}: {r.lastFollowUp.followUpDate.slice(0, 10)} — {r.lastFollowUp.method}
              {r.lastFollowUp.note ? ` — ${r.lastFollowUp.note}` : ""}
              {r.lastFollowUp.promisedPayDate ? ` — ${f.followUpPromised} ${r.lastFollowUp.promisedPayDate.slice(0, 10)}` : ""}
            </div>
          )}

          <div style={{ display: "flex", gap: 8, marginTop: 10, flexWrap: "wrap" }}>
            <button className="btn btn-sm" onClick={() => setOpen(open === r.customerId ? null : r.customerId)}>{f.followUpLog}</button>
            <button className="btn btn-secondary btn-sm" disabled={busy || !r.email} onClick={() => sendEmail(r.customerId)} title={r.email ? undefined : f.followUpNoEmail}>
              {f.followUpEmail}
            </button>
          </div>

          {open === r.customerId && (
            <div className="form-grid" style={{ marginTop: 10 }}>
              <div className="form-field">
                <label>{f.followUpMethod}</label>
                <select value={method} onChange={(e) => setMethod(e.target.value)}>
                  <option value="">-</option>
                  {[f.followUpCall, f.followUpVisit, f.followUpMessage, f.followUpOther].map((m) => (
                    <option key={m} value={m}>{m}</option>
                  ))}
                </select>
              </div>
              <div className="form-field">
                <label>{f.followUpPromisedDate}</label>
                <input type="date" value={promised} onChange={(e) => setPromised(e.target.value)} />
              </div>
              <div className="form-field" style={{ gridColumn: "1 / -1" }}>
                <label>{f.followUpNote}</label>
                <input value={note} maxLength={500} onChange={(e) => setNote(e.target.value)} />
              </div>
              <div>
                <button className="btn" disabled={busy || !method} onClick={() => save(r.customerId)}>{f.followUpSave}</button>
              </div>
            </div>
          )}
        </div>
      ))}
    </div>
  );
}
