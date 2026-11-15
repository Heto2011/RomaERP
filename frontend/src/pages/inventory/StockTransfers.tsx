import { Fragment, useEffect, useMemo, useState } from "react";
import { ItemsApi, StockTransfersApi, WarehousesApi } from "../../api/services";
import type { Item, StockTransfer, Warehouse, WarehouseStockRow } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";

const TEXT = {
  ar: {
    title: "تحويل بين الفروع", intro: "فرع يبعت (Out) وفرع يستلم (In). التحويل بينقل الكمية من فرع لفرع بنفس التكلفة، ومبيأثرش على الحسابات ولا الأرباح.",
    newTransfer: "تحويل جديد", from: "الفرع المرسل (Out)", to: "الفرع المستلم (In)", date: "التاريخ", notes: "ملاحظات", choose: "اختار",
    item: "الصنف", qty: "الكمية", lot: "رقم الدُفعة", available: "المتاح في الفرع المرسل", addLine: "إضافة صنف", removeLine: "حذف",
    send: "إرسال (Out)", receive: "استلام (In)", cancel: "إلغاء التحويل", cancelConfirm: "تلغي التحويل؟ الكمية هترجع للفرع المرسل.",
    receiveNote: "ملاحظة على الاستلام (اختياري):", inTransit: "في الطريق", received: "تم الاستلام", cancelled: "ملغي", all: "الكل",
    number: "الرقم", fromTo: "من ← إلى", items: "الأصناف", status: "الحالة", value: "القيمة", sent: "أرسله", receivedBy: "استلمه",
    empty: "مفيش تحويلات.", sentOk: "اتبعت. الفرع التاني يضغط «استلام (In)» لما البضاعة توصل.", done: "تم.",
    details: "التفاصيل", hide: "إخفاء", sameBranch: "الفرعين لازم يكونوا مختلفين.",
  },
  en: {
    title: "Branch transfers", intro: "One branch sends (Out), the other receives (In). A transfer moves quantity from one branch to another at the same cost, and never touches the accounts or the profit.",
    newTransfer: "New transfer", from: "Sending branch (Out)", to: "Receiving branch (In)", date: "Date", notes: "Notes", choose: "Choose",
    item: "Item", qty: "Quantity", lot: "Lot number", available: "Available at the sending branch", addLine: "Add item", removeLine: "Remove",
    send: "Send (Out)", receive: "Receive (In)", cancel: "Cancel transfer", cancelConfirm: "Cancel the transfer? The quantity goes back to the sending branch.",
    receiveNote: "Note on receipt (optional):", inTransit: "In transit", received: "Received", cancelled: "Cancelled", all: "All",
    number: "Number", fromTo: "From → To", items: "Items", status: "Status", value: "Value", sent: "Sent by", receivedBy: "Received by",
    empty: "No transfers.", sentOk: "Sent. The other branch presses “Receive (In)” when the goods arrive.", done: "Done.",
    details: "Details", hide: "Hide", sameBranch: "The two branches must be different.",
  },
};

const FILTERS = ["InTransit", "Received", "Cancelled", ""] as const;
const money = (n: number) => n.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const qtyFmt = (n: number) => n.toLocaleString(undefined, { maximumFractionDigits: 4 });
type Line = { itemId: string; quantity: string; lotNumber: string };

