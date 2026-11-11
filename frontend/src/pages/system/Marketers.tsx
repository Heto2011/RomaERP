import { useState } from "react";
import { MarketersApi } from "../../api/services";
import type { MarketerCommission, MarketerSummary } from "../../api/types";
import { getErrorMessage } from "../../api/client";

const STATUS = ["Waiting / payable", "Paid out", "Voided"];

function money(n: number, currency: string) {
  return `${n.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })} ${currency}`;
}

export default function MarketersPage() {
  const [systemKey, setSystemKey] = useState("");
  const [marketers, setMarketers] = useState<MarketerSummary[] | null>(null);
  const [open, setOpen] = useState<string | null>(null);
  const [rows, setRows] = useState<MarketerCommission[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [form, setForm] = useState({ name: "", code: "", email: "" });

  async function load() {
    if (!systemKey) { setError("Enter the system key first."); return; }
    setError(null);
    try { setMarketers((await MarketersApi.list(systemKey)).data); } catch (e) { setError(getErrorMessage(e)); }
  }

  async function run(action: () => Promise<unknown>, done?: string) {
    setError(null); setMessage(null);
    try { await action(); if (done) setMessage(done); await load(); if (open) await show(open); } catch (e) { setError(getErrorMessage(e)); }
  }

  async function show(id: string) {
    setOpen(id);
    try { setRows((await MarketersApi.commissions(systemKey, id)).data); } catch (e) { setError(getErrorMessage(e)); }
  }

  const now = Date.now();

  return (
    <div style={{ maxWidth: 1000, margin: "40px auto", padding: "0 20px" }}>
      <h1>Marketers and commissions</h1>
      <p className="text-muted">
        20% of every payment in the first year after a customer's first payment, 10% in the second year, nothing after that.
        A commission becomes payable 30 days after the payment. Share a marketer's link as <code>/start-trial?ref=CODE</code>.
      </p>

      <div className="card">
        <div className="form-field">
          <label>System Key</label>
          <input type="password" value={systemKey} onChange={(e) => setSystemKey(e.target.value)} placeholder="X-System-Key" />
        </div>
        <button className="btn btn-secondary btn-sm" onClick={load}>Load</button>
      </div>

      {error && <div className="alert-error" style={{ marginTop: 16 }}>{error}</div>}
      {message && <div className="card" style={{ marginTop: 16 }}>{message}</div>}

      {marketers && (
        <>
          <form
            className="card" style={{ marginTop: 16, display: "grid", gridTemplateColumns: "1fr 1fr 1fr auto", gap: 10, alignItems: "end" }}
            onSubmit={(e) => { e.preventDefault(); run(async () => { await MarketersApi.create(systemKey, { name: form.name, code: form.code || undefined, email: form.email || undefined }); setForm({ name: "", code: "", email: "" }); }, "Marketer added."); }}
          >
            <div className="form-field"><label>Name</label><input value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} required /></div>
            <div className="form-field"><label>Code (leave empty for automatic)</label><input value={form.code} onChange={(e) => setForm({ ...form, code: e.target.value })} placeholder="Automatic" /></div>
            <div className="form-field"><label>Email (optional)</label><input value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} /></div>
            <button className="btn" type="submit">Add marketer</button>
          </form>

          <div className="card" style={{ marginTop: 16, overflowX: "auto" }}>
            <table>
              <thead>
                <tr><th>Marketer</th><th>Code</th><th>Referred</th><th>Paying</th><th>Waiting</th><th>Payable now</th><th>Paid out</th><th></th></tr>
              </thead>
              <tbody>
                {marketers.length === 0 && <tr><td colSpan={8} className="text-muted">No marketers yet.</td></tr>}
                {marketers.map((m) => (
                  <tr key={m.id} style={{ opacity: m.isActive ? 1 : 0.55 }}>
                    <td><strong>{m.name}</strong>{m.email && <div className="text-muted" style={{ fontSize: 12 }}>{m.email}</div>}</td>
                    <td><code>{m.code}</code><div className="text-muted" style={{ fontSize: 11 }}>/start-trial?ref={m.code}</div></td>
                    <td>{m.referrals}</td>
                    <td>{m.payingCustomers}</td>
                    <td>{m.totals.length ? m.totals.map((t) => <div key={t.currency}>{money(t.waiting, t.currency)}</div>) : "—"}</td>
                    <td>{m.totals.length ? m.totals.map((t) => <div key={t.currency}><strong>{money(t.payable, t.currency)}</strong></div>) : "—"}</td>
                    <td>{m.totals.length ? m.totals.map((t) => <div key={t.currency}>{money(t.paidOut, t.currency)}</div>) : "—"}</td>
                    <td style={{ whiteSpace: "nowrap" }}>
                      <button className="btn btn-secondary btn-sm" onClick={() => (open === m.id ? setOpen(null) : show(m.id))}>Details</button>{" "}
                      <button
                        className="btn btn-sm"
                        disabled={!m.totals.some((t) => t.payable > 0)}
                        onClick={() => { const note = window.prompt("Payment note (optional), e.g. bank transfer ref:") ?? undefined; run(() => MarketersApi.payOut(systemKey, m.id, note), "Payable commissions marked as paid out."); }}
                      >Mark paid out</button>{" "}
                      <button className="btn btn-secondary btn-sm" onClick={() => run(() => MarketersApi.setActive(systemKey, m.id, !m.isActive))}>{m.isActive ? "Deactivate" : "Activate"}</button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {open && (
            <div className="card" style={{ marginTop: 16, overflowX: "auto" }}>
              <h3 style={{ marginTop: 0 }}>Commissions</h3>
              <table>
                <thead><tr><th>Company</th><th>Paid on</th><th>Payment</th><th>Rate</th><th>Commission</th><th>Payable from</th><th>Status</th><th></th></tr></thead>
                <tbody>
                  {rows.length === 0 && <tr><td colSpan={8} className="text-muted">No payments from referred customers yet.</td></tr>}
                  {rows.map((c) => (
                    <tr key={c.id}>
                      <td>{c.companyName}<div className="text-muted" style={{ fontSize: 12 }}>{c.companyCode}</div></td>
                      <td>{new Date(c.paymentPaidAtUtc).toLocaleDateString()}</td>
                      <td>{money(c.paymentAmount, c.currency)}</td>
                      <td>{Math.round(c.rate * 100)}%</td>
                      <td><strong>{money(c.amount, c.currency)}</strong></td>
                      <td>{new Date(c.eligibleAtUtc).toLocaleDateString()}</td>
                      <td>{c.status === 0 ? (new Date(c.eligibleAtUtc).getTime() <= now ? "Payable" : "Waiting (hold)") : STATUS[c.status]}{c.note && <div className="text-muted" style={{ fontSize: 12 }}>{c.note}</div>}</td>
                      <td>{c.status === 0 && <button className="btn btn-secondary btn-sm" onClick={() => { const r = window.prompt("Reason (e.g. customer refunded):"); if (r !== null) run(() => MarketersApi.voidCommission(systemKey, c.id, r), "Commission voided."); }}>Void</button>}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </>
      )}
    </div>
  );
}
