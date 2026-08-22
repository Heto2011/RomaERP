import { useEffect, useState } from "react";
import { LookupsApi, SalesApi } from "../../api/services";
import { PaymentTerm, type Customer, type FiscalPeriod, type SalesInvoice, type SalesInvoiceLineInput } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";

const emptyLine = (): SalesInvoiceLineInput => ({ description: "", quantity: 1, unitPrice: 0 });

export default function SalesInvoices() {
  const { t } = useLanguage();
  const [invoices, setInvoices] = useState<SalesInvoice[]>([]);
  const [customers, setCustomers] = useState<Customer[]>([]);
  const [periods, setPeriods] = useState<FiscalPeriod[]>([]);
  const [error, setError] = useState<string | null>(null);

  const [showForm, setShowForm] = useState(false);
  const [customerId, setCustomerId] = useState("");
  const [fiscalPeriodId, setFiscalPeriodId] = useState("");
  const [invoiceDate, setInvoiceDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [paymentTerm, setPaymentTerm] = useState<PaymentTerm>(PaymentTerm.Cash);
  const [notes, setNotes] = useState("");
  const [lines, setLines] = useState<SalesInvoiceLineInput[]>([emptyLine()]);

  const [payingInvoice, setPayingInvoice] = useState<SalesInvoice | null>(null);
  const [payAmount, setPayAmount] = useState(0);
  const [payMethod, setPayMethod] = useState<PaymentTerm>(PaymentTerm.Cash);

  async function load() {
    const [invRes, custRes, periodRes] = await Promise.all([
      SalesApi.getInvoices(),
      SalesApi.getCustomers(),
      LookupsApi.fiscalPeriods(),
    ]);
    setInvoices(invRes.data);
    setCustomers(custRes.data);
    setPeriods(periodRes.data);
  }

  useEffect(() => {
    load();
  }, []);

  function updateLine(idx: number, patch: Partial<SalesInvoiceLineInput>) {
    setLines((prev) => prev.map((l, i) => (i === idx ? { ...l, ...patch } : l)));
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    try {
      await SalesApi.createInvoice({
        customerId,
        invoiceDate,
        fiscalPeriodId,
        paymentTerm,
        notes: notes || null,
        lines,
      });
      setShowForm(false);
      setCustomerId("");
      setFiscalPeriodId("");
      setNotes("");
      setLines([emptyLine()]);
      setPaymentTerm(PaymentTerm.Cash);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  function openPaymentDialog(invoice: SalesInvoice) {
    setPayingInvoice(invoice);
    setPayAmount(invoice.outstandingAmount);
    setPayMethod(PaymentTerm.Cash);
  }

  async function handleRecordPayment(e: React.FormEvent) {
    e.preventDefault();
    if (!payingInvoice) return;
    setError(null);
    try {
      await SalesApi.recordPayment(payingInvoice.id, {
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
        <h1>{t.sales.invoicesTitle}</h1>
        <button className="btn" onClick={() => setShowForm((v) => !v)}>
          {showForm ? t.common.cancel : t.sales.newInvoice}
        </button>
      </div>

      {error && <div className="alert-error">{error}</div>}

      {showForm && (
        <div className="card">
          <form onSubmit={handleSubmit}>
            <div className="form-grid">
              <div className="form-field">
                <label>{t.sales.customer}</label>
                <select value={customerId} onChange={(e) => setCustomerId(e.target.value)} required>
                  <option value="" disabled>-</option>
                  {customers.map((c) => (
                    <option key={c.id} value={c.id}>{c.code} - {c.nameAr}</option>
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
                <label>{t.sales.paymentTerm}</label>
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
                    <th>{t.common.quantity}</th>
                    <th>{t.common.unitPrice}</th>
                    <th></th>
                  </tr>
                </thead>
                <tbody>
                  {lines.map((line, idx) => (
                    <tr key={idx}>
                      <td><input value={line.description} onChange={(e) => updateLine(idx, { description: e.target.value })} required /></td>
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
              <button type="button" className="btn btn-secondary btn-sm" style={{ marginTop: 8 }} onClick={() => setLines((prev) => [...prev, emptyLine()])}>
                {t.sales.addLine}
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
              <th>{t.sales.invoiceNumber}</th>
              <th>{t.common.date}</th>
              <th>{t.sales.customer}</th>
              <th>{t.common.total}</th>
              <th>{t.sales.paymentTerm}</th>
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
                <td>{inv.customerName}</td>
                <td>{inv.totalAmount.toLocaleString()}</td>
                <td>{paymentTermLabel[inv.paymentTerm]}</td>
                <td>{inv.outstandingAmount.toLocaleString()}</td>
                <td>
                  {inv.paymentTerm === PaymentTerm.Credit && inv.outstandingAmount > 0 && (
                    <button className="btn btn-secondary btn-sm" title={t.sales.recordPayment} onClick={() => openPaymentDialog(inv)}>
                      💰 {t.sales.recordPayment}
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
            <h3>{t.sales.recordPaymentTitle} — {payingInvoice.invoiceNumber}</h3>
            <form onSubmit={handleRecordPayment}>
              <div className="form-field">
                <label>{t.common.amount}</label>
                <input type="number" min={0.01} max={payingInvoice.outstandingAmount} step="0.01" value={payAmount} onChange={(e) => setPayAmount(Number(e.target.value))} required />
              </div>
              <div className="form-field">
                <label>{t.sales.paymentTerm}</label>
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
