import { useState } from "react";
import { SystemApi } from "../../api/services";
import { Country, type ProvisionTenantRequest, type Tenant } from "../../api/types";
import { getErrorMessage } from "../../api/client";

const countryLabel: Record<Country, string> = {
  [Country.Egypt]: "Egypt",
  [Country.SaudiArabia]: "Saudi Arabia",
  [Country.UAE]: "UAE",
  [Country.Bahrain]: "Bahrain",
  [Country.Oman]: "Oman",
  [Country.Qatar]: "Qatar",
  [Country.Kuwait]: "Kuwait",
};

function randomPassword() {
  return `Demo-${Math.random().toString(36).slice(2, 8)}!${Math.floor(Math.random() * 90 + 10)}`;
}

export default function DemoTenantsPage() {
  const [systemKey, setSystemKey] = useState("");
  const [tenants, setTenants] = useState<Tenant[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [lastCreated, setLastCreated] = useState<{ tenant: Tenant; email: string; password: string } | null>(null);

  const [companyCode, setCompanyCode] = useState("");
  const [companyNameAr, setCompanyNameAr] = useState("");
  const [companyNameEn, setCompanyNameEn] = useState("");
  const [country, setCountry] = useState<Country>(Country.SaudiArabia);
  const [adminEmail, setAdminEmail] = useState("");
  const [adminPassword, setAdminPassword] = useState(randomPassword());
  const [expiryDays, setExpiryDays] = useState(14);
  const [seedDemoData, setSeedDemoData] = useState(true);

  async function loadTenants() {
    if (!systemKey) {
      setError("Enter the system key first.");
      return;
    }
    setError(null);
    try {
      const res = await SystemApi.getTenants(systemKey, true);
      setTenants(res.data);
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  async function handleCreate(e: React.FormEvent) {
    e.preventDefault();
    if (!systemKey) {
      setError("Enter the system key first.");
      return;
    }
    setError(null);
    setLoading(true);
    setLastCreated(null);
    try {
      const payload: ProvisionTenantRequest = {
        companyCode: companyCode.trim().toLowerCase(),
        companyNameAr,
        companyNameEn,
        country,
        adminEmail,
        adminPassword,
        isDemo: true,
        demoExpiryDays: expiryDays,
        seedDemoData,
      };
      const res = await SystemApi.createTenant(systemKey, payload);
      setLastCreated({ tenant: res.data, email: adminEmail, password: adminPassword });
      setCompanyCode("");
      setCompanyNameAr("");
      setCompanyNameEn("");
      setAdminEmail("");
      setAdminPassword(randomPassword());
      await loadTenants();
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setLoading(false);
    }
  }

  async function handleExpireNow() {
    if (!systemKey) {
      setError("Enter the system key first.");
      return;
    }
    setError(null);
    try {
      const res = await SystemApi.expireDemoTenants(systemKey);
      await loadTenants();
      alert(`Deactivated ${res.data.deactivatedCount} expired demo tenant(s).`);
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  return (
    <div style={{ maxWidth: 900, margin: "40px auto", padding: "0 20px" }}>
      <h1>Demo Company Tool</h1>
      <p className="text-muted">Internal use only — creates a fully isolated tenant with sample data for sales demos.</p>

      <div className="card">
        <div className="form-field">
          <label>System Key</label>
          <input type="password" value={systemKey} onChange={(e) => setSystemKey(e.target.value)} placeholder="X-System-Key" />
        </div>
      </div>

      {error && <div className="alert-error" style={{ marginTop: 16 }}>{error}</div>}

      {lastCreated && (
        <div className="card" style={{ marginTop: 16, borderInlineStart: "4px solid var(--color-success)" }}>
          <strong>Demo company created — hand these to the prospect:</strong>
          <table style={{ marginTop: 10 }}>
            <tbody>
              <tr><td>Company Code</td><td>{lastCreated.tenant.companyCode}</td></tr>
              <tr><td>Login Email</td><td>{lastCreated.email}</td></tr>
              <tr><td>Password</td><td>{lastCreated.password}</td></tr>
              <tr><td>Expires</td><td>{lastCreated.tenant.expiresAtUtc ? new Date(lastCreated.tenant.expiresAtUtc).toLocaleDateString() : "—"}</td></tr>
            </tbody>
          </table>
        </div>
      )}

      <div className="card" style={{ marginTop: 16 }}>
        <h3>Create Demo Company</h3>
        <form onSubmit={handleCreate}>
          <div className="form-grid">
            <div className="form-field">
              <label>Company Code (lowercase, dashes)</label>
              <input value={companyCode} onChange={(e) => setCompanyCode(e.target.value)} placeholder="acme-restaurant" required />
            </div>
            <div className="form-field">
              <label>Company Name (Arabic)</label>
              <input value={companyNameAr} onChange={(e) => setCompanyNameAr(e.target.value)} required />
            </div>
            <div className="form-field">
              <label>Company Name (English)</label>
              <input value={companyNameEn} onChange={(e) => setCompanyNameEn(e.target.value)} required />
            </div>
            <div className="form-field">
              <label>Country</label>
              <select value={country} onChange={(e) => setCountry(Number(e.target.value) as Country)}>
                {Object.entries(countryLabel).map(([value, label]) => (
                  <option key={value} value={value}>{label}</option>
                ))}
              </select>
            </div>
            <div className="form-field">
              <label>Admin Email</label>
              <input type="email" value={adminEmail} onChange={(e) => setAdminEmail(e.target.value)} required />
            </div>
            <div className="form-field">
              <label>Admin Password</label>
              <input value={adminPassword} onChange={(e) => setAdminPassword(e.target.value)} required />
            </div>
            <div className="form-field">
              <label>Expires After (days)</label>
              <input type="number" min={1} value={expiryDays} onChange={(e) => setExpiryDays(Number(e.target.value))} />
            </div>
            <div className="form-field" style={{ justifyContent: "flex-end" }}>
              <label style={{ display: "flex", alignItems: "center", gap: 6, fontWeight: "normal" }}>
                <input type="checkbox" checked={seedDemoData} onChange={(e) => setSeedDemoData(e.target.checked)} />
                Seed sample data (items, sale, purchase, restaurant order)
              </label>
            </div>
          </div>
          <button className="btn" type="submit" disabled={loading} style={{ marginTop: 14 }}>
            {loading ? "Creating…" : "Create Demo Company"}
          </button>
        </form>
      </div>

      <div className="card" style={{ marginTop: 16 }}>
        <div className="page-header" style={{ marginBottom: 10 }}>
          <h3 style={{ margin: 0 }}>Demo Tenants</h3>
          <div style={{ display: "flex", gap: 8 }}>
            <button className="btn btn-secondary btn-sm" onClick={loadTenants}>Refresh</button>
            <button className="btn btn-secondary btn-sm" onClick={handleExpireNow}>Deactivate Expired Now</button>
          </div>
        </div>
        {tenants === null && <div className="text-muted">Click Refresh to load.</div>}
        {tenants !== null && tenants.length === 0 && <div className="text-muted">No demo tenants yet.</div>}
        {tenants !== null && tenants.length > 0 && (
          <table>
            <thead>
              <tr>
                <th>Code</th>
                <th>Name</th>
                <th>Country</th>
                <th>Status</th>
                <th>Expires</th>
                <th>Created</th>
              </tr>
            </thead>
            <tbody>
              {tenants.map((t) => (
                <tr key={t.id}>
                  <td>{t.companyCode}</td>
                  <td>{t.companyNameEn}</td>
                  <td>{countryLabel[t.country]}</td>
                  <td className={t.isActive ? "text-success" : "text-danger"}>{t.isActive ? "Active" : "Deactivated"}</td>
                  <td>{t.expiresAtUtc ? new Date(t.expiresAtUtc).toLocaleDateString() : "—"}</td>
                  <td>{new Date(t.createdAtUtc).toLocaleDateString()}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}
