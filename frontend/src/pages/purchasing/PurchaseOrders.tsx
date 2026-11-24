import { useEffect, useState } from "react";
import { PurchaseOrdersApi, PurchasingApi, AccountsApi, LookupsApi, type PurchaseOrder, type TradeLineInput } from "../../api/services";
import type { Account, FiscalPeriod, Vendor } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";

const TEXT = {
  ar: {
    title: "أوامر الشراء", add: "أمر شراء جديد", cancel: "إلغاء", save: "حفظ", vendor: "المورد", choose: "اختار...", date: "التاريخ", expected: "التوريد المتوقع", notes: "ملاحظات",
    lines: "البنود", desc: "الوصف", qty: "الكمية", price: "السعر", vat: "الضريبة %", addLine: "إضافة بند", total: "الإجمالي", net: "قبل الضريبة", tax: "الضريبة",
    number: "الرقم", status: "الحالة", empty: "مفيش أوامر لسه.", edit: "تعديل", approve: "اعتمد", cancelOrder: "ألغِ الأمر", toInvoice: "سجّل فاتورة المورد", print: "طباعة",
    s: { 1: "مسودة", 2: "معتمد", 3: "اتحوّل لفاتورة", 4: "ملغي" } as Record<number, string>, approvedBy: "اعتمده",
    convertTitle: "تسجيل فاتورة المورد", invoiceDate: "تاريخ الفاتورة", period: "الفترة المحاسبية", payment: "طريقة الدفع", cash: "نقدي", card: "كارت", credit: "آجل", account: "حساب البنود", run: "سجّل", note: "الفاتورة بتتسجل في الدفاتر بالطريقة العادية. اختار حساب المصروف أو المخزون للبنود.",
    hint: "الأمر مش بيتسجل في الدفاتر. الدفاتر بتشوفه بس لما تسجّل فاتورة المورد منه بعد الاعتماد.", removeLine: "حذف البند",
  },
  en: {
    title: "Purchase orders", add: "New purchase order", cancel: "Cancel", save: "Save", vendor: "Supplier", choose: "Choose...", date: "Date", expected: "Expected delivery", notes: "Notes",
    lines: "Lines", desc: "Description", qty: "Qty", price: "Price", vat: "Tax %", addLine: "Add line", total: "Total", net: "Before tax", tax: "Tax",
    number: "No.", status: "Status", empty: "No orders yet.", edit: "Edit", approve: "Approve", cancelOrder: "Cancel order", toInvoice: "Book the supplier invoice", print: "Print",
    s: { 1: "Draft", 2: "Approved", 3: "Invoiced", 4: "Cancelled" } as Record<number, string>, approvedBy: "Approved by",
    convertTitle: "Book the supplier invoice", invoiceDate: "Invoice date", period: "Fiscal period", payment: "Payment", cash: "Cash", card: "Card", credit: "On account", account: "Account for the lines", run: "Book", note: "The invoice is booked in the ledger the normal way. Choose the expense or stock account for the lines.",
    hint: "An order is not booked in the ledger. The ledger only sees it when you book the supplier's invoice from it after approval.", removeLine: "Remove line",
  },
};

const today = () => new Date().toISOString().slice(0, 10);
const emptyLine = (): TradeLineInput => ({ description: "", quantity: 1, unitPrice: 0, vatRate: null });
const money = (n: number) => n.toLocaleString(undefined, { maximumFractionDigits: 2 });
const esc = (s: string) => s.replace(/[&<>"]/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" })[c]!);

