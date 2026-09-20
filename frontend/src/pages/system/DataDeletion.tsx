import { useState } from "react";
import { SystemApi } from "../../api/services";
import { type Tenant, type DataDeletionRecord } from "../../api/types";
import { getErrorMessage } from "../../api/client";

export default function DataDeletionPage() {
  const [systemKey, setSystemKey] = useState("");
  const [tenants, setTenants] = useState<Tenant[] | null>(null);
  const [records, setRecords] = useState<DataDeletionRecord[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  const [activeTenant, setActiveTenant] = useState<Tenant | null>(null);
  const [confirmCode, setConfirmCode] = useState("");
  const [requestedByEmail, setRequestedByEmail] = useState("");
  const [processedByEmail, setProcessedByEmail] = useState("");
  const [reason, setReason] = useState("");
  const [processing, setProcessing] = useState(false);
  const [lastResult, setLastResult] = useState<DataDeletionRecord | null>(null);

  async function loadTenants() {
    if (!systemKey) {
      setError("Enter the system key first.");
      return;
    }
    setError(null);
    try {
      const res = await SystemApi.getTenants(systemKey, false);
      setTenants(res.data);
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  async function loadRecords() {
    if (!systemKey) {
      setError("Enter the system key first.");
      return;
    }
    setError(null);
    try {
      const res = await SystemApi.getDataDeletionRecords(systemKey);
      setRecords(res.data);
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  function startDeletion(tenant: Tenant) {
    setActiveTenant(tenant);
    setConfirmCode("");
    setRequestedByEmail("");
    setProcessedByEmail("");
    setReason("");
    setLastResult(null);
    setError(null);
  }

  async function handleConfirmDeletion(e: React.FormEvent) {
    e.preventDefault();
    if (!activeTenant) return;
    if (confirmCode.trim().toLowerCase() !== activeTenant.companyCode) {
      setError("Company code doesn't match — type it exactly to confirm.");
      return;
    }
    setError(null);
    setProcessing(true);
    try {
      const res = await SystemApi.deleteTenantData(systemKey, activeTenant.id, {
        requestedByEmail,
        reason: reason || null,
        processedByEmail,
      });
      setLastResult(res.data);
      setActiveTenant(null);
      await loadTenants();
      await loadRecords();
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setProcessing(false);
    }
  }

  return (
    <div style={{ maxWidth: 900, margin: "40px auto", padding: "0 20px" }}>
      <h1>Data Deletion Tool</h1>
      <p className="text-muted">
        Internal use only — honors an official customer request to permanently erase their data. This drops the
        tenant's entire database and cannot be undone. Every request leaves a permanent record below, regardless
        of outcome.
      </p>

      <div className="card">
        <div className="form-field">
          <label>System Key</label>
          <input type="password" value={systemKey} onChange={(e) => setSystemKey(e.target.value)} placeholder="X-System-Key" />
        </div>
      </div>

      {error && <div className="alert-error" style={{ marginTop: 16 }}>{error}</div>}

      {lastResult && (
        <div className="card" style={{ marginTop: 16, borderInlineStart: "4px solid var(--color-success)" }}>
          <strong>Data deleted — confirmation #{lastResult.confirmationNumber}</strong>
          <table style={{ marginTop: 10 }}>
            <tbody>
              <tr><td>Company</td><td>{lastResult.companyNameEn} ({lastResult.companyCode})</td></tr>
              <tr><td>Requested by</td><td>{lastResult.requestedByEmail}</td></tr>
              <tr><td>Processed by</td><td>{lastResult.processedByEmail}</td></tr>
              <tr><td>Completed</td><td>{lastResult.completedAtUtc ? new Date(lastResult.completedAtUtc).toLocaleString() : "—"}</td></tr>
            </tbody>
          </table>
        </div>
      )}

      {activeTenant && (
        <div className="card" style={{ marginTop: 16, borderInlineStart: "4px solid var(--color-danger)" }}>
          <h3 style={{ marginTop: 0 }}>
            Permanently delete all data for <span className="mono">{activeTenant.companyCode}</span> ({activeTenant.companyNameEn})?
          </h3>
          <p className="text-muted">This drops the tenant's database entirely. It cannot be recovered.</p>
          <form onSubmit={handleConfirmDeletion}>
            <div className="form-grid">
              <div className="form-field">
                <label>Type "{activeTenant.companyCode}" to confirm</label>
                <input value={confirmCode} onChange={(e) => setConfirmCode(e.target.value)} required />
              </div>
              <div className="form-field">
                <label>Requested by (customer's email)</label>
                <input type="email" value={requestedByEmail} onChange={(e) => setRequestedByEmail(e.target.value)} required />
              </div>
              <div className="form-field">
                <label>Processed by (your email)</label>
                <input type="email" value={processedByEmail} onChange={(e) => setProcessedByEmail(e.target.value)} required />
              </div>
              <div className="form-field">
                <label>Reason / notes (optional)</label>
                <input value={reason} onChange={(e) => setReason(e.target.value)} />
              </div>
            </div>
            <div style={{ display: "flex", gap: 8, marginTop: 14 }}>
              <button className="btn btn-danger" type="submit" disabled={processing}>
                {processing ? "Deleting…" : "Permanently Delete Data"}
              </button>
              <button className="btn btn-secondary" type="button" onClick={() => setActiveTenant(null)}>
                Cancel
              </button>
            </div>
          </form>
        </div>
      )}

      <div className="card" style={{ marginTop: 16 }}>
        <div className="page-header" style={{ marginBottom: 10 }}>
          <h3 style={{ margin: 0 }}>Tenants</h3>
          <button className="btn btn-secondary btn-sm" onClick={loadTenants}>Refresh</button>
        </div>
        {tenants === null && <div className="text-muted">Click Refresh to load.</div>}
        {tenants !== null && (
          <table>
            <thead>
              <tr>
                <th>Code</th>
                <th>Name</th>
                <th>Status</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {tenants.map((t) => (
                <tr key={t.id}>
                  <td>{t.companyCode}</td>
                  <td>{t.companyNameEn}</td>
                  <td>
                    {t.dataDeletedAtUtc ? (
                      <span className="text-danger">Data deleted {new Date(t.dataDeletedAtUtc).toLocaleDateString()}</span>
                    ) : t.isActive ? (
                      <span className="text-success">Active</span>
                    ) : (
                      <span className="text-danger">Suspended</span>
                    )}
                  </td>
                  <td>
                    {!t.dataDeletedAtUtc && (
                      <button className="btn btn-danger btn-sm" onClick={() => startDeletion(t)}>
                        Delete Data
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      <div className="card" style={{ marginTop: 16 }}>
        <div className="page-header" style={{ marginBottom: 10 }}>
          <h3 style={{ margin: 0 }}>Deletion Records (permanent proof log)</h3>
          <button className="btn btn-secondary btn-sm" onClick={loadRecords}>Refresh</button>
        </div>
        {records === null && <div className="text-muted">Click Refresh to load.</div>}
        {records !== null && records.length === 0 && <div className="text-muted">No deletion requests yet.</div>}
        {records !== null && records.length > 0 && (
          <table>
            <thead>
              <tr>
                <th>#</th>
                <th>Company</th>
                <th>Requested by</th>
                <th>Processed by</th>
                <th>Requested</th>
                <th>Completed</th>
                <th>Failure</th>
              </tr>
            </thead>
            <tbody>
              {records.map((r) => (
                <tr key={r.id}>
                  <td>{r.confirmationNumber}</td>
                  <td>{r.companyNameEn} ({r.companyCode})</td>
                  <td>{r.requestedByEmail}</td>
                  <td>{r.processedByEmail}</td>
                  <td>{new Date(r.requestedAtUtc).toLocaleString()}</td>
                  <td>{r.completedAtUtc ? new Date(r.completedAtUtc).toLocaleString() : "—"}</td>
                  <td className="text-danger">{r.failureReason ?? "—"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}
