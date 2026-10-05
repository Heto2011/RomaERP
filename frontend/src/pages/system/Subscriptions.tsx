import { useState } from "react";
import { SubscriptionsApi } from "../../api/services";
import { SubscriptionStatus, SubscriptionInvoiceStatus, type TenantActivity, type SubscriptionPlan, type TenantSubscription, type SubscriptionInvoice, type BillingRunResult } from "../../api/types";
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
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [total, setTotal] = useState(0);
  const [activityFor, setActivityFor] = useState<TenantSubscription | null>(null);
  const [activity, setActivity] = useState<TenantActivity[] | null>(null);
  const PAGE_SIZE = 25;

  async function openActivity(s: TenantSubscription) {
    if (!systemKey) return;
    setActivityFor(s);
    setActivity(null);
    try {
      const res = await SubscriptionsApi.getTenantActivity(systemKey, s.tenantId);
      setActivity(res.data);
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  async function loadAll(nextSearch = search, nextPage = page) {
    if (!systemKey) {
      setError("Enter the system key first.");
      return;
    }
    setError(null);
    try {
      const [plansRes, subsRes, invRes] = await Promise.all([
        SubscriptionsApi.getPlans(systemKey),
        SubscriptionsApi.getTenantSubscriptions(systemKey, nextSearch, nextPage, PAGE_SIZE),
        SubscriptionsApi.getInvoices(systemKey),
      ]);
      setPlans(plansRes.data);
      setSubscriptions(subsRes.data);
      setTotal(Number(subsRes.headers["x-total-count"] ?? subsRes.data.length));
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

  async function handleChangeCode(s: TenantSubscription) {
    if (!systemKey) return;
    const code = prompt(`New login code for "${s.companyNameEn}" (current: ${s.companyCode}).\nLowercase English letters, numbers and dashes, 3–50 characters.\nEveryone signed in will have to log in again with the new code:`);
    if (code === null || !code.trim()) return;
    setError(null);
    try {
      await SubscriptionsApi.changeCompanyCode(systemKey, s.tenantId, code);
      await loadAll();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  async function handleDeleteForever(s: TenantSubscription) {
    if (!systemKey) return;
    const typed = prompt(`DELETE FOREVER: "${s.companyNameEn}" — its database, users, subscription, invoices and activity will be erased and CANNOT be recovered.\n\nType the company code (${s.companyCode}) to confirm:`);
    if (typed === null) return;
    setError(null);
    try {
      await SubscriptionsApi.deleteForever(systemKey, s.tenantId, typed);
      if (activityFor?.tenantId === s.tenantId) { setActivityFor(null); setActivity(null); }
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

  async function handleActivatePaid(s: TenantSubscription) {
    if (!systemKey) return;
    const reference = prompt(`Confirm ${s.companyNameEn}'s first payment. This makes the account permanent (the trial no longer expires) on the plan shown, and records this month's invoice as paid.\n\nPayment reference (bank transfer note, receipt #, etc.) — optional:`);
    if (reference === null) return;
    // Enterprise is priced by agreement: no annual option, the server refuses it too.
    const annualAllowed = s.planCode !== "enterprise";
    const annual = annualAllowed && confirm("Is this an ANNUAL payment (12 months for the price of 10)?\n\nOK = Annual (pays 10 months, covers 12)\nCancel = Monthly");
    setError(null);
    try {
      await SubscriptionsApi.activatePaid(systemKey, s.tenantId, s.planId, reference || null, annual);
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
    if (!confirm("Run the billing cycle now? This generates invoices for every active subscription due today (it also runs by itself every few hours). It never invoices trials and only suspends overdue tenants if that is switched on.")) return;
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
          <button className="btn" onClick={() => { setPage(1); void loadAll(search, 1); }}>Load</button>
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
                  <td>{p.isCustomPricing ? "Custom" : `${p.monthlyBasePrice.toLocaleString()} SAR (EGP / GBP lists differ)`}</td>
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
          <h3>Tenants <span className="text-muted" style={{ fontSize: 14 }}>({total})</span></h3>
          <div style={{ display: "flex", gap: 8, marginBottom: 12 }}>
            <input
              style={{ maxWidth: 320 }}
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              onKeyDown={(e) => { if (e.key === "Enter") { setPage(1); void loadAll(search, 1); } }}
              placeholder="Search by company name or code"
            />
            <button className="btn btn-secondary btn-sm" onClick={() => { setPage(1); void loadAll(search, 1); }}>Search</button>
          </div>
          {subscriptions.length === 0 && <div className="text-muted">No tenants found.</div>}
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
                    <td className={s.outstandingAmount > 0 ? "text-danger" : undefined}>
                      {s.outstandingAmount.toLocaleString()} {s.currency}
                      {s.overdueDays > 0 && <div><b>Overdue {s.overdueDays} day(s)</b></div>}
                    </td>
                    <td style={{ display: "flex", gap: 6, flexWrap: "wrap" }}>
                      {s.status === SubscriptionStatus.Trialing && (
                        <button className="btn btn-sm" onClick={() => handleActivatePaid(s)}>Confirm first payment</button>
                      )}
                      <button className="btn btn-secondary btn-sm" onClick={() => openActivity(s)}>Activity</button>
                      {s.tenantIsActive ? (
                        <button className="btn btn-secondary btn-sm" onClick={() => handleSuspend(s.tenantId)}>Suspend</button>
                      ) : (
                        <button className="btn btn-secondary btn-sm" onClick={() => handleReactivate(s.tenantId)}>Reactivate</button>
                      )}
                      <button className="btn btn-secondary btn-sm" onClick={() => handleChangeCode(s)}>Change code</button>
                      <button className="btn btn-sm" style={{ background: "#b42318", color: "#fff" }} onClick={() => handleDeleteForever(s)}>Delete forever</button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
          {total > PAGE_SIZE && (
            <div style={{ display: "flex", gap: 8, alignItems: "center", marginTop: 12 }}>
              <button className="btn btn-secondary btn-sm" disabled={page <= 1} onClick={() => { setPage(page - 1); void loadAll(search, page - 1); }}>Previous</button>
              <span className="text-muted">Page {page} of {Math.max(1, Math.ceil(total / PAGE_SIZE))}</span>
              <button className="btn btn-secondary btn-sm" disabled={page * PAGE_SIZE >= total} onClick={() => { setPage(page + 1); void loadAll(search, page + 1); }}>Next</button>
            </div>
          )}
        </div>
      )}

      {activityFor && (
        <div className="card" style={{ marginTop: 16 }}>
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
            <h3 style={{ margin: 0 }}>Activity — {activityFor.companyNameEn} <span className="text-muted">({activityFor.companyCode})</span></h3>
            <button className="btn btn-secondary btn-sm" onClick={() => { setActivityFor(null); setActivity(null); }}>Close</button>
          </div>
          {activity === null && <div className="text-muted" style={{ marginTop: 10 }}>Loading…</div>}
          {activity !== null && activity.length === 0 && <div className="text-muted" style={{ marginTop: 10 }}>Nothing recorded yet for this company.</div>}
          {activity !== null && activity.length > 0 && (
            <table style={{ marginTop: 10 }}>
              <thead><tr><th>When</th><th>Type</th><th>What happened</th><th>Details</th><th>By</th></tr></thead>
              <tbody>
                {activity.map((a) => (
                  <tr key={a.id}>
                    <td>{new Date(a.occurredAtUtc).toLocaleString()}</td>
                    <td>{a.category}</td>
                    <td><b>{a.action}</b></td>
                    <td>{a.details ?? "—"}</td>
                    <td>{a.actor ?? "—"}</td>
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