export default function PurchaseOrders() {
  const { lang } = useLanguage();
  const x = TEXT[lang === "ar" ? "ar" : "en"];
  const [list, setList] = useState<PurchaseOrder[]>([]);
  const [vendors, setVendors] = useState<Vendor[]>([]);
  const [accounts, setAccounts] = useState<Account[]>([]);
  const [periods, setPeriods] = useState<FiscalPeriod[]>([]);
  const [vatRate, setVatRate] = useState(0);
  const [error, setError] = useState<string | null>(null);
  const [editing, setEditing] = useState<string | "new" | null>(null);
  const [vendorId, setVendorId] = useState("");
  const [date, setDate] = useState(today());
  const [expected, setExpected] = useState("");
  const [notes, setNotes] = useState("");
  const [lines, setLines] = useState<TradeLineInput[]>([emptyLine()]);
  const [converting, setConverting] = useState<PurchaseOrder | null>(null);
  const [cv, setCv] = useState({ invoiceDate: today(), fiscalPeriodId: "", paymentTerm: 3, defaultAccountId: "" });

  const load = async () => {
    const [o, v, a, p, s] = await Promise.all([PurchaseOrdersApi.getAll(), PurchasingApi.getVendors(), AccountsApi.getAll(), LookupsApi.fiscalPeriods(), LookupsApi.companySettings()]);
    setList(o.data); setVendors(v.data); setAccounts(a.data.filter((k) => k.isActive && !k.isControlAccount)); setPeriods(p.data.filter((f) => !f.isClosed)); setVatRate(s.data.vatRate);
  };
  useEffect(() => { load().catch((e) => setError(getErrorMessage(e))); }, []);

  const rate = (l: TradeLineInput) => l.vatRate ?? vatRate;
  const net = lines.reduce((s, l) => s + l.quantity * l.unitPrice, 0);
  const vat = lines.reduce((s, l) => s + l.quantity * l.unitPrice * rate(l), 0);
  const setLine = (i: number, patch: Partial<TradeLineInput>) => setLines((p) => p.map((l, k) => (k === i ? { ...l, ...patch } : l)));

  function open(o?: PurchaseOrder) {
    setError(null);
    if (o) { setEditing(o.id); setVendorId(o.vendorId); setDate(o.orderDate.slice(0, 10)); setExpected(o.expectedDate?.slice(0, 10) ?? ""); setNotes(o.notes ?? ""); setLines(o.lines.map((l) => ({ description: l.description, quantity: l.quantity, unitPrice: l.unitPrice, vatRate: l.vatRate, accountId: l.accountId }))); }
    else { setEditing("new"); setVendorId(""); setDate(today()); setExpected(""); setNotes(""); setLines([emptyLine()]); }
  }
  async function save(e: React.FormEvent) {
    e.preventDefault(); setError(null);
    const body = { vendorId, orderDate: date, expectedDate: expected || null, notes: notes || null, lines };
    try { if (editing === "new") await PurchaseOrdersApi.create(body); else await PurchaseOrdersApi.update(editing!, body); setEditing(null); await load(); } catch (err) { setError(getErrorMessage(err)); }
  }
  async function act(fn: () => Promise<unknown>) { setError(null); try { await fn(); await load(); } catch (err) { setError(getErrorMessage(err)); } }
  async function convert() {
    if (!converting) return; setError(null);
    try { await PurchaseOrdersApi.convert(converting.id, { ...cv, defaultAccountId: cv.defaultAccountId || null }); setConverting(null); await load(); } catch (err) { setError(getErrorMessage(err)); }
  }
  function print(o: PurchaseOrder) {
    const w = window.open("", "_blank"); if (!w) return;
    const rows = o.lines.map((l) => `<tr><td>${l.lineNumber}</td><td>${esc(l.description)}</td><td>${l.quantity}</td><td>${money(l.unitPrice)}</td><td>${money(l.lineTotal)}</td></tr>`).join("");
    w.document.write(`<html dir="${lang === "ar" ? "rtl" : "ltr"}"><head><meta charset="utf-8"><title>${o.orderNumber}</title><style>body{font-family:sans-serif;padding:24px}table{width:100%;border-collapse:collapse}td,th{border:1px solid #999;padding:6px;text-align:start}</style></head><body><h2>${x.title} ${o.orderNumber}</h2><p>${x.vendor}: ${esc(o.vendorName)}<br>${x.date}: ${o.orderDate.slice(0, 10)}${o.expectedDate ? `<br>${x.expected}: ${o.expectedDate.slice(0, 10)}` : ""}</p><table><tr><th>#</th><th>${x.desc}</th><th>${x.qty}</th><th>${x.price}</th><th>${x.total}</th></tr>${rows}</table><p>${x.net}: ${money(o.subTotal)}<br>${x.tax}: ${money(o.vatAmount)}<br><b>${x.total}: ${money(o.totalAmount)}</b></p>${o.notes ? `<p>${esc(o.notes)}</p>` : ""}</body></html>`);
    w.document.close(); w.print();
  }

  return (
    <div>
      <div className="page-header"><h1>{x.title}</h1><button className="btn" onClick={() => (editing ? setEditing(null) : open())}>{editing ? x.cancel : x.add}</button></div>
      <p className="text-muted">{x.hint}</p>
      {error && <div className="alert-error">{error}</div>}

      {editing && (
        <form className="card" onSubmit={save} style={{ maxWidth: 820, marginBottom: 16 }}>
          <div className="form-grid">
            <div className="form-field"><label>{x.vendor}</label>
              <select required value={vendorId} onChange={(e) => setVendorId(e.target.value)}><option value="">{x.choose}</option>{vendors.map((v) => <option key={v.id} value={v.id}>{v.nameAr}</option>)}</select></div>
            <div className="form-field"><label>{x.date}</label><input type="date" required value={date} onChange={(e) => setDate(e.target.value)} /></div>
            <div className="form-field"><label>{x.expected}</label><input type="date" value={expected} onChange={(e) => setExpected(e.target.value)} /></div>
          </div>
          <label>{x.lines}</label>
          {lines.map((l, i) => (
            <div key={i} style={{ display: "grid", gridTemplateColumns: "3fr 1fr 1fr 1fr auto", gap: 8, marginBottom: 6 }}>
              <input required value={l.description} placeholder={x.desc} onChange={(e) => setLine(i, { description: e.target.value })} />
              <input type="number" min={0} step="any" value={l.quantity} placeholder={x.qty} onChange={(e) => setLine(i, { quantity: Number(e.target.value) })} />
              <input type="number" min={0} step="any" value={l.unitPrice} placeholder={x.price} onChange={(e) => setLine(i, { unitPrice: Number(e.target.value) })} />
              <input type="number" min={0} max={100} step="any" value={l.vatRate === null ? vatRate * 100 : l.vatRate * 100} placeholder={x.vat} onChange={(e) => setLine(i, { vatRate: Number(e.target.value) / 100 })} />
              <button type="button" className="btn btn-secondary btn-sm" disabled={lines.length === 1} title={x.removeLine} onClick={() => setLines((p) => p.filter((_, k) => k !== i))}>×</button>
            </div>
          ))}
          <button type="button" className="btn btn-secondary btn-sm" onClick={() => setLines((p) => [...p, emptyLine()])}>{x.addLine}</button>
          <div className="form-field" style={{ marginTop: 10 }}><label>{x.notes}</label><input maxLength={1000} value={notes} onChange={(e) => setNotes(e.target.value)} /></div>
          <div>{x.net}: {money(net)} — {x.tax}: {money(vat)} — <strong>{x.total}: {money(net + vat)}</strong></div>
          <button className="btn" style={{ marginTop: 10 }}>{x.save}</button>
        </form>
      )}

      {converting && (
        <div className="card" style={{ maxWidth: 560, marginBottom: 16 }}>
          <strong>{x.convertTitle}: {converting.orderNumber}</strong>
          <div className="text-muted">{x.note}</div>
          <div className="form-grid" style={{ marginTop: 8 }}>
            <div className="form-field"><label>{x.invoiceDate}</label><input type="date" value={cv.invoiceDate} onChange={(e) => setCv({ ...cv, invoiceDate: e.target.value })} /></div>
            <div className="form-field"><label>{x.period}</label>
              <select value={cv.fiscalPeriodId} onChange={(e) => setCv({ ...cv, fiscalPeriodId: e.target.value })}><option value="">{x.choose}</option>{periods.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}</select></div>
            <div className="form-field"><label>{x.payment}</label>
              <select value={cv.paymentTerm} onChange={(e) => setCv({ ...cv, paymentTerm: Number(e.target.value) })}><option value={1}>{x.cash}</option><option value={2}>{x.card}</option><option value={3}>{x.credit}</option></select></div>
            <div className="form-field"><label>{x.account}</label>
              <select value={cv.defaultAccountId} onChange={(e) => setCv({ ...cv, defaultAccountId: e.target.value })}><option value="">{x.choose}</option>{accounts.map((a) => <option key={a.id} value={a.id}>{a.code} {a.nameAr}</option>)}</select></div>
          </div>
          <div style={{ display: "flex", gap: 8, marginTop: 8 }}>
            <button className="btn" disabled={!cv.fiscalPeriodId} onClick={convert}>{x.run}</button>
            <button className="btn btn-secondary" onClick={() => setConverting(null)}>{x.cancel}</button>
          </div>
        </div>
      )}

      <div className="card" style={{ overflowX: "auto" }}>
        {list.length === 0 ? <p className="text-muted">{x.empty}</p> : (
          <table>
            <thead><tr><th>{x.number}</th><th>{x.vendor}</th><th>{x.date}</th><th>{x.total}</th><th>{x.status}</th><th /></tr></thead>
            <tbody>
              {list.map((o) => (
                <tr key={o.id}>
                  <td>{o.orderNumber}</td><td>{o.vendorName}</td><td>{o.orderDate.slice(0, 10)}</td><td>{money(o.totalAmount)}</td>
                  <td>{x.s[o.status]}{o.approvedBy && o.status === 2 ? ` — ${x.approvedBy}: ${o.approvedBy}` : ""}</td>
                  <td style={{ display: "flex", gap: 6, flexWrap: "wrap" }}>
                    {o.status === 1 && <button className="btn btn-secondary btn-sm" onClick={() => open(o)}>{x.edit}</button>}
                    {o.status === 1 && <button className="btn btn-secondary btn-sm" onClick={() => act(() => PurchaseOrdersApi.approve(o.id))}>{x.approve}</button>}
                    {o.status === 2 && <button className="btn btn-sm" onClick={() => { setConverting(o); setCv({ invoiceDate: today(), fiscalPeriodId: "", paymentTerm: 3, defaultAccountId: "" }); }}>{x.toInvoice}</button>}
                    {(o.status === 1 || o.status === 2) && <button className="btn btn-secondary btn-sm" onClick={() => act(() => PurchaseOrdersApi.cancel(o.id))}>{x.cancelOrder}</button>}
                    <button className="btn btn-secondary btn-sm" onClick={() => print(o)}>{x.print}</button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}
