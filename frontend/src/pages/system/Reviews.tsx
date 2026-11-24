import { useState } from "react";
import SystemKeyInput, { useSystemKey } from "../../components/SystemKeyInput";
import { systemApiClient, getErrorMessage } from "../../api/client";

type Review = { id: string; companyCode: string; companyName?: string | null; userName: string; displayName: string; rating: number; comment?: string | null; product: string;
  allowPublish: boolean; showCompany: boolean; periodKey: string; status: number; createdAtUtc: string; adminNote?: string | null };
const STATUS = ["Pending", "Approved", "Rejected"];

export default function ReviewsPage() {
  const [systemKey, setSystemKey] = useSystemKey();
  const [filter, setFilter] = useState("");
  const [rows, setRows] = useState<Review[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const headers = { "X-System-Key": systemKey.trim() };

  async function load(status = filter) {
    if (!systemKey) { setError("Enter the system key first."); return; }
    setError(null);
    try { setRows((await systemApiClient.get<Review[]>("/system/reviews", { headers, params: status ? { status } : {} })).data); } catch (e) { setError(getErrorMessage(e)); }
  }
  async function decide(id: string, status: "Approved" | "Rejected") {
    const note = status === "Rejected" ? window.prompt("Reason (optional)") ?? undefined : undefined;
    try { await systemApiClient.post(`/system/reviews/${id}/decision`, { status, note }, { headers }); await load(); } catch (e) { setError(getErrorMessage(e)); }
  }

  return (
    <div style={{ maxWidth: 1000, margin: "40px auto", padding: "0 20px" }}>
      <h1>Customer reviews</h1>
      <p className="text-muted">What customers wrote when asked at the end of the month. A review appears on the website only when the customer agreed to publish it AND you approved it here. Nothing is edited or invented.</p>
      <div className="card" style={{ display: "flex", gap: 8, alignItems: "end", flexWrap: "wrap" }}>
        <div className="form-field"><label>System Key</label><SystemKeyInput value={systemKey} onChange={setSystemKey} /></div>
        <div className="form-field"><label>Show</label>
          <select value={filter} onChange={(e) => { setFilter(e.target.value); void load(e.target.value); }}>
            <option value="">All</option><option value="Pending">Waiting for a decision</option><option value="Approved">Approved</option><option value="Rejected">Rejected</option>
          </select></div>
        <button className="btn" onClick={() => load()}>Load</button>
      </div>
      {error && <div className="alert-error">{error}</div>}
      {rows && (rows.length === 0 ? <div className="card text-muted">No reviews yet.</div> : rows.map((r) => (
        <div key={r.id} className="card" style={{ marginTop: 12 }}>
          <div style={{ display: "flex", justifyContent: "space-between", gap: 8, flexWrap: "wrap" }}>
            <strong>{"★".repeat(r.rating)}{"☆".repeat(5 - r.rating)} <span className="text-muted">({r.rating}/5)</span></strong>
            <span className="badge">{STATUS[r.status]}</span>
          </div>
          <p style={{ margin: "8px 0" }}>{r.comment ?? <span className="text-muted">(no comment)</span>}</p>
          <div className="text-muted" style={{ fontSize: 13 }}>
            {r.userName} — {r.companyName ?? r.companyCode} ({r.companyCode}) · {r.product} · {r.periodKey} · will appear as “{r.displayName}”{r.showCompany ? " with the company name" : ""}
            · {r.allowPublish ? "agreed to publish" : "did NOT agree to publish"}
          </div>
          {r.status === 0 && (
            <div style={{ marginTop: 8, display: "flex", gap: 8 }}>
              <button className="btn btn-sm" disabled={!r.allowPublish} title={r.allowPublish ? "" : "The customer did not agree to publish"} onClick={() => decide(r.id, "Approved")}>Approve for the website</button>
              <button className="btn btn-secondary btn-sm" onClick={() => decide(r.id, "Rejected")}>Reject</button>
            </div>
          )}
          {r.adminNote && <div className="text-muted" style={{ fontSize: 12 }}>Note: {r.adminNote}</div>}
        </div>
      )))}
    </div>
  );
}
