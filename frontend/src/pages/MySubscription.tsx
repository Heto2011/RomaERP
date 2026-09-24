import { useEffect, useState } from "react";
import { MySubscriptionApi } from "../api/services";
import type { TenantSubscription, SubscriptionInvoice, BankTransferInfo } from "../api/types";
import { getErrorMessage } from "../api/client";
import { useLanguage } from "../i18n/LanguageContext";

const SUBSCRIPTION_STATUS_KEY: Record<number, string> = {
  0: "statusTrialing",
  1: "statusActive",
  2: "statusPastDue",
  3: "statusSuspended",
  4: "statusCancelled",
};

const INVOICE_STATUS_KEY: Record<number, string> = {
  0: "invoicePending",
  1: "invoicePaid",
  2: "invoiceFailed",
  3: "invoiceCancelled",
};

export default function MySubscriptionPage() {
  const { t } = useLanguage();
  const [subscription, setSubscription] = useState<TenantSubscription | null>(null);
  const [invoices, setInvoices] = useState<SubscriptionInvoice[]>([]);
  const [bankInfo, setBankInfo] = useState<BankTransferInfo | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const [reportingInvoice, setReportingInvoice] = useState<SubscriptionInvoice | null>(null);
  const [paymentReference, setPaymentReference] = useState("");
  const [note, setNote] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [copiedField, setCopiedField] = useState<string | null>(null);

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  async function load() {
    setError(null);
    setLoading(true);
    try {
      const [subRes, invRes, bankRes] = await Promise.all([
        MySubscriptionApi.get(),
        MySubscriptionApi.getInvoices(),
        MySubscriptionApi.getBankTransferInfo(),
      ]);
      setSubscription(subRes.data);
      setInvoices(invRes.data);
      setBankInfo(bankRes.data);
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setLoading(false);
    }
  }

  function startReport(invoice: SubscriptionInvoice) {
    setReportingInvoice(invoice);
    setPaymentReference("");
    setNote("");
    setSuccessMessage(null);
    setError(null);
  }

  async function submitReport(e: React.FormEvent) {
    e.preventDefault();
    if (!reportingInvoice) return;
    setSubmitting(true);
    setError(null);
    try {
      const res = await MySubscriptionApi.reportPayment(reportingInvoice.id, paymentReference || null, note || null);
      setSuccessMessage(t.mySubscription.reportSuccess(res.data.ticketNumber));
      setReportingInvoice(null);
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setSubmitting(false);
    }
  }

  function copy(field: string, value: string) {
    navigator.clipboard?.writeText(value).then(() => {
      setCopiedField(field);
      setTimeout(() => setCopiedField(null), 1500);
    });
  }

  if (loading) return <div className="text-muted" style={{ padding: 40 }}>{t.common.loading}</div>;

  return (
    <div>
      <div className="page-header">
        <h1>{t.mySubscription.title}</h1>
      </div>
      <p className="text-muted">{t.mySubscription.intro}</p>

      {error && <div className="alert-error">{error}</div>}
      {successMessage && (
        <div className="card" style={{ borderColor: "var(--color-success)" }}>
          <strong className="text-success">{successMessage}</strong>
        </div>
      )}

      {subscription && (
        <div className="card">
          <h3 style={{ marginTop: 0 }}>{t.mySubscription.currentPlan}</h3>
          <table>
            <tbody>
              <tr>
                <td>{t.mySubscription.currentPlan}</td>
                <td><strong>{subscription.planNameAr}</strong></td>
              </tr>
              <tr>
                <td>{t.common.status}</td>
                <td>
                  <span className={`badge ${subscription.status === 1 ? "badge-posted" : subscription.status >= 2 ? "badge-draft" : ""}`}>
                    {t.mySubscription[SUBSCRIPTION_STATUS_KEY[subscription.status] as keyof typeof t.mySubscription] as string}
                  </span>
                </td>
              </tr>
              <tr>
                <td>{t.mySubscription.period}</td>
                <td>{new Date(subscription.currentPeriodStart).toLocaleDateString()} — {new Date(subscription.currentPeriodEnd).toLocaleDateString()}</td>
              </tr>
              <tr>
                <td>{t.mySubscription.branchesUsage}</td>
                <td>{subscription.currentBranches}</td>
              </tr>
              <tr>
                <td>{t.mySubscription.usersUsage}</td>
                <td>{subscription.currentUsers}</td>
              </tr>
              <tr>
                <td>{t.mySubscription.outstanding}</td>
                <td>
                  {subscription.outstandingAmount > 0 ? (
                    <strong className="text-danger mono">{subscription.outstandingAmount.toLocaleString()}</strong>
                  ) : (
                    <span className="text-success">{t.mySubscription.noOutstanding}</span>
                  )}
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      )}

      <div className="card">
        <h3 style={{ marginTop: 0 }}>{t.mySubscription.invoicesTitle}</h3>
        {invoices.length === 0 && <div className="text-muted">{t.mySubscription.noInvoices}</div>}
        {invoices.length > 0 && (
          <table>
            <thead>
              <tr>
                <th>{t.mySubscription.invoicePeriod}</th>
                <th>{t.mySubscription.invoiceAmount}</th>
                <th>{t.mySubscription.invoiceStatus}</th>
                <th>{t.mySubscription.invoiceDue}</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {invoices.map((inv) => (
                <tr key={inv.id}>
                  <td>{new Date(inv.periodStart).toLocaleDateString()} — {new Date(inv.periodEnd).toLocaleDateString()}</td>
                  <td className="mono">{inv.totalAmount.toLocaleString()} {inv.currency}</td>
                  <td>
                    <span className={`badge ${inv.status === 1 ? "badge-posted" : "badge-draft"}`}>
                      {t.mySubscription[INVOICE_STATUS_KEY[inv.status] as keyof typeof t.mySubscription] as string}
                    </span>
                  </td>
                  <td>{new Date(inv.dueDateUtc).toLocaleDateString()}</td>
                  <td>
                    {inv.status === 0 && (
                      <button className="btn btn-secondary btn-sm" onClick={() => startReport(inv)}>
                        {t.mySubscription.reportPaymentBtn}
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {reportingInvoice && (
        <div className="card" style={{ borderInlineStart: "4px solid var(--color-primary)" }}>
          <h3 style={{ marginTop: 0 }}>{t.mySubscription.reportPaymentTitle}</h3>
          <form onSubmit={submitReport}>
            <div className="form-grid">
              <div className="form-field">
                <label>{t.mySubscription.paymentReferenceLabel}</label>
                <input value={paymentReference} onChange={(e) => setPaymentReference(e.target.value)} />
              </div>
              <div className="form-field">
                <label>{t.mySubscription.paymentNoteLabel}</label>
                <input value={note} onChange={(e) => setNote(e.target.value)} />
              </div>
            </div>
            <div style={{ display: "flex", gap: 8, marginTop: 14 }}>
              <button className="btn btn-primary" type="submit" disabled={submitting}>
                {submitting ? t.common.loading : t.mySubscription.submitReport}
              </button>
              <button className="btn btn-secondary" type="button" onClick={() => setReportingInvoice(null)}>
                {t.mySubscription.cancel}
              </button>
            </div>
          </form>
        </div>
      )}

      <div className="card">
        <h3 style={{ marginTop: 0 }}>{t.mySubscription.bankTransferTitle}</h3>
        {!bankInfo?.configured && <div className="text-muted">{t.mySubscription.bankNotConfigured}</div>}
        {bankInfo?.configured && (
          <table>
            <tbody>
              {([
                ["accountName", bankInfo.accountName],
                ["iban", bankInfo.iban],
                ["swift", bankInfo.swift],
                ["bankName", bankInfo.bankName],
              ] as const).map(([field, value]) =>
                value ? (
                  <tr key={field}>
                    <td>{t.mySubscription[field]}</td>
                    <td className="mono">{value}</td>
                    <td>
                      <button className="btn btn-secondary btn-sm" type="button" onClick={() => copy(field, value)}>
                        {copiedField === field ? t.mySubscription.copied : t.mySubscription.copy}
                      </button>
                    </td>
                  </tr>
                ) : null
              )}
            </tbody>
          </table>
        )}
        {bankInfo?.instaPayMobile && (
          <>
            <h4>{t.mySubscription.instaPayTitle}</h4>
            <table>
              <tbody>
                <tr>
                  <td>{t.mySubscription.instaPayMobile}</td>
                  <td className="mono">{bankInfo.instaPayMobile}</td>
                  <td>
                    <button className="btn btn-secondary btn-sm" type="button" onClick={() => copy("instaPayMobile", bankInfo.instaPayMobile!)}>
                      {copiedField === "instaPayMobile" ? t.mySubscription.copied : t.mySubscription.copy}
                    </button>
                  </td>
                </tr>
              </tbody>
            </table>
          </>
        )}
      </div>
    </div>
  );
}
