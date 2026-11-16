import { useEffect, useState } from "react";
import { ItemsApi, StockTransfersApi, WarehousesApi } from "../../api/services";
import type { Item, Warehouse } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";

const TEXT = {
  ar: {
    title: "تسوية مخزون", intro: "لو في بضاعة موجودة في الفرع ومش مسجلة (نسيت تدخلها) أو ناقصة، سجّلها هنا بسبب واضح. الزيادة مبتتسجلش على مورد، والنقص بيتحمّل على تكلفة البضاعة، وكله بقيد محاسبي تلقائي.",
    add: "تسوية جديدة", branch: "الفرع", item: "الصنف", direction: "نوع التسوية", up: "زيادة (موجودة ومش مسجلة)", down: "نقص (ناقصة)", qty: "الكمية",
    cost: "تكلفة الوحدة", costHint: "سيبها فاضية لو عايز متوسط تكلفة الصنف", reason: "السبب", date: "التاريخ", notes: "ملاحظات", lot: "رقم الدُفعة", expiry: "تاريخ الانتهاء",
    choose: "اختار", save: "تسجيل التسوية", saved: "تم تسجيل التسوية.", empty: "مفيش تسويات.", number: "الرقم", total: "القيمة", by: "سجّلها",
    r1: "رصيد افتتاحي", r2: "زيادة جرد", r3: "نقص جرد", r4: "أخرى",
  },
  en: {
    title: "Stock adjustment", intro: "If goods are in a branch but were never recorded, or are missing, record it here with a clear reason. An increase is not booked against a supplier; a decrease is charged to cost of goods; both post a journal entry automatically.",
    add: "New adjustment", branch: "Branch", item: "Item", direction: "Type", up: "Increase (there but not recorded)", down: "Decrease (missing)", qty: "Quantity",
    cost: "Unit cost", costHint: "Leave empty to use the item's average cost", reason: "Reason", date: "Date", notes: "Notes", lot: "Lot number", expiry: "Expiry date",
    choose: "Choose", save: "Record adjustment", saved: "Adjustment recorded.", empty: "No adjustments.", number: "Number", total: "Value", by: "By",
    r1: "Opening stock", r2: "Count gain", r3: "Count loss", r4: "Other",
  },
};

type Row = { id: string; number: string; adjustmentDate: string; warehouseName: string; itemCode: string; itemName: string; isIncrease: boolean; quantity: number; unitCost: number; totalCost: number; reason: number; notes?: string; createdByName?: string };
const qtyFmt = (n: number) => n.toLocaleString(undefined, { maximumFractionDigits: 4 });
const money = (n: number) => n.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });

