import { useState } from "react";
import { AiUsageApi } from "../../api/services";
import type { AiUsageOverview, TenantAiUsage } from "../../api/types";
import { getErrorMessage } from "../../api/client";

const levelLabel: Record<TenantAiUsage["level"], string> = {
  ok: "Normal",
  warning: "Near limit",
  over: "Limit reached",
  unlimited: "No monthly cap",
};
const levelClass: Record<TenantAiUsage["level"], string> = {
  ok: "badge badge-posted",
  warning: "badge badge-draft",
  over: "badge badge-reversed",
  unlimited: "badge",
};

export default function AiUsagePage() {
  const [systemKey, setSystemKey] = useState("");
  const [data, setData] = useState<AiUsageOverview | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  async function load() {
    if (!systemKey) {
      setError("Enter the system key first.");
      return;
    }
    setError(null);
    setLoading(true);
    try {
      setData((await AiUsageApi.getOverview(systemKey)).data);
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setLoading(false);
    }
  }

  return (
    <div style={{ maxWidth: 1000, margin: "40px auto", padding: "0 20px" }}>
      <h1>AI Usage</h1>
      <p className="text-muted">
        Calls to the AI (Claude) by every active company this month, against the monthly cap of its plan. The cost is an
        estimate (about one US cent per call) — the real bill is on the Anthropic console.
      </p>

      <div className="card">
        <div className="form-field">
          <label>System Key</label>
          <input type="password" value={systemKey} onChange={(e) => setSystemKey(e.target.value)} placeholder="X-System-Key" />
        </div>
        <button className="btn btn-secondary btn-sm" onClick={load} disabled={loading} style={{ marginTop: 8 }}>
          {loading ? "Loading…" : "Load"}
        </button>
      </div>

      {error && <div className="alert-error" style={{ marginTop: 16 }}>{error}</div>}

      {data && (
        <>
          <div className="card" style={{ marginTop: 16, display: "flex", flexWrap: "wrap", gap: 32 }}>
            <div><strong style={{ fontSize: 22 }}>{data.monthTotalCalls}</strong><div className="text-muted">Calls this month</div></div>
            <div><strong style={{ fontSize: 22 }}>${data.monthEstimatedCostUsd.toFixed(2)}</strong><div className="text-muted">Estimated cost this month</div></div>
            <div><strong style={{ fontSize: 22 }}>{data.todayTotalCalls}</strong><div className="text-muted">Calls today</div></div>
            <div>
              <strong style={{ fontSize: 22 }} className={data.nearOrOverLimit > 0 ? "text-danger" : ""}>{data.nearOrOverLimit}</strong>
              <div className="text-muted">Near or over the limit</div>
            </div>
          </div>

          <div className="card" style={{ marginTop: 16, overflowX: "auto" }}>
            <table>
              <thead>
                <tr>
                  <th>Company</th>
                  <th>Plan</th>
                  <th>This month</th>
                  <th>Cap</th>
                  <th>Used</th>
                  <th>Today (Q&amp;A / capture)</th>
                  <th>Est. cost</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {data.tenants.map((t) => (
                  <tr key={t.companyCode}>
                    <td>
                      {t.companyName}
                      <div className="text-muted">{t.companyCode}{t.isDemo ? " · trial" : ""}</div>
                    </td>
                    <td>{t.planCode ?? "—"}</td>
                    <td>{t.error ? "—" : t.monthTotal}</td>
                    <td>{t.monthlyLimit ?? "—"}</td>
                    <td>{t.percentOfLimit == null ? "—" : `${t.percentOfLimit}%`}</td>
                    <td>{t.error ? "—" : `${t.todayBusinessQa} / ${t.todayExpenseCapture}`}</td>
                    <td>{t.error ? "—" : `$${t.estimatedCostUsd.toFixed(2)}`}</td>
                    <td>
                      {t.error ? <span className="text-danger">{t.error}</span> : <span className={levelClass[t.level]}>{levelLabel[t.level]}</span>}
                    </td>
                  </tr>
                ))}
                {data.tenants.length === 0 && <tr><td colSpan={8} className="text-muted">No active companies.</td></tr>}
              </tbody>
            </table>
          </div>
        </>
      )}
    </div>
  );
}
