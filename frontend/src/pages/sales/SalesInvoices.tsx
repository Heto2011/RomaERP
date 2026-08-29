import { Fragment, useEffect, useState } from "react";
import { ItemsApi, LookupsApi, SalesApi, WarehousesApi } from "../../api/services";
import { EInvoiceStatus, PaymentTerm, type Customer, type FiscalPeriod, type Item, type SalesInvoice, type SalesInvoiceLineInput, type Warehouse } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";

const emptyLine = (): SalesInvoiceLineInput => ({ description: "", quantity: 1, unitPrice: 0, itemId: null });

export default function SalesInvoices() {
  const { t } = useLanguage();
  const [invoices, setInvoices] = useState<SalesInvoice[]>([]);
  const [customers, setCustomers] = useState<Customer[]>([]);
  const [periods, setPeriods] = useState<FiscalPeriod[]>([]);
  const [items, setItems] = useState<Item[]>([]);
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [vatRate, setVatRate] = useState(0);
  const [error, setError] = useState<string | null>(null);

  const [showForm, setShowForm] = useState(false);
  const [customerId, setCustomerId] = useState("");
  const [fiscalPeriodId, setFiscalPeriodId] = useState("");
  const [warehouseId, setWarehouseId] = useState("");
  const [invoiceDate, setInvoiceDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [paymentTerm, setPaymentTerm] = useState<PaymentTerm>(PaymentTerm.Cash);
  const [numberOfInstallments, setNumberOfInstallments] = useState(3);
  const [firstInstallmentDueDate, setFirstInstallmentDueDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [notes, setNotes] = useState("");
  const [lines, setLines] = useState<SalesInvoiceLineInput[]>([emptyLine()]);
  const [expandedInvoiceId, setExpandedInvoiceId] = useState<string | null>(null);

  const [payingInvoice, setPayingInvoice] = useState<SalesInvoice | null>(null);
  const [payAmount, setPayAmount] = useState(0);
  const [payMethod, setPayMethod] = useState<PaymentTerm>(PaymentTerm.Cash);

  const [submittingEInvoiceId, setSubmittingEInvoiceId] = useState<string | null>(null);

  const netTotal = lines.reduce((sum, l) => sum + l.quantity * l.unitPrice, 0);
  const vatTotal = netTotal * vatRate;
  const grandTotal = netTotal + vatTotal;
  const hasItemLines = lines.some((l) => l.itemId);

  async function load() {
    const [invRes, custRes, periodRes, settingsRes, itemsRes, warehousesRes] = await Promise.all([
      SalesApi.getInvoices(),
      SalesApi.getCustomers(),
      LookupsApi.fiscalPeriods(),
      LookupsApi.companySettings(),
      ItemsApi.getAll(),
      WarehousesApi.getAll(),
    ]);
    setInvoices(invRes.data);
    setCustomers(custRes.data);
    setPeriods(periodRes.data);
    setVatRate(settingsRes.data.vatRate);
    setItems(itemsRes.data);
    setWarehouses(warehousesRes.data);
  }

  useEffect(() => {
    load();
  }, []);

  function updateLine(idx: number, patch: Partial<SalesInvoiceLineInput>) {
    setLines((prev) => prev.map((l, i) => (i === idx ? { ...l, ...patch } : l)));
  }

  function selectLineItem(idx: number, itemId: string) {
    if (!itemId) {
      updateLine(idx, { itemId: null });
      return;
    }
    const item = items.find((i) => i.id === itemId);
    setLines((prev) => prev.map((l, i) => (i === idx ? { ...l, itemId, description: item ? item.nameAr : l.description } : l)));
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    if (hasItemLines && !warehouseId) {
      setError("لازم تحدد المخزن لما تختار صنف من المخزون في بند الفاتورة.");
      return;
    }
    try {
      await SalesApi.createInvoice({
        customerId,
        invoiceDate,
        fiscalPeriodId,
        paymentTerm,
        notes: notes || null,
        warehouseId: warehouseId || null,
        lines,
        numberOfInstallments: paymentTerm === PaymentTerm.Installment ? numberOfInstallments : null,
        firstInstallmentDueDate: paymentTerm === PaymentTerm.Installment ? firstInstallmentDueDate : null,
      });
      setShowForm(false);
      setCustomerId("");
      setFiscalPeriodId("");
      setWarehouseId("");
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

  async function handleDownloadPdf(invoice: SalesInvoice) {
    setError(null);
    try {
      const res = await SalesApi.downloadInvoicePdf(invoice.id);
      const url = window.URL.createObjectURL(res.data as Blob);
      const link = document.createElement("a");
      link.href = url;
      link.download = `${invoice.invoiceNumber}.pdf`;
      link.click();
      window.URL.revokeObjectURL(url);
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  async function handleSubmitEInvoice(invoiceId: string) {
    setError(null);
    setSubmittingEInvoiceId(invoiceId);
    try {
      await SalesApi.submitEInvoice(invoiceId);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setSubmittingEInvoiceId(null);
    }
  }

  const paymentTermLabel: Record<PaymentTerm, string> = {
    [PaymentTerm.Cash]: t.paymentTerm.cash,
    [PaymentTerm.Card]: t.paymentTerm.card,
    [PaymentTerm.Credit]: t.paymentTerm.credit,
    [PaymentTerm.Installment]: t.paymentTerm.installment,
  };

  const eInvoiceStatusLabel: Record<EInvoiceStatus, string> = {
    [EInvoiceStatus.NotSubmitted]: t.eInvoicing.statuses.notSubmitted,
    [EInvoiceStatus.Submitted]: t.eInvoicing.statuses.submitted,
    [EInvoiceStatus.Accepted]: t.eInvoicing.statuses.accepted,
    [EInvoiceStatus.Rejected]: t.eInvoicing.statuses.rejected,
  };

  const eInvoiceStatusClass: Record<EInvoiceStatus, string> = {
    [EInvoiceStatus.NotSubmitted]: "text-muted",
    [EInvoiceStatus.Submitted]: "text-muted",
    [EInvoiceStatus.Accepted]: "text-success",
    [EInvoiceStatus.Rejected]: "text-danger",
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
                  <option value={PaymentTerm.Installment}>📅 {t.paymentTerm.installment}</option>
                </select>
              </div>
              <div className="form-field">
                <label>{t.sales.warehouse}{hasItemLines ? " *" : ""}</label>
                <select value={warehouseId} onChange={(e) => setWarehouseId(e.target.value)} required={hasItemLines}>
                  <option value="">-</option>
                  {warehouses.map((w) => (
                    <option key={w.id} value={w.id}>{w.code} - {w.nameAr}</option>
                  ))}
                </select>
              </div>
              {paymentTerm === PaymentTerm.Installment && (
                <>
                  <div className="form-field">
                    <label>{t.sales.numberOfInstallments}</label>
                    <input type="number" min={2} step={1} value={numberOfInstallments} onChange={(e) => setNumberOfInstallments(Number(e.target.value))} required />
                  </div>
                  <div className="form-field">
                    <label>{t.sales.firstInstallmentDueDate}</label>
                    <input type="date" value={firstInstallmentDueDate} onChange={(e) => setFirstInstallmentDueDate(e.target.value)} required />
                  </div>
                </>
              )}
            </div>

            <div style={{ marginTop: 16 }}>
              <table>
                <thead>
                  <tr>
                    <th>{t.sales.item}</th>
                    <th>{t.common.description}</th>
                    <th>{t.common.quantity}</th>
                    <th>{t.common.unitPrice}</th>
                    <th>{t.common.vat}</th>
                    <th></th>
                  </tr>
                </thead>
                <tbody>
                  {lines.map((line, idx) => (
                    <tr key={idx}>
                      <td>
                        <select value={line.itemId ?? ""} onChange={(e) => selectLineItem(idx, e.target.value)} style={{ width: 160 }}>
                          <option value="">{t.sales.serviceLine}</option>
                          {items.map((i) => (
                            <option key={i.id} value={i.id}>{i.code} - {i.nameAr} ({i.quantityOnHand})</option>
                          ))}
                        </select>
                      </td>
                      <td><input value={line.description} onChange={(e) => updateLine(idx, { description: e.target.value })} required /></td>
                      <td><input type="number" min={0.0001} step="0.01" value={line.quantity} onChange={(e) => updateLine(idx, { quantity: Number(e.target.value) })} style={{ width: 90 }} required /></td>
                      <td><input type="number" min={0} step="0.01" value={line.unitPrice} onChange={(e) => updateLine(idx, { unitPrice: Number(e.target.value) })} style={{ width: 110 }} required /></td>
                      <td className="text-muted">{(vatRate * 100).toFixed(0)}%</td>
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

              <div style={{ display: "flex", justifyContent: "flex-end", marginTop: 12 }}>
                <table style={{ width: 260 }}>
                  <tbody>
                    <tr>
                      <td>{t.common.subtotal}</td>
                      <td style={{ textAlign: "end" }}>{netTotal.toLocaleString()}</td>
                    </tr>
                    <tr>
                      <td>{t.common.vat} ({(vatRate * 100).toFixed(0)}%)</td>
                      <td style={{ textAlign: "end" }}>{vatTotal.toLocaleString()}</td>
                    </tr>
                    <tr>
                      <td><strong>{t.common.total}</strong></td>
                      <td style={{ textAlign: "end" }}><strong>{grandTotal.toLocaleString()}</strong></td>
                    </tr>
                  </tbody>
                </table>
              </div>
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
              <th>{t.common.subtotal}</th>
              <th>{t.common.vat}</th>
              <th>{t.common.total}</th>
              <th>{t.sales.paymentTerm}</th>
              <th>{t.common.outstanding}</th>
              <th>{t.sales.eInvoice}</th>
              <th>{t.common.actions}</th>
            </tr>
          </thead>
          <tbody>
            {invoices.length === 0 && (
              <tr>
                <td colSpan={10} className="text-muted" style={{ textAlign: "center", padding: 20 }}>
                  {t.common.noData}
                </td>
              </tr>
            )}
            {invoices.map((inv) => (
              <Fragment key={inv.id}>
                <tr>
                  <td>{inv.invoiceNumber}</td>
                  <td>{new Date(inv.invoiceDate).toLocaleDateString()}</td>
                  <td>{inv.customerName}</td>
                  <td>{inv.subTotal.toLocaleString()}</td>
                  <td>{inv.vatAmount.toLocaleString()}</td>
                  <td>{inv.totalAmount.toLocaleString()}</td>
                  <td>{paymentTermLabel[inv.paymentTerm]}</td>
                  <td>{inv.outstandingAmount.toLocaleString()}</td>
                  <td>
                    <span className={eInvoiceStatusClass[inv.eInvoiceStatus]} title={inv.eInvoiceErrorMessage ?? undefined}>
                      {eInvoiceStatusLabel[inv.eInvoiceStatus]}
                    </span>
                  </td>
                  <td style={{ display: "flex", gap: 6, flexWrap: "wrap" }}>
                    <button className="btn btn-secondary btn-sm" title={t.sales.downloadPdf} onClick={() => handleDownloadPdf(inv)}>
                      🖨️ {t.sales.downloadPdf}
                    </button>
                    {(inv.paymentTerm === PaymentTerm.Credit || inv.paymentTerm === PaymentTerm.Installment) && inv.outstandingAmount > 0 && (
                      <button className="btn btn-secondary btn-sm" title={t.sales.recordPayment} onClick={() => openPaymentDialog(inv)}>
                        💰 {t.sales.recordPayment}
                      </button>
                    )}
                    {inv.paymentTerm === PaymentTerm.Installment && (
                      <button
                        className="btn btn-secondary btn-sm"
                        onClick={() => setExpandedInvoiceId(expandedInvoiceId === inv.id ? null : inv.id)}
                      >
                        📅 {t.sales.viewInstallments}
                      </button>
                    )}
                    {inv.eInvoiceStatus !== EInvoiceStatus.Accepted && inv.eInvoiceStatus !== EInvoiceStatus.Submitted && (
                      <button
                        className="btn btn-secondary btn-sm"
                        disabled={submittingEInvoiceId === inv.id}
                        onClick={() => handleSubmitEInvoice(inv.id)}
                      >
                        {submittingEInvoiceId === inv.id ? t.eInvoicing.submitting : `🧾 ${t.eInvoicing.submit}`}
                      </button>
                    )}
                  </td>
                </tr>
                {expandedInvoiceId === inv.id && (
                  <tr>
                    <td colSpan={10}>
                      <table>
                        <thead>
                          <tr>
                            <th>{t.sales.installmentNumber}</th>
                            <th>{t.sales.dueDate}</th>
                            <th>{t.common.amount}</th>
                            <th>{t.common.status}</th>
                          </tr>
                        </thead>
                        <tbody>
                          {inv.installmentLines.map((l) => (
                            <tr key={l.installmentNumber}>
                              <td>{l.installmentNumber}</td>
                              <td>{new Date(l.dueDate).toLocaleDateString()}</td>
                              <td>{l.amount.toLocaleString()}</td>
                              <td>
                                <span className={l.isPaid ? "badge badge-posted" : "badge badge-draft"}>
                                  {l.isPaid ? t.sales.installmentPaid : t.sales.installmentUnpaid}
                                </span>
                              </td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </td>
                  </tr>
                )}
              </Fragment>
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
