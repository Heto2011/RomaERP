import { useState } from "react";
import { SystemApi } from "../../api/services";
import { Country, ProductScope, type ProvisionTenantRequest, type Tenant, type TransferUserResult } from "../../api/types";
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
  const [productScope, setProductScope] = useState<ProductScope>(ProductScope.Full);

  const [transferSourceCode, setTransferSourceCode] = useState("");
  const [transferTargetCode, setTransferTargetCode] = useState("");
  const [transferEmail, setTransferEmail] = useState("");
  const [transferPassword, setTransferPassword] = useState(randomPassword());
  const [transferDeactivateSource, setTransferDeactivateSource] = useState(true);
  const [transferring, setTransferring] = useState(false);
  const [transferResult, setTransferResult] = useState<TransferUserResult | null>(null);

  const [resetCompanyCode, setResetCompanyCode] = useState("");
  const [resetEmail, setResetEmail] = useState("");
  const [resetPassword, setResetPassword] = useState(randomPassword());
  const [resetting, setResetting] = useState(false);
  const [resetDone, setResetDone] = useState<{ companyCode: string; email: string; password: string } | null>(null);

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
        productScope,
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

  async function handleTransferUser(e: React.FormEvent) {
    e.preventDefault();
    if (!systemKey) {
      setError("Enter the system key first.");
      return;
    }
    setError(null);
    setTransferring(true);
    setTransferResult(null);
    try {
      const res = await SystemApi.transferUser(systemKey, {
        sourceCompanyCode: transferSourceCode.trim().toLowerCase(),
        targetCompanyCode: transferTargetCode.trim().toLowerCase(),
        email: transferEmail,
        newPassword: transferPassword,
        deactivateInSource: transferDeactivateSource,
      });
      setTransferResult(res.data);
      setTransferEmail("");
      setTransferPassword(randomPassword());
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setTransferring(false);
    }
  }

  async function handleResetPassword(e: React.FormEvent) {
    e.preventDefault();
    if (!systemKey) {
      setError("Enter the system key first.");
      return;
    }
    setError(null);
    setResetting(true);
    setResetDone(null);
    try {
      await SystemApi.resetUserPassword(systemKey, {
        companyCode: resetCompanyCode.trim().toLowerCase(),
        email: resetEmail,
        newPassword: resetPassword,
      });
      setResetDone({ companyCode: resetCompanyCode.trim().toLowerCase(), email: resetEmail, password: resetPassword });
      setResetEmail("");
      setResetPassword(randomPassword());
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setResetting(false);
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
            <div className="form-field">
              <label>Product</label>
              <select value={productScope} onChange={(e) => setProductScope(Number(e.target.value) as ProductScope)}>
                <option value={ProductScope.Full}>Full RomaERP</option>
                <option value={ProductScope.PeopleOnly}>ROMA People only</option>
              </select>
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
        <h3>Transfer User Between Companies</h3>
        <p className="text-muted" style={{ marginTop: 0 }}>
          Recreates the user's account (email, name, roles) in the target company with a new password, and deactivates it in the source. History
          tied to the old company (attendance, leave, payroll) stays there — only the account moves.
        </p>
        {transferResult && (
          <div className="card" style={{ marginBottom: 14, borderInlineStart: "4px solid var(--color-success)" }}>
            <strong>Moved — hand these to the user:</strong>
            <table style={{ marginTop: 10 }}>
              <tbody>
                <tr><td>New Company Code</td><td>{transferResult.targetCompanyCode}</td></tr>
                <tr><td>Email</td><td>{transferResult.email}</td></tr>
                <tr><td>New Password</td><td>{transferPassword}</td></tr>
                <tr><td>Roles</td><td>{transferResult.roles.join(", ") || "—"}</td></tr>
              </tbody>
            </table>
          </div>
        )}
        <form onSubmit={handleTransferUser}>
          <div className="form-grid">
            <div className="form-field">
              <label>Source Company Code</label>
              <input value={transferSourceCode} onChange={(e) => setTransferSourceCode(e.target.value)} placeholder="the-salad-bar" required />
            </div>
            <div className="form-field">
              <label>Target Company Code</label>
              <input value={transferTargetCode} onChange={(e) => setTransferTargetCode(e.target.value)} placeholder="acme-restaurant" required />
            </div>
            <div className="form-field">
              <label>User Email</label>
              <input type="email" value={transferEmail} onChange={(e) => setTransferEmail(e.target.value)} required />
            </div>
            <div className="form-field">
              <label>New Password (for target company)</label>
              <input value={transferPassword} onChange={(e) => setTransferPassword(e.target.value)} required />
            </div>
            <div className="form-field" style={{ justifyContent: "flex-end" }}>
              <label style={{ display: "flex", alignItems: "center", gap: 6, fontWeight: "normal" }}>
                <input type="checkbox" checked={transferDeactivateSource} onChange={(e) => setTransferDeactivateSource(e.target.checked)} />
                Deactivate in source company
              </label>
            </div>
          </div>
          <button className="btn" type="submit" disabled={transferring} style={{ marginTop: 14 }}>
            {transferring ? "Moving…" : "Transfer User"}
          </button>
        </form>
      </div>

      <div className="card" style={{ marginTop: 16 }}>
        <h3>Reset a User's Password</h3>
        <p className="text-muted" style={{ marginTop: 0 }}>
          Last resort when nobody can log in to a company (e.g. its only Admin is locked out) — sets that
          user's password directly, bypassing the normal in-app "Admin resets a user's password" flow.
        </p>
        {resetDone && (
          <div className="card" style={{ marginBottom: 14, borderInlineStart: "4px solid var(--color-success)" }}>
            <strong>Password reset — hand these to the user:</strong>
            <table style={{ marginTop: 10 }}>
              <tbody>
                <tr><td>Company Code</td><td>{resetDone.companyCode}</td></tr>
                <tr><td>Email</td><td>{resetDone.email}</td></tr>
                <tr><td>New Password</td><td>{resetDone.password}</td></tr>
              </tbody>
            </table>
          </div>
        )}
        <form onSubmit={handleResetPassword}>
          <div className="form-grid">
            <div className="form-field">
              <label>Company Code</label>
              <input value={resetCompanyCode} onChange={(e) => setResetCompanyCode(e.target.value)} placeholder="the-salad-bar" required />
            </div>
            <div className="form-field">
              <label>User Email</label>
              <input type="email" value={resetEmail} onChange={(e) => setResetEmail(e.target.value)} required />
            </div>
            <div className="form-field">
              <label>New Password</label>
              <input value={resetPassword} onChange={(e) => setResetPassword(e.target.value)} required />
            </div>
          </div>
          <button className="btn" type="submit" disabled={resetting} style={{ marginTop: 14 }}>
            {resetting ? "Resetting…" : "Reset Password"}
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
                <th>Product</th>
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
                  <td>{t.productScope === ProductScope.PeopleOnly ? "ROMA People" : "Full"}</td>
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
