import { useState } from "react";
import { SubscriptionsApi } from "../../api/services";
import { SubscriptionStatus, SubscriptionInvoiceStatus, type SubscriptionPlan, type TenantSubscription, type SubscriptionInvoice, type BillingRunResult } from "../../api/types";
import { getErrorMessage } from "../../api/client";

const statusLabel: Record<SubscriptionStatus, string> = {
  [SubscriptionStatus.Trialing]: "Trial",
  [SubscriptionStatus.Active]: "Active",
  [SubscriptionStatus.PastDue]: "Past Due",
  [SubscriptionStatus.Suspended]: "Suspended",
  [SubscriptionStatus.Cancelled]: "Cancelled",
};

const statusClass: Record<SubscriptionStatus, string> = {
  [SubscriptionStatus.Trialing]: "text-muted",
  [SubscriptionStatus.Active]: "text-success",
  [SubscriptionStatus.PastDue]: "text-danger",
  [SubscriptionStatus.Suspended]: "text-danger",
  [SubscriptionStatus.Cancelled]: "text-muted",
};

const invoiceStatusLabel: Record<SubscriptionInvoiceStatus, string> = {
  [SubscriptionInvoiceStatus.Pending]: "Pending",
  [SubscriptionInvoiceStatus.Paid]: "Paid",
  [SubscriptionInvoiceStatus.Failed]: "Failed",
  [SubscriptionInvoiceStatus.Cancelled]: "Cancelled",
};

