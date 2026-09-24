import { useState } from "react";
import { MarketingApi } from "../../api/services";
import { type MarketingPageView, type MarketingPageViewStats } from "../../api/types";
import { getErrorMessage } from "../../api/client";

export default function MarketingViewsPage() {
  const [systemKey, setSystemKey] = useState("");
  const [stats, setStats] = useState<MarketingPageViewStats | null>(null);
  const [views, setViews] = useState<MarketingPageView[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function load() {
    if (!systemKey) {
      setError("Enter the system key first.");
      return;
    }
    setError(null);
    try {
      const [statsRes, viewsRes] = await Promise.all([
        MarketingApi.getStats(systemKey),
        MarketingApi.getPageViews(systemKey),
      ]);
      setStats(statsRes.data);
      setViews(viewsRes.data);
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  return (
    <div style={{ maxWidth: 900, margin: "40px auto", padding: "0 20px" }}>
      <h1>Marketing Page Views</h1>
      <p className="text-muted">
        Anonymous visit log for the public marketing pages (pricing.html etc.) — no cookies, no visitor identity,
        just volume, page, and where it came from (Referrer).
      </p>

      <div className="card">
        <div className="form-field">
          <label>System Key</label>
          <input type="password" value={systemKey} onChange={(e) => setSystemKey(e.target.value)} placeholder="X-System-Key" />
        </div>
        <button className="btn btn-secondary btn-sm" onClick={load} style={{ marginTop: 8 }}>Load</button>
      </div>

      {error && <div className="alert-error" style={{ marginTop: 16 }}>{error}</div>}

      {stats && (
        <div className="card" style={{ marginTop: 16 }}>
          <div style={{ display: "flex", gap: 24, marginBottom: 16 }}>
            <div><strong style={{ fontSize: 22 }}>{stats.totalViews}</strong><div className="text-muted">Total views</div></div>
            <div><strong style={{ fontSize: 22 }}>{stats.last7Days}</strong><div className="text-muted">Last 7 days</div></div>
            <div><strong style={{ fontSize: 22 }}>{stats.last30Days}</strong><div className="text-muted">Last 30 days</div></div>
          </div>
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 20 }}>
            <div>
              <h3 style={{ marginTop: 0 }}>Top pages</h3>
              <table>
                <tbody>
                  {stats.topPaths.map((p) => (
                    <tr key={p.label}><td>{p.label}</td><td>{p.count}</td></tr>
                  ))}
                </tbody>
              </table>
            </div>
            <div>
              <h3 style={{ marginTop: 0 }}>Top referrers</h3>
              <table>
                <tbody>
                  {stats.topReferrers.map((r) => (
                    <tr key={r.label}><td>{r.label}</td><td>{r.count}</td></tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      )}

      {views && (
        <div className="card" style={{ marginTop: 16 }}>
          <h3 style={{ marginTop: 0 }}>Recent views (last 500)</h3>
          <table>
            <thead>
              <tr>
                <th>When</th>
                <th>Page</th>
                <th>Referrer</th>
                <th>Device</th>
              </tr>
            </thead>
            <tbody>
              {views.length === 0 && (
                <tr><td colSpan={4} className="text-muted" style={{ textAlign: "center", padding: 20 }}>No views yet.</td></tr>
              )}
              {views.map((v) => (
                <tr key={v.id}>
                  <td>{new Date(v.viewedAtUtc).toLocaleString()}</td>
                  <td>{v.path}</td>
                  <td>{v.referrer || "(مباشر)"}</td>
                  <td>{v.userAgent && /Mobi|Android|iPhone/.test(v.userAgent) ? "Mobile" : "Desktop"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
