import { useState } from "react";
import SystemKeyInput, { useSystemKey } from "../../components/SystemKeyInput";
import { SubscriptionsApi } from "../../api/services";
import { ProductScope, SubscriptionStatus, SubscriptionInvoiceStatus, type TenantActivity, type SubscriptionPlan, type TenantSubscription, type SubscriptionInvoice, type BillingRunResult } from "../../api/types";
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
  const [systemKey, setSystemKey] = useSystemKey();
  const [plans, setPlans] = useState<SubscriptionPlan[] | null>(null);
  const [subscriptions, setSubscriptions] = useState<TenantSubscription[] | null>(null);
  const [invoices, setInvoices] = useState<SubscriptionInvoice[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [runResult, setRunResult] = useState<BillingRunResult | null>(null);
  const [busy, setBusy] = useState(false);
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [total, setTotal] = useState(0);
  const [viewFilter, setViewFilter] = useState<"all" | "dormant">("all");
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

  async function handleToggleScope(s: TenantSubscription) {
    if (!systemKey) return;
    const toFull = s.productScope !== ProductScope.Full;
    if (!confirm(toFull
      ? `Give "${s.companyNameEn}" the FULL ERP + HR? Everyone signed in must log in again to see it.`
      : `Limit "${s.companyNameEn}" to Roma HR only? Everyone signed in must log in again.`)) return;
    setError(null);
    try {
      await SubscriptionsApi.changeProductScope(systemKey, s.tenantId, toFull ? ProductScope.Full : ProductScope.PeopleOnly);
      await loadAll();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  async function handleDiscount(s: TenantSubscription) {
    if (!systemKey) return;
    const current = (s.discountMonthsLeft ?? 0) > 0 ? ` (now ${s.discountPercent}% for ${s.discountMonthsLeft} more months)` : "";
    const pct = prompt(`Discount for "${s.companyNameEn}"${current}\n\nPercent off the plan price each month (0 removes the discount):`, String(s.discountPercent || 0));
    if (pct === null) return;
    const percent = Number(pct);
    if (!Number.isFinite(percent) || percent < 0 || percent > 100) { setError("The percent must be between 0 and 100."); return; }
    let months = 0;
    if (percent > 0) {
      const m = prompt("For how many months?", "3");
      if (m === null) return;
      months = Math.floor(Number(m));
      if (!Number.isFinite(months) || months < 1 || months > 36) { setError("The months must be between 1 and 36."); return; }
    }
    setError(null);
    try {
      await SubscriptionsApi.setDiscount(systemKey, s.tenantId, percent, months);
      await loadAll();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  async function handleFreeMonths(s: TenantSubscription) {
    if (!systemKey) return;
    const m = prompt(`Start "${s.companyNameEn}" on the ${s.planCode} plan with free months.\n\nHow many months are free? Nothing is invoiced until they end, then billing starts by itself:`, "3");
    if (m === null) return;
    const months = Math.floor(Number(m));
    if (!Number.isFinite(months) || months < 1 || months > 36) { setError("The months must be between 1 and 36."); return; }
    setError(null);
    try {
      await SubscriptionsApi.startWithFreeMonths(systemKey, s.tenantId, s.planId, months);
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

  // A company that never made anything (no login, or a login and nothing else). Only these are ever offered for a follow-up or a clean-up.
  const isDormant = (s: TenantSubscription) => s.activationStatus === "NeverStarted" || s.activationStatus === "LoggedInOnly";
  const hasNoContact = (s: TenantSubscription) => !s.contactEmail?.trim() && !s.contactPhone?.trim();
  // Safe to erase: dormant, nobody to reach, and not a paying company.
  const isRemovable = (s: TenantSubscription) =>
    s.activationStatus === "NeverStarted" && hasNoContact(s) && (s.status === SubscriptionStatus.Trialing || ["books", "hrfree", "pos"].includes(s.planCode.toLowerCase())) && s.outstandingAmount === 0;

  const followUpText = `Hello,\n\nYou created a Roma account recently and we noticed you haven't started using it yet. Are you still interested? If something was unclear or didn't fit, tell us and we'll help you set it up, or tell us it's not for you and we'll close the account.\n\nشكراً لتسجيلك في روما. لاحظنا إنك لسه ما بدأتش تستخدم الحساب. هل ما زال عندك اهتمام؟ لو في أي صعوبة قولنا ونساعدك، ولو مش محتاج الحساب قولنا ونقفله.\n\nRoma team`;

  async function handleDeleteInactive() {
    if (!systemKey || !subscriptions) return;
    const targets = subscriptions.filter(isRemovable);
    if (targets.length === 0) { setError("Nothing to remove on this page: no dormant company without an email and a phone."); return; }
    const typed = prompt(`DELETE FOREVER ${targets.length} companies that never started AND have no email and no phone:\n\n${targets.map((t) => `${t.companyNameEn} (${t.companyCode})`).join("\n")}\n\nThis cannot be undone. Type DELETE to confirm:`);
    if (typed !== "DELETE") return;
    setError(null);
    let failed = 0;
    for (const t of targets) {
      try { await SubscriptionsApi.deleteForever(systemKey, t.tenantId, t.companyCode); } catch { failed++; }
    }
    if (failed > 0) setError(`${failed} of ${targets.length} could not be removed.`);
    await loadAll();
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
          <SystemKeyInput value={systemKey} onChange={setSystemKey} />
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
                  <td>{p.isCustomPricing ? "—" : p.includedBranches >= 1000000 ? "Unlimited" : p.includedBranches}</td>
                  <td>{p.isCustomPricing ? "—" : p.includedUsers >= 1000000 ? "Unlimited" : p.includedUsers}</td>
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
            <button className="btn btn-secondary btn-sm" onClick={() => {
              // the companies on this page, with how to reach them
              const q = (v: unknown) => `"${String(v ?? "").replace(/"/g, '""')}"`;
              const rows = [["Company", "Code", "Plan", "Status", "Name", "Email", "Phone", "Last login"],
                ...subscriptions.map((s) => [s.companyNameEn, s.companyCode, s.planCode, statusLabel[s.status], s.contactName, s.contactEmail, s.contactPhone, s.lastLoginAtUtc ?? ""])];
              const blob = new Blob(["\ufeff" + rows.map((r) => r.map(q).join(",")).join("\n")], { type: "text/csv;charset=utf-8" });
              const a = document.createElement("a"); a.href = URL.createObjectURL(blob); a.download = "customers-contacts.csv"; a.click(); URL.revokeObjectURL(a.href);
            }}>Download contacts (CSV)</button>
            <select value={viewFilter} onChange={(e) => setViewFilter(e.target.value as "all" | "dormant")} style={{ maxWidth: 220 }}>
              <option value="all">Show: all companies</option>
              <option value="dormant">Show: never started</option>
            </select>
            <button className="btn btn-secondary btn-sm" title="Copies the emails of the never-started companies on this page" onClick={() => {
              const emails = subscriptions.filter((x) => isDormant(x) && x.contactEmail?.trim()).map((x) => x.contactEmail!.trim());
              void navigator.clipboard?.writeText(Array.from(new Set(emails)).join(", "));
              alert(`${new Set(emails).size} emails copied. Paste them in BCC and use the follow-up text (next button).`);
            }}>Copy emails (never started)</button>
            <button className="btn btn-secondary btn-sm" onClick={() => { void navigator.clipboard?.writeText(followUpText); alert("Follow-up message copied."); }}>Copy follow-up message</button>
            <button className="btn btn-sm" style={{ background: "#b42318", color: "#fff" }} onClick={handleDeleteInactive}>
              Delete never-started with no contact ({subscriptions.filter(isRemovable).length})
            </button>
          </div>
          {subscriptions.length === 0 && <div className="text-muted">No tenants found.</div>}
          {subscriptions.length > 0 && (
            <table>
              <thead>
                <tr>
                  <th>Company</th><th>Contact</th><th>Plan</th><th>Status</th><th>Usage</th><th>Activity</th><th>Period Ends</th><th>Outstanding</th><th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {subscriptions.filter((x) => viewFilter === "all" || isDormant(x)).map((s) => (
                  <tr key={s.tenantId}>
                    <td>{s.companyNameEn} <span className="text-muted">({s.companyCode})</span>
                      <div className="text-muted">{s.productScope === ProductScope.PeopleOnly ? "Roma HR only" : s.productScope === ProductScope.BooksOnly ? "Roma Books (free)" : s.productScope === ProductScope.PosOnly ? "Roma POS" : "Full ERP + HR"}</div></td>
                    <td style={{ fontSize: 13 }}>
                      {s.contactName && <div>{s.contactName}</div>}
                      {s.contactEmail && <div><a href={`mailto:${s.contactEmail}`}>{s.contactEmail}</a></div>}
                      {s.contactPhone
                        ? <div><a href={`https://wa.me/${s.contactPhone.replace(/[^\d]/g, "")}`} target="_blank" rel="noreferrer">{s.contactPhone}</a></div>
                        : <div className="text-muted">no phone</div>}
                    </td>
                    <td>
                      <select value={s.planId} onChange={(e) => handleSetPlan(s.tenantId, e.target.value)}>
                        {(plans ?? []).map((p) => <option key={p.id} value={p.id}>{p.nameEn}</option>)}
                      </select>
                    </td>
                    <td className={statusClass[s.status]}>{statusLabel[s.status]}{(s.discountMonthsLeft ?? 0) > 0 && <div style={{ fontSize: 12 }}>{s.discountPercent}% off · {s.discountMonthsLeft} mo left</div>}</td>
                    <td>{s.currentBranches} branches / {s.currentUsers} users</td>
                    <td>
                      {(() => {
                        const label: Record<string, string> = { NeverStarted: "Never started", LoggedInOnly: "Logged in, made nothing", Active: "Active", Idle: "Idle (no login for a week)" };
                        const color: Record<string, string> = { NeverStarted: "#b42318", LoggedInOnly: "#c8862e", Active: "#1a7f4b", Idle: "#c8862e" };
                        const st = s.activationStatus ?? "";
                        // Closeness to the free plan's limits is what signals an upgrade: the company has used all it was given, or nearly.
                        const ratio = (used: number, included: number | null) => (included ? used / included : 0);
                        const hit = Math.max(ratio(s.currentUsers, s.includedUsers), ratio(s.currentBranches, s.includedBranches));
                        return (
                          <>
                            {st && <div style={{ color: color[st] ?? undefined, fontWeight: 700 }}>{label[st] ?? st}</div>}
                            <div className="text-muted" style={{ fontSize: 12 }}>
                              {(s.salesInvoiceCount ?? 0)} sales / {(s.purchaseInvoiceCount ?? 0)} purchase invoices
                              {(s.restaurantOrderCount ?? 0) > 0 ? ` / ${s.restaurantOrderCount} POS orders` : ""} / {(s.employeeCount ?? 0)} employees
                            </div>
                            {s.createdAtUtc && (() => {
                              const days = Math.floor((Date.now() - new Date(s.createdAtUtc).getTime()) / 86400000);
                              return <div className="text-muted" style={{ fontSize: 12 }}>Opened {new Date(s.createdAtUtc).toLocaleDateString()} ({days} days ago{st === "NeverStarted" ? (days >= 15 ? " - can be removed" : ` - removable after day 15`) : ""})</div>;
                            })()}
                            {s.lastLoginAtUtc && <div className="text-muted" style={{ fontSize: 12 }}>Last login {new Date(s.lastLoginAtUtc).toLocaleDateString()}</div>}
                            {s.status === SubscriptionStatus.Trialing || hit >= 0.8 ? (hit >= 1 ? <div style={{ color: "#b42318", fontWeight: 700, fontSize: 12 }}>At the plan limit: ready to upgrade</div> : hit >= 0.8 ? <div style={{ color: "#c8862e", fontWeight: 700, fontSize: 12 }}>Near the plan limit</div> : null) : null}
                          </>
                        );
                      })()}
                    </td>
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
                      <button className="btn btn-secondary btn-sm" onClick={() => handleToggleScope(s)}>{s.productScope !== ProductScope.Full ? "Open full ERP" : "HR only"}</button>
                      <button className="btn btn-secondary btn-sm" onClick={() => handleFreeMonths(s)}>Free months</button>
                      <button className="btn btn-secondary btn-sm" onClick={() => handleDiscount(s)}>Discount</button>
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