export default function SubscriptionsPage() {
  const [systemKey, setSystemKey] = useState("");
  const [plans, setPlans] = useState<SubscriptionPlan[] | null>(null);
  const [subscriptions, setSubscriptions] = useState<TenantSubscription[] | null>(null);
  const [invoices, setInvoices] = useState<SubscriptionInvoice[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [runResult, setRunResult] = useState<BillingRunResult | null>(null);
  const [busy, setBusy] = useState(false);

  async function loadAll() {
    if (!systemKey) {
      setError("Enter the system key first.");
      return;
    }
    setError(null);
    try {
      const [plansRes, subsRes, invRes] = await Promise.all([
        SubscriptionsApi.getPlans(systemKey),
        SubscriptionsApi.getTenantSubscriptions(systemKey),
        SubscriptionsApi.getInvoices(systemKey),
      ]);
      setPlans(plansRes.data);
      setSubscriptions(subsRes.data);
      setInvoices(invRes.data);
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  async function handleSetPlan(tenantId: string, planId: string) {
    if (!systemKey) return;
    setError(null);
    try {
      await SubscriptionsApi.setPlan(systemKey, tenantId, planId);
      await loadAll();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  async function handleSuspend(tenantId: string) {
    if (!systemKey) return;
    if (!confirm("Suspend this tenant now? They will be locked out of the app until reactivated.")) return;
    setError(null);
    try {
      await SubscriptionsApi.suspend(systemKey, tenantId);
      await loadAll();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  async function handleReactivate(tenantId: string) {
    if (!systemKey) return;
    setError(null);
    try {
      await SubscriptionsApi.reactivate(systemKey, tenantId);
      await loadAll();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  async function handleMarkPaid(invoiceId: string) {
    if (!systemKey) return;
    const reference = prompt("Payment reference (bank transfer note, receipt #, etc.) — optional:") ?? undefined;
    setError(null);
    try {
      await SubscriptionsApi.markInvoicePaid(systemKey, invoiceId, reference || null);
      await loadAll();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  async function handleRunBillingCycle() {
    if (!systemKey) return;
    if (!confirm("Run the billing cycle now? This generates invoices for every subscription due today and suspends tenants past the grace period.")) return;
    setError(null);
    setBusy(true);
    setRunResult(null);
    try {
      const res = await SubscriptionsApi.runBillingCycle(systemKey);
      setRunResult(res.data);
      await loadAll();
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setBusy(false);
    }
  }

  return (
    <div style={{ maxWidth: 1100, margin: "40px auto", padding: "0 20px" }}>
      <h1>Subscriptions &amp; Billing</h1>
      <p className="text-muted">Internal use only — every tenant's plan, billing period, and invoices, in one place.</p>

      <div className="card">
        <div className="form-field">
          <label>System Key</label>
          <input type="password" value={systemKey} onChange={(e) => setSystemKey(e.target.value)} placeholder="X-System-Key" />
        </div>
        <div style={{ display: "flex", gap: 8, marginTop: 10 }}>
          <button className="btn" onClick={loadAll}>Load</button>
          <button className="btn btn-secondary" onClick={handleRunBillingCycle} disabled={busy}>
            {busy ? "Running…" : "Run Billing Cycle Now"}
          </button>
        </div>
      </div>

      {error && <div className="alert-error" style={{ marginTop: 16 }}>{error}</div>}

      {runResult && (
        <div className="card" style={{ marginTop: 16, borderInlineStart: "4px solid var(--color-success)" }}>
          <strong>Billing run finished:</strong> {runResult.invoicesGenerated} invoice(s) generated, {runResult.autoCharged} auto-charged, {runResult.suspended} tenant(s) suspended.
          {runResult.notes.length > 0 && (
            <ul style={{ marginTop: 8 }}>
              {runResult.notes.map((n, i) => <li key={i}>{n}</li>)}
            </ul>
          )}
        </div>
      )}

      {plans && (
        <div className="card" style={{ marginTop: 16 }}>
          <h3>Plans</h3>
          <table>
            <thead>
              <tr><th>Code</th><th>Name</th><th>Monthly Base</th><th>Included Branches</th><th>Included Users</th></tr>
            </thead>
            <tbody>
              {plans.map((p) => (
                <tr key={p.id}>
                  <td>{p.code}</td>
                  <td>{p.nameEn}</td>
                  <td>{p.isCustomPricing ? "Custom" : `${p.monthlyBasePrice.toLocaleString()} SAR`}</td>
                  <td>{p.isCustomPricing ? "—" : p.includedBranches}</td>
                  <td>{p.isCustomPricing ? "—" : p.includedUsers}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {subscriptions && (
        <div className="card" style={{ marginTop: 16 }}>
          <h3>Tenants</h3>
          {subscriptions.length === 0 && <div className="text-muted">No tenants yet.</div>}
          {subscriptions.length > 0 && (
            <table>
              <thead>
                <tr>
                  <th>Company</th><th>Plan</th><th>Status</th><th>Usage</th><th>Period Ends</th><th>Outstanding</th><th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {subscriptions.map((s) => (
                  <tr key={s.tenantId}>
                    <td>{s.companyNameEn} <span className="text-muted">({s.companyCode})</span></td>
                    <td>
                      <select value={s.planId} onChange={(e) => handleSetPlan(s.tenantId, e.target.value)}>
                        {(plans ?? []).map((p) => <option key={p.id} value={p.id}>{p.nameEn}</option>)}
                      </select>
                    </td>
                    <td className={statusClass[s.status]}>{statusLabel[s.status]}</td>
                    <td>{s.currentBranches} branches / {s.currentUsers} users</td>
                    <td>{new Date(s.currentPeriodEnd).toLocaleDateString()}</td>
                    <td className={s.outstandingAmount > 0 ? "text-danger" : undefined}>{s.outstandingAmount.toLocaleString()} SAR</td>
                    <td>
                      {s.tenantIsActive ? (
                        <button className="btn btn-secondary btn-sm" onClick={() => handleSuspend(s.tenantId)}>Suspend</button>
                      ) : (
                        <button className="btn btn-secondary btn-sm" onClick={() => handleReactivate(s.tenantId)}>Reactivate</button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      )}

      {invoices && (
        <div className="card" style={{ marginTop: 16 }}>
          <h3>Invoices</h3>
          {invoices.length === 0 && <div className="text-muted">No invoices yet.</div>}
          {invoices.length > 0 && (
            <table>
              <thead>
                <tr>
                  <th>Company</th><th>Period</th><th>Base</th><th>Overage</th><th>Discount</th><th>Total</th><th>Status</th><th>Due</th><th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {invoices.map((inv) => (
                  <tr key={inv.id}>
                    <td>{inv.companyNameAr}</td>
                    <td>{new Date(inv.periodStart).toLocaleDateString()} – {new Date(inv.periodEnd).toLocaleDateString()}</td>
                    <td>{inv.baseAmount.toLocaleString()}</td>
                    <td>{(inv.extraBranchesAmount + inv.extraUsersAmount).toLocaleString()}</td>
                    <td>{inv.multiCompanyDiscountAmount > 0 ? `-${inv.multiCompanyDiscountAmount.toLocaleString()}` : "—"}</td>
                    <td><b>{inv.totalAmount.toLocaleString()} {inv.currency}</b></td>
                    <td className={inv.status === SubscriptionInvoiceStatus.Paid ? "text-success" : inv.status === SubscriptionInvoiceStatus.Failed ? "text-danger" : undefined}>
                      {invoiceStatusLabel[inv.status]}
                    </td>
                    <td>{new Date(inv.dueDateUtc).toLocaleDateString()}</td>
                    <td>
                      {inv.status !== SubscriptionInvoiceStatus.Paid && (
                        <button className="btn btn-secondary btn-sm" onClick={() => handleMarkPaid(inv.id)}>Mark Paid</button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      )}
    </div>
  );
}
