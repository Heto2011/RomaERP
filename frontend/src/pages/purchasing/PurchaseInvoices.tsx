import { useEffect, useState } from "react";
import { AccountsApi, LookupsApi, PurchasingApi } from "../../api/services";
import { PaymentTerm, type Account, type FiscalPeriod, type PurchaseInvoice, type PurchaseInvoiceLineInput, type Vendor } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";

const emptyLine = (defaultAccountId: string): PurchaseInvoiceLineInput => ({ description: "", accountId: defaultAccountId, quantity: 1, unitPrice: 0 });

export default function PurchaseInvoices() {
  const { t } = useLanguage();
  const [invoices, setInvoices] = useState<PurchaseInvoice[]>([]);
  const [vendors, setVendors] = useState<Vendor[]>([]);
  const [periods, setPeriods] = useState<FiscalPeriod[]>([]);
  const [accounts, setAccounts] = useState<Account[]>([]);
  const [error, setError] = useState<string | null>(null);

  const [showForm, setShowForm] = useState(false);
  const [vendorId, setVendorId] = useState("");
  const [fiscalPeriodId, setFiscalPeriodId] = useState("");
  const [invoiceDate, setInvoiceDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [paymentTerm, setPaymentTerm] = useState<PaymentTerm>(PaymentTerm.Cash);
  const [notes, setNotes] = useState("");
  const [lines, setLines] = useState<PurchaseInvoiceLineInput[]>([]);

  const [payingInvoice, setPayingInvoice] = useState<PurchaseInvoice | null>(null);
  const [payAmount, setPayAmount] = useState(0);
  const [payMethod, setPayMethod] = useState<PaymentTerm>(PaymentTerm.Cash);

  // Purchase invoice lines are coded to Expense or Asset accounts (AccountType.Expense = 5, AccountType.Asset = 1).
  const expenseAccounts = accounts.filter((a) => !a.isControlAccount && (a.accountType === 5 || a.accountType === 1));

  async function load() {
    const [invRes, vendRes, periodRes, accRes] = await Promise.all([
      PurchasingApi.getInvoices(),
      PurchasingApi.getVendors(),
      LookupsApi.fiscalPeriods(),
      AccountsApi.getAll(),
    ]);
    setInvoices(invRes.data);
    setVendors(vendRes.data);
    setPeriods(periodRes.data);
    setAccounts(accRes.data);
  }

  useEffect(() => {
    load();
  }, []);

  useEffect(() => {
    if (lines.length === 0 && expenseAccounts.length > 0) {
      setLines([emptyLine(expenseAccounts[0].id)]);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [accounts]);

  function updateLine(idx: number, patch: Partial<PurchaseInvoiceLineInput>) {
    setLines((prev) => prev.map((l, i) => (i === idx ? { ...l, ...patch } : l)));
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    try {
      await PurchasingApi.createInvoice({
        vendorId,
        invoiceDate,
        fiscalPeriodId,
        paymentTerm,
        notes: notes || null,
        lines,
      });
      setShowForm(false);
      setVendorId("");
      setFiscalPeriodId("");
      setNotes("");
      setLines(expenseAccounts.length > 0 ? [emptyLine(expenseAccounts[0].id)] : []);
      setPaymentTerm(PaymentTerm.Cash);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  function openPaymentDialog(invoice: PurchaseInvoice) {
    setPayingInvoice(invoice);
    setPayAmount(invoice.outstandingAmount);
    setPayMethod(PaymentTerm.Cash);
  }

  async function handleRecordPayment(e: React.FormEvent) {
    e.preventDefault();
    if (!payingInvoice) return;
    setError(null);
    try {
      await PurchasingApi.recordPayment(payingInvoice.id, {
        amount: payAmount,
        method: payMethod,
        paymentDate: new Date().toISOString().slice(0, 10),
      });
      setPayingInvoice(null);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  const paymentTermLabel: Record<PaymentTerm, string> = {
    [PaymentTerm.Cash]: t.paymentTerm.cash,
    [PaymentTerm.Card]: t.paymentTerm.card,
    [PaymentTerm.Credit]: t.paymentTerm.credit,
  };

  return (
    <div>
      <div className="page-header">
        <h1>{t.purchasing.invoicesTitle}</h1>
        <button className="btn" onClick={() => setShowForm((v) => !v)}>
          {showForm ? t.common.cancel : t.purchasing.newInvoice}
        </button>
      </div>

      {error && <div className="alert-error">{error}</div>}

      {showForm && (
        <div className="card">
          <form onSubmit={handleSubmit}>
            <div className="form-grid">
              <div className="form-field">
                <label>{t.purchasing.vendor}</label>
                <select value={vendorId} onChange={(e) => setVendorId(e.target.value)} required>
                  <option value="" disabled>-</option>
                  {vendors.map((v) => (
                    <option key={v.id} value={v.id}>{v.code} - {v.nameAr}</option>
                  ))}
                </select>
              </div>
              <div className="form-field">
                <label>{t.common.date}</label>
                <input type="date" value={invoiceDate} onChange={(e) => setInvoiceDate(e.target.value)} required />
              </div>
              <div className="form-field">
                <label>{t.common.fiscalPeriod}</label>
                <select value={fiscalPeriodId} onChange={(e) => setFiscalPeriodId(e.target.value)} required>
                  <option value="" disabled>-</option>
                  {periods.map((p) => (
                    <option key={p.id} value={p.id}>{p.name}</option>
                  ))}
                </select>
              </div>
              <div className="form-field">
                <label>{t.purchasing.paymentTerm}</label>
                <select value={paymentTerm} onChange={(e) => setPaymentTerm(Number(e.target.value) as PaymentTerm)}>
                  <option value={PaymentTerm.Cash}>💵 {t.paymentTerm.cash}</option>
                  <option value={PaymentTerm.Card}>💳 {t.paymentTerm.card}</option>
                  <option value={PaymentTerm.Credit}>🗓 {t.paymentTerm.credit}</option>
                </select>
              </div>
            </div>

            <div style={{ marginTop: 16 }}>
              <table>
                <thead>
                  <tr>
                    <th>{t.common.description}</th>
                    <th>{t.common.account}</th>
                    <th>{t.common.quantity}</th>
                    <th>{t.common.unitPrice}</th>
                    <th></th>
                  </tr>
                </thead>
                <tbody>
                  {lines.map((line, idx) => (
                    <tr key={idx}>
                      <td><input value={line.description} onChange={(e) => updateLine(idx, { description: e.target.value })} required /></td>
                      <td>
                        <select value={line.accountId} onChange={(e) => updateLine(idx, { accountId: e.target.value })} required>
                          {expenseAccounts.map((a) => (
                            <option key={a.id} value={a.id}>{a.code} - {a.nameAr}</option>
                          ))}
                        </select>
                      </td>
                      <td><input type="number" min={0.0001} step="0.01" value={line.quantity} onChange={(e) => updateLine(idx, { quantity: Number(e.target.value) })} style={{ width: 90 }} required /></td>
                      <td><input type="number" min={0} step="0.01" value={line.unitPrice} onChange={(e) => updateLine(idx, { unitPrice: Number(e.target.value) })} style={{ width: 110 }} required /></td>
                      <td>
                        {lines.length > 1 && (
                          <button type="button" className="btn btn-secondary btn-sm" onClick={() => setLines((prev) => prev.filter((_, i) => i !== idx))}>
                            {t.common.delete}
                          </button>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
              <button
                type="button"
                className="btn btn-secondary btn-sm"
                style={{ marginTop: 8 }}
                onClick={() => setLines((prev) => [...prev, emptyLine(expenseAccounts[0]?.id ?? "")])}
              >
                {t.purchasing.addLine}
              </button>
            </div>

            <div className="form-field" style={{ marginTop: 14 }}>
              <label>{t.common.notes}</label>
              <input value={notes} onChange={(e) => setNotes(e.target.value)} />
            </div>

            <button className="btn" type="submit" style={{ marginTop: 14 }}>
              {t.common.save}
            </button>
          </form>
        </div>
      )}

      <div className="card">
        <table>
          <thead>
            <tr>
              <th>{t.purchasing.invoiceNumber}</th>
              <th>{t.common.date}</th>
              <th>{t.purchasing.vendor}</th>
              <th>{t.common.total}</th>
              <th>{t.purchasing.paymentTerm}</th>
              <th>{t.common.outstanding}</th>
              <th>{t.common.actions}</th>
            </tr>
          </thead>
          <tbody>
            {invoices.length === 0 && (
              <tr>
                <td colSpan={7} className="text-muted" style={{ textAlign: "center", padding: 20 }}>
                  {t.common.noData}
                </td>
              </tr>
            )}
            {invoices.map((inv) => (
              <tr key={inv.id}>
                <td>{inv.invoiceNumber}</td>
                <td>{new Date(inv.invoiceDate).toLocaleDateString()}</td>
                <td>{inv.vendorName}</td>
                <td>{inv.totalAmount.toLocaleString()}</td>
                <td>{paymentTermLabel[inv.paymentTerm]}</td>
                <td>{inv.outstandingAmount.toLocaleString()}</td>
                <td>
                  {inv.paymentTerm === PaymentTerm.Credit && inv.outstandingAmount > 0 && (
                    <button className="btn btn-secondary btn-sm" title={t.purchasing.recordPayment} onClick={() => openPaymentDialog(inv)}>
                      💰 {t.purchasing.recordPayment}
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {payingInvoice && (
        <div className="modal-overlay" onClick={() => setPayingInvoice(null)}>
          <div className="card" style={{ maxWidth: 420, margin: "10% auto" }} onClick={(e) => e.stopPropagation()}>
            <h3>{t.purchasing.recordPaymentTitle} — {payingInvoice.invoiceNumber}</h3>
            <form onSubmit={handleRecordPayment}>
              <div className="form-field">
                <label>{t.common.amount}</label>
                <input type="number" min={0.01} max={payingInvoice.outstandingAmount} step="0.01" value={payAmount} onChange={(e) => setPayAmount(Number(e.target.value))} required />
              </div>
              <div className="form-field">
                <label>{t.purchasing.paymentTerm}</label>
                <select value={payMethod} onChange={(e) => setPayMethod(Number(e.target.value) as PaymentTerm)}>
                  <option value={PaymentTerm.Cash}>💵 {t.paymentTerm.cash}</option>
                  <option value={PaymentTerm.Card}>💳 {t.paymentTerm.card}</option>
                </select>
              </div>
              <div style={{ display: "flex", gap: 10, marginTop: 14 }}>
                <button className="btn" type="submit">{t.common.save}</button>
                <button className="btn btn-secondary" type="button" onClick={() => setPayingInvoice(null)}>{t.common.cancel}</button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