export default function StockTransfers() {
  const { lang } = useLanguage();
  const x = TEXT[lang === "ar" ? "ar" : "en"];
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [items, setItems] = useState<Item[]>([]);
  const [rows, setRows] = useState<StockTransfer[]>([]);
  const [filter, setFilter] = useState<(typeof FILTERS)[number]>("InTransit");
  const [creating, setCreating] = useState(false);
  const [fromId, setFromId] = useState("");
  const [toId, setToId] = useState("");
  const [date, setDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [notes, setNotes] = useState("");
  const [lines, setLines] = useState<Line[]>([{ itemId: "", quantity: "", lotNumber: "" }]);
  const [stock, setStock] = useState<WarehouseStockRow[]>([]);
  const [open, setOpen] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const wName = (w: Warehouse) => (lang === "ar" ? w.nameAr : w.nameEn || w.nameAr);
  const itemName = (i: Item) => `${i.code} — ${lang === "ar" ? i.nameAr : i.nameEn || i.nameAr}`;
  const itemById = useMemo(() => new Map(items.map((i) => [i.id, i])), [items]);
  const availableAtFrom = (itemId: string) => stock.find((r) => r.itemId === itemId)?.quantity ?? 0;

  async function loadList(f = filter) { try { setRows((await StockTransfersApi.getAll(f || undefined)).data); } catch (e) { setError(getErrorMessage(e)); } }
  useEffect(() => {
    (async () => {
      try {
        const [w, i] = await Promise.all([WarehousesApi.getAll(), ItemsApi.getAll()]);
        setWarehouses(w.data.filter((a) => a.isActive)); setItems(i.data);
      } catch (e) { setError(getErrorMessage(e)); }
    })();
  }, []);
  useEffect(() => { loadList(filter); /* eslint-disable-next-line react-hooks/exhaustive-deps */ }, [filter]);
  useEffect(() => {
    if (!fromId) { setStock([]); return; }
    StockTransfersApi.warehouseStock(fromId).then((r) => setStock(r.data)).catch(() => setStock([]));
  }, [fromId]);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setError(null); setMessage(null);
    if (fromId === toId) { setError(x.sameBranch); return; }
    setBusy(true);
    try {
      await StockTransfersApi.send({
        fromWarehouseId: fromId, toWarehouseId: toId, transferDate: date, notes: notes || undefined,
        lines: lines.filter((l) => l.itemId && Number(l.quantity) > 0).map((l) => ({ itemId: l.itemId, quantity: Number(l.quantity), lotNumber: l.lotNumber || undefined })),
      });
      setMessage(x.sentOk); setCreating(false); setLines([{ itemId: "", quantity: "", lotNumber: "" }]); setNotes("");
      setFilter("InTransit"); await loadList("InTransit");
    } catch (err) { setError(getErrorMessage(err)); } finally { setBusy(false); }
  }

  async function act(fn: () => Promise<unknown>) {
    setError(null); setMessage(null);
    try { await fn(); setMessage(x.done); await loadList(); } catch (err) { setError(getErrorMessage(err)); }
  }
  const statusLabel = (s: string) => ({ InTransit: x.inTransit, Received: x.received, Cancelled: x.cancelled } as Record<string, string>)[s] ?? s;

  return (
    <div style={{ maxWidth: 1100 }}>
      <div className="page-header">
        <h1>{x.title}</h1>
        <button className="btn btn-sm" onClick={() => setCreating(!creating)}>{x.newTransfer}</button>
      </div>
      <p className="text-muted">{x.intro}</p>
      {error && <div className="alert-error">{error}</div>}
      {message && <div className="card" style={{ marginBottom: 12 }}>{message}</div>}

      {creating && (
        <form className="card" style={{ marginBottom: 16 }} onSubmit={submit}>
          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", gap: 12 }}>
            <div className="form-field"><label>{x.from}</label>
              <select value={fromId} onChange={(e) => setFromId(e.target.value)} required>
                <option value="">{x.choose}</option>{warehouses.map((w) => <option key={w.id} value={w.id}>{wName(w)}</option>)}
              </select></div>
            <div className="form-field"><label>{x.to}</label>
              <select value={toId} onChange={(e) => setToId(e.target.value)} required>
                <option value="">{x.choose}</option>{warehouses.filter((w) => w.id !== fromId).map((w) => <option key={w.id} value={w.id}>{wName(w)}</option>)}
              </select></div>
            <div className="form-field"><label>{x.date}</label><input type="date" value={date} onChange={(e) => setDate(e.target.value)} required /></div>
          </div>
          <table style={{ marginTop: 12 }}>
            <thead><tr><th>{x.item}</th><th>{x.qty}</th><th>{x.lot}</th><th>{x.available}</th><th></th></tr></thead>
            <tbody>
              {lines.map((l, idx) => {
                const item = itemById.get(l.itemId);
                const set = (patch: Partial<Line>) => setLines(lines.map((r, i) => (i === idx ? { ...r, ...patch } : r)));
                return (
                  <tr key={idx}>
                    <td><select value={l.itemId} onChange={(e) => set({ itemId: e.target.value })} required>
                      <option value="">{x.choose}</option>{items.map((i) => <option key={i.id} value={i.id}>{itemName(i)}</option>)}</select></td>
                    <td><input type="number" min="0" step="0.0001" value={l.quantity} onChange={(e) => set({ quantity: e.target.value })} style={{ width: 110 }} required /></td>
                    <td>{item?.isLotTracked ? <input value={l.lotNumber} onChange={(e) => set({ lotNumber: e.target.value })} style={{ width: 120 }} required /> : "—"}</td>
                    <td>{l.itemId && fromId ? `${qtyFmt(availableAtFrom(l.itemId))} ${item?.unitOfMeasure ?? ""}` : "—"}</td>
                    <td>{lines.length > 1 && <button type="button" className="btn btn-secondary btn-sm" onClick={() => setLines(lines.filter((_, i) => i !== idx))}>{x.removeLine}</button>}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
          <button type="button" className="btn btn-secondary btn-sm" style={{ margin: "8px 0" }} onClick={() => setLines([...lines, { itemId: "", quantity: "", lotNumber: "" }])}>{x.addLine}</button>
          <div className="form-field"><label>{x.notes}</label><input value={notes} onChange={(e) => setNotes(e.target.value)} maxLength={500} /></div>
          <button className="btn" type="submit" disabled={busy}>{x.send}</button>
        </form>
      )}

      <div style={{ display: "flex", gap: 6, flexWrap: "wrap", marginBottom: 12 }}>
        {FILTERS.map((f) => <button key={f || "all"} className={filter === f ? "btn btn-sm" : "btn btn-secondary btn-sm"} onClick={() => setFilter(f)}>{f ? statusLabel(f) : x.all}</button>)}
      </div>

      {rows.length === 0 ? <div className="card text-muted">{x.empty}</div> : (
        <div className="card" style={{ overflowX: "auto" }}>
          <table>
            <thead><tr><th>{x.number}</th><th>{x.date}</th><th>{x.fromTo}</th><th>{x.items}</th><th>{x.value}</th><th>{x.status}</th><th></th></tr></thead>
            <tbody>
              {rows.map((t) => (
                <Fragment key={t.id}>
                  <tr>
                    <td><strong>{t.number}</strong></td>
                    <td>{new Date(t.transferDate).toLocaleDateString()}</td>
                    <td>{t.fromWarehouseName} ← {t.toWarehouseName}</td>
                    <td>{t.lines.length}</td>
                    <td>{money(t.totalCost)}</td>
                    <td><span className="badge">{statusLabel(t.status)}</span></td>
                    <td style={{ whiteSpace: "nowrap" }}>
                      <button className="btn btn-secondary btn-sm" onClick={() => setOpen(open === t.id ? null : t.id)}>{open === t.id ? x.hide : x.details}</button>{" "}
                      {t.status === "InTransit" && (<>
                        <button className="btn btn-sm" onClick={() => { const n = window.prompt(x.receiveNote); if (n !== null) act(() => StockTransfersApi.receive(t.id, n)); }}>{x.receive}</button>{" "}
                        <button className="btn btn-secondary btn-sm" onClick={() => { if (window.confirm(x.cancelConfirm)) act(() => StockTransfersApi.cancel(t.id)); }}>{x.cancel}</button>
                      </>)}
                    </td>
                  </tr>
                  {open === t.id && (
                    <tr><td colSpan={7}>
                      <table>
                        <tbody>{t.lines.map((l) => (
                          <tr key={l.itemId + (l.lotNumber ?? "")}><td>{l.itemCode} — {l.itemName}{l.lotNumber ? ` (${l.lotNumber})` : ""}</td><td>{qtyFmt(l.quantity)}</td><td className="text-muted">{money(l.unitCost)}</td></tr>
                        ))}</tbody>
                      </table>
                      <div className="text-muted" style={{ fontSize: 12, marginTop: 6 }}>
                        {x.sent}: {t.sentBy ?? "—"}{t.receivedBy ? ` · ${x.receivedBy}: ${t.receivedBy}` : ""}{t.notes ? ` · ${t.notes}` : ""}{t.receiveNote ? ` · ${t.receiveNote}` : ""}
                      </div>
                    </td></tr>
                  )}
                </Fragment>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