export default function StockAdjustments() {
  const { lang } = useLanguage();
  const x = TEXT[lang === "ar" ? "ar" : "en"];
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [items, setItems] = useState<Item[]>([]);
  const [rows, setRows] = useState<Row[]>([]);
  const [open, setOpen] = useState(false);
  const [warehouseId, setWarehouseId] = useState("");
  const [itemId, setItemId] = useState("");
  const [isIncrease, setIsIncrease] = useState(true);
  const [quantity, setQuantity] = useState("");
  const [unitCost, setUnitCost] = useState("");
  const [reason, setReason] = useState(1);
  const [date, setDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [notes, setNotes] = useState("");
  const [lot, setLot] = useState("");
  const [expiry, setExpiry] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const reasonLabel = (r: number) => ({ 1: x.r1, 2: x.r2, 3: x.r3, 4: x.r4 } as Record<number, string>)[r] ?? String(r);
  const reasons = isIncrease ? [1, 2, 4] : [3, 4];
  const item = items.find((i) => i.id === itemId);

  async function load() { try { setRows((await StockTransfersApi.adjustments()).data as Row[]); } catch (e) { setError(getErrorMessage(e)); } }
  useEffect(() => {
    (async () => {
      try {
        const [w, i] = await Promise.all([WarehousesApi.getAll(), ItemsApi.getAll()]);
        setWarehouses(w.data.filter((a) => a.isActive)); setItems(i.data);
      } catch (e) { setError(getErrorMessage(e)); }
      load();
    })();
  }, []);

  async function submit(e: React.FormEvent) {
    e.preventDefault(); setError(null); setMessage(null); setBusy(true);
    try {
      await StockTransfersApi.createAdjustment({
        warehouseId, itemId, isIncrease, quantity: Number(quantity), unitCost: unitCost ? Number(unitCost) : undefined, reason, adjustmentDate: date,
        notes: notes || undefined, lotNumber: lot || undefined, expiryDate: expiry || undefined,
      });
      setMessage(x.saved); setQuantity(""); setUnitCost(""); setNotes(""); setLot(""); setExpiry(""); setOpen(false);
      await load();
    } catch (err) { setError(getErrorMessage(err)); } finally { setBusy(false); }
  }

  return (
    <div>
      <div className="page-header">
        <h1>{x.title}</h1>
        <button className="btn btn-sm" onClick={() => setOpen(!open)}>{x.add}</button>
      </div>
      <p className="text-muted">{x.intro}</p>
      {error && <div className="alert-error">{error}</div>}
      {message && <div className="card" style={{ marginBottom: 12 }}>{message}</div>}

      {open && (
        <form className="card" style={{ marginBottom: 16 }} onSubmit={submit}>
          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", gap: 12 }}>
            <div className="form-field"><label>{x.branch}</label>
              <select value={warehouseId} onChange={(e) => setWarehouseId(e.target.value)} required>
                <option value="">{x.choose}</option>{warehouses.map((w) => <option key={w.id} value={w.id}>{lang === "ar" ? w.nameAr : w.nameEn || w.nameAr}</option>)}
              </select></div>
            <div className="form-field"><label>{x.item}</label>
              <select value={itemId} onChange={(e) => setItemId(e.target.value)} required>
                <option value="">{x.choose}</option>{items.map((i) => <option key={i.id} value={i.id}>{i.code} — {lang === "ar" ? i.nameAr : i.nameEn || i.nameAr}</option>)}
              </select></div>
            <div className="form-field"><label>{x.direction}</label>
              <select value={isIncrease ? "up" : "down"} onChange={(e) => { const up = e.target.value === "up"; setIsIncrease(up); setReason(up ? 1 : 3); }}>
                <option value="up">{x.up}</option><option value="down">{x.down}</option>
              </select></div>
            <div className="form-field"><label>{x.reason}</label>
              <select value={reason} onChange={(e) => setReason(Number(e.target.value))}>{reasons.map((r) => <option key={r} value={r}>{reasonLabel(r)}</option>)}</select></div>
            <div className="form-field"><label>{x.qty}</label><input type="number" min="0" step="any" value={quantity} onChange={(e) => setQuantity(e.target.value)} required /></div>
            {isIncrease && <div className="form-field"><label>{x.cost}</label><input type="number" min="0" step="any" value={unitCost} onChange={(e) => setUnitCost(e.target.value)} placeholder={x.costHint} /></div>}
            <div className="form-field"><label>{x.date}</label><input type="date" value={date} onChange={(e) => setDate(e.target.value)} required /></div>
            {item?.isLotTracked && <div className="form-field"><label>{x.lot}</label><input value={lot} onChange={(e) => setLot(e.target.value)} maxLength={60} /></div>}
            {item?.isLotTracked && isIncrease && <div className="form-field"><label>{x.expiry}</label><input type="date" value={expiry} onChange={(e) => setExpiry(e.target.value)} /></div>}
          </div>
          <div className="form-field"><label>{x.notes}</label><input value={notes} onChange={(e) => setNotes(e.target.value)} maxLength={500} /></div>
          <button className="btn" type="submit" disabled={busy}>{x.save}</button>
        </form>
      )}

      {rows.length === 0 ? <div className="card text-muted">{x.empty}</div> : (
        <div className="card" style={{ overflowX: "auto" }}>
          <table className="table">
            <thead><tr><th>{x.number}</th><th>{x.date}</th><th>{x.branch}</th><th>{x.item}</th><th>{x.qty}</th><th>{x.reason}</th><th>{x.total}</th><th>{x.by}</th></tr></thead>
            <tbody>
              {rows.map((r) => (
                <tr key={r.id}>
                  <td>{r.number}</td><td>{r.adjustmentDate.slice(0, 10)}</td><td>{r.warehouseName}</td><td>{r.itemCode} — {r.itemName}</td>
                  <td style={{ color: r.isIncrease ? "var(--success, #1a7f4b)" : "var(--danger, #b42318)" }}>{r.isIncrease ? "+" : "−"}{qtyFmt(r.quantity)}</td>
                  <td>{reasonLabel(r.reason)}{r.notes ? ` — ${r.notes}` : ""}</td><td>{money(r.totalCost)}</td><td>{r.createdByName}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
