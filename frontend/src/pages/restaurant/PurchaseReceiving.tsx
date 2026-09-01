import { useEffect, useState } from "react";
import { ItemsApi, LookupsApi, PurchasingApi, WarehousesApi } from "../../api/services";
import {
  PaymentTerm,
  type FiscalPeriod,
  type Item,
  type PurchaseInvoice,
  type ReceiveInventoryPurchaseLineInput,
  type Vendor,
  type Warehouse,
} from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";
import { bilingualName } from "../../i18n/bilingual";

const emptyLine = (): ReceiveInventoryPurchaseLineInput => ({ itemId: "", quantity: 1, unitCost: 0 });

export default function PurchaseReceivingPage() {
  const { t, lang } = useLanguage();
  const [vendors, setVendors] = useState<Vendor[]>([]);
  const [periods, setPeriods] = useState<FiscalPeriod[]>([]);
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [items, setItems] = useState<Item[]>([]);
  const [vatRate, setVatRate] = useState(0);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [lastReceipt, setLastReceipt] = useState<PurchaseInvoice | null>(null);

  const [vendorId, setVendorId] = useState("");
  const [fiscalPeriodId, setFiscalPeriodId] = useState("");
  const [warehouseId, setWarehouseId] = useState("");
  const [invoiceDate, setInvoiceDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [paymentTerm, setPaymentTerm] = useState<PaymentTerm>(PaymentTerm.Credit);
  const [notes, setNotes] = useState("");
  const [lines, setLines] = useState<ReceiveInventoryPurchaseLineInput[]>([emptyLine()]);

  const netTotal = lines.reduce((sum, l) => sum + l.quantity * l.unitCost, 0);
  const vatTotal = netTotal * vatRate;
  const grandTotal = netTotal + vatTotal;

  async function load() {
    const [vendRes, periodRes, whRes, itemsRes, settingsRes] = await Promise.all([
      PurchasingApi.getVendors(),
      LookupsApi.fiscalPeriods(),
      WarehousesApi.getAll(),
      ItemsApi.getAll(),
      LookupsApi.companySettings(),
    ]);
    setVendors(vendRes.data);
    setPeriods(periodRes.data);
    setWarehouses(whRes.data);
    setItems(itemsRes.data);
    setVatRate(settingsRes.data.vatRate);
  }

  useEffect(() => {
    load();
  }, []);

  useEffect(() => {
    if (!warehouseId && warehouses.length > 0) setWarehouseId(warehouses[0].id);
  }, [warehouses, warehouseId]);

  useEffect(() => {
    if (!fiscalPeriodId && periods.length > 0) {
      const open = periods.find((p) => !p.isClosed);
      if (open) setFiscalPeriodId(open.id);
    }
  }, [periods, fiscalPeriodId]);

  function updateLine(idx: number, patch: Partial<ReceiveInventoryPurchaseLineInput>) {
    setLines((prev) => prev.map((l, i) => (i === idx ? { ...l, ...patch } : l)));
  }

  function addLine() {
    setLines((prev) => [...prev, emptyLine()]);
  }

  function removeLine(idx: number) {
    setLines((prev) => (prev.length > 1 ? prev.filter((_, i) => i !== idx) : prev));
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    setLoading(true);
    try {
      const res = await PurchasingApi.receiveInventoryPurchase({
        vendorId,
        invoiceDate,
        fiscalPeriodId,
        warehouseId,
        paymentTerm,
        notes: notes || null,
        lines: lines.filter((l) => l.itemId),
      });
      setLastReceipt(res.data);
      setLines([emptyLine()]);
      setNotes("");
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setLoading(false);
    }
  }

  return (
    <div>
      <div className="page-header">
        <h1>{t.restaurant.purchaseReceivingTitle}</h1>
      </div>
      <p className="text-muted">{t.restaurant.purchaseReceivingIntro}</p>

      {error && <div className="alert-error">{error}</div>}

      {lastReceipt && (
        <div className="card" style={{ marginBottom: 16, borderInlineStart: "4px solid var(--color-success)" }}>
          <strong>{t.restaurant.receiptSaved} — {lastReceipt.invoiceNumber}</strong>
          <div className="text-muted" style={{ marginTop: 6 }}>
            {t.common.total}: {lastReceipt.totalAmount.toLocaleString()} ({t.common.vat}: {lastReceipt.vatAmount.toLocaleString()})
          </div>
        </div>
      )}

      <div className="card">
        <form onSubmit={handleSubmit}>
          <div className="form-grid">
            <div className="form-field">
              <label>{t.purchasing.vendor}</label>
              <select value={vendorId} onChange={(e) => setVendorId(e.target.value)} required>
                <option value="" disabled>-</option>
                {vendors.map((v) => (
                  <option key={v.id} value={v.id}>{v.code} - {bilingualName(v.nameAr, v.nameEn, lang)}</option>
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
                  <option key={p.id} value={p.id} disabled={p.isClosed}>{p.name}</option>
                ))}
              </select>
            </div>
            <div className="form-field">
              <label>{t.sales.warehouse}</label>
              <select value={warehouseId} onChange={(e) => setWarehouseId(e.target.value)} required>
                <option value="" disabled>-</option>
                {warehouses.map((w) => (
                  <option key={w.id} value={w.id}>{w.code} - {bilingualName(w.nameAr, w.nameEn, lang)}</option>
                ))}
              </select>
            </div>
            <div className="form-field">
              <label>{t.sales.paymentTerm}</label>
              <select value={paymentTerm} onChange={(e) => setPaymentTerm(Number(e.target.value))}>
                <option value={PaymentTerm.Credit}>{t.paymentTerm.credit}</option>
                <option value={PaymentTerm.Cash}>{t.paymentTerm.cash}</option>
                <option value={PaymentTerm.Card}>{t.paymentTerm.card}</option>
              </select>
            </div>
            <div className="form-field">
              <label>{t.common.notes}</label>
              <input value={notes} onChange={(e) => setNotes(e.target.value)} />
            </div>
          </div>

          <table style={{ marginTop: 16 }}>
            <thead>
              <tr>
                <th>{t.inventory.itemsTitle}</th>
                <th>{t.common.quantity}</th>
                <th>{t.restaurant.unitCostExVat}</th>
                <th>{t.common.total}</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {lines.map((line, idx) => (
                <tr key={idx}>
                  <td>
                    <select value={line.itemId} onChange={(e) => updateLine(idx, { itemId: e.target.value })} required>
                      <option value="" disabled>-</option>
                      {items.map((i) => (
                        <option key={i.id} value={i.id}>{i.code} - {bilingualName(i.nameAr, i.nameEn, lang)}</option>
                      ))}
                    </select>
                  </td>
                  <td>
                    <input type="number" min={0.001} step="0.001" style={{ width: 100 }} value={line.quantity}
                      onChange={(e) => updateLine(idx, { quantity: Number(e.target.value) })} required />
                  </td>
                  <td>
                    <input type="number" min={0} step="0.01" style={{ width: 120 }} value={line.unitCost}
                      onChange={(e) => updateLine(idx, { unitCost: Number(e.target.value) })} required />
                  </td>
                  <td>{(line.quantity * line.unitCost).toLocaleString()}</td>
                  <td>
                    <button type="button" className="btn btn-secondary btn-sm" onClick={() => removeLine(idx)}>{t.common.delete}</button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          <button type="button" className="btn btn-secondary btn-sm" style={{ marginTop: 10 }} onClick={addLine}>
            + {t.common.add}
          </button>

          <div className="card" style={{ marginTop: 16, maxWidth: 320, marginInlineStart: "auto" }}>
            <div style={{ display: "flex", justifyContent: "space-between" }}><span>{t.common.subtotal}</span><span>{netTotal.toLocaleString()}</span></div>
            <div style={{ display: "flex", justifyContent: "space-between" }}><span>{t.common.vat} ({(vatRate * 100).toFixed(0)}%)</span><span>{vatTotal.toLocaleString()}</span></div>
            <div style={{ display: "flex", justifyContent: "space-between", fontWeight: 700, marginTop: 6 }}><span>{t.common.total}</span><span>{grandTotal.toLocaleString()}</span></div>
          </div>

          <button className="btn" type="submit" disabled={loading} style={{ marginTop: 16 }}>
            {loading ? t.common.loading : t.restaurant.saveReceipt}
          </button>
        </form>
      </div>
    </div>
  );
}
