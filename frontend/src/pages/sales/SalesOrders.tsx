import { useEffect, useState } from "react";
import { SalesOrdersApi, SalesApi, LookupsApi, type SalesOrder, type TradeLineInput } from "../../api/services";
import type { Customer, FiscalPeriod } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";

const TEXT = {
  ar: {
    title: "أوامر البيع وإذن التسليم", add: "أمر بيع جديد", cancel: "إلغاء", save: "حفظ", customer: "العميل", choose: "اختار...", date: "التاريخ", delivery: "تاريخ التسليم", address: "عنوان التسليم", notes: "ملاحظات",
    lines: "البنود", desc: "الوصف", qty: "الكمية", price: "السعر", vat: "الضريبة %", addLine: "إضافة بند", removeLine: "حذف البند", total: "الإجمالي", net: "قبل الضريبة", tax: "الضريبة",
    number: "الرقم", status: "الحالة", empty: "مفيش أوامر بيع لسه.", edit: "تعديل", confirm: "أكّد الأمر", delivered: "اتسلّم", cancelOrder: "ألغِ الأمر", toInvoice: "حوّله لفاتورة", printOrder: "طباعة الأمر", printNote: "طباعة إذن التسليم",
    s: { 1: "مسودة", 2: "مؤكد", 3: "اتسلّم", 4: "اتحوّل لفاتورة", 5: "ملغي" } as Record<number, string>,
    convertTitle: "تحويل لفاتورة مبيعات", invoiceDate: "تاريخ الفاتورة", period: "الفترة المحاسبية", payment: "طريقة الدفع", cash: "نقدي", card: "كارت", credit: "آجل", run: "حوّل",
    hint: "أمر البيع مش بيتسجل في الدفاتر ومش بيخصم من المخزون. الدفاتر والمخزون بيتحركوا لما يتحوّل لفاتورة. إذن التسليم ورقة بتطبعها وترافق البضاعة (من غير أسعار).",
    noteTitle: "إذن تسليم", recv: "توقيع المستلم", sender: "المسلّم", where: "مكان التسليم", phone: "تليفون",
  },
  en: {
    title: "Sales orders and delivery notes", add: "New sales order", cancel: "Cancel", save: "Save", customer: "Customer", choose: "Choose...", date: "Date", delivery: "Delivery date", address: "Delivery address", notes: "Notes",
    lines: "Lines", desc: "Description", qty: "Qty", price: "Price", vat: "Tax %", addLine: "Add line", removeLine: "Remove line", total: "Total", net: "Before tax", tax: "Tax",
    number: "No.", status: "Status", empty: "No sales orders yet.", edit: "Edit", confirm: "Confirm the order", delivered: "Delivered", cancelOrder: "Cancel the order", toInvoice: "Make an invoice", printOrder: "Print the order", printNote: "Print the delivery note",
    s: { 1: "Draft", 2: "Confirmed", 3: "Delivered", 4: "Invoiced", 5: "Cancelled" } as Record<number, string>,
    convertTitle: "Make a sales invoice", invoiceDate: "Invoice date", period: "Fiscal period", payment: "Payment", cash: "Cash", card: "Card", credit: "On account", run: "Convert",
    hint: "A sales order is not booked in the ledger and does not take stock out. The ledger and the stock move when it becomes an invoice. The delivery note is a sheet you print to go with the goods (no prices).",
    noteTitle: "Delivery note", recv: "Receiver's signature", sender: "Delivered by", where: "Deliver to", phone: "Phone",
  },
};
const today = () => new Date().toISOString().slice(0, 10);
const blank = (): TradeLineInput => ({ description: "", quantity: 1, unitPrice: 0, vatRate: null });
const money = (n: number) => n.toLocaleString(undefined, { maximumFractionDigits: 2 });
const esc = (s: string) => s.replace(/[&<>"]/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" })[c]!);

export default function SalesOrders() {
  const { lang } = useLanguage();
  const x = TEXT[lang === "ar" ? "ar" : "en"];
  const [list, setList] = useState<SalesOrder[]>([]);
  const [customers, setCustomers] = useState<Customer[]>([]);
  const [periods, setPeriods] = useState<FiscalPeriod[]>([]);
  const [vatRate, setVatRate] = useState(0);
  const [error, setError] = useState<string | null>(null);
  const [editing, setEditing] = useState<string | "new" | null>(null);
  const [customerId, setCustomerId] = useState("");
  const [date, setDate] = useState(today());
  const [deliveryDate, setDeliveryDate] = useState("");
  const [address, setAddress] = useState("");
  const [notes, setNotes] = useState("");
  const [lines, setLines] = useState<TradeLineInput[]>([blank()]);
  const [converting, setConverting] = useState<SalesOrder | null>(null);
  const [cv, setCv] = useState({ invoiceDate: today(), fiscalPeriodId: "", paymentTerm: 3 });

  const load = async () => {
    const [o, c, p, s] = await Promise.all([SalesOrdersApi.getAll(), SalesApi.getCustomers(), LookupsApi.fiscalPeriods(), LookupsApi.companySettings()]);
    setList(o.data); setCustomers(c.data); setPeriods(p.data.filter((f) => !f.isClosed)); setVatRate(s.data.vatRate);
  };
  useEffect(() => { load().catch((e) => setError(getErrorMessage(e))); }, []);

  const rate = (l: TradeLineInput) => l.vatRate ?? vatRate;
  const net = lines.reduce((s, l) => s + l.quantity * l.unitPrice, 0);
  const vat = lines.reduce((s, l) => s + l.quantity * l.unitPrice * rate(l), 0);
  const setLine = (i: number, patch: Partial<TradeLineInput>) => setLines((p) => p.map((l, k) => (k === i ? { ...l, ...patch } : l)));

  function open(o?: SalesOrder) {
    setError(null);
    if (o) { setEditing(o.id); setCustomerId(o.customerId); setDate(o.orderDate.slice(0, 10)); setDeliveryDate(o.deliveryDate?.slice(0, 10) ?? ""); setAddress(o.deliveryAddress ?? ""); setNotes(o.notes ?? ""); setLines(o.lines.map((l) => ({ description: l.description, quantity: l.quantity, unitPrice: l.unitPrice, vatRate: l.vatRate }))); }
    else { setEditing("new"); setCustomerId(""); setDate(today()); setDeliveryDate(""); setAddress(""); setNotes(""); setLines([blank()]); }
  }
  async function save(e: React.FormEvent) {
    e.preventDefault(); setError(null);
    const body = { customerId, orderDate: date, deliveryDate: deliveryDate || null, deliveryAddress: address || null, notes: notes || null, lines };
    try { if (editing === "new") await SalesOrdersApi.create(body); else await SalesOrdersApi.update(editing!, body); setEditing(null); await load(); } catch (err) { setError(getErrorMessage(err)); }
  }
  async function act(fn: () => Promise<unknown>) { setError(null); try { await fn(); await load(); } catch (err) { setError(getErrorMessage(err)); } }
  async function convert() {
    if (!converting) return; setError(null);
    try { await SalesOrdersApi.convert(converting.id, cv); setConverting(null); await load(); } catch (err) { setError(getErrorMessage(err)); }
  }
  function printDoc(o: SalesOrder, withPrices: boolean) {
    const w = window.open("", "_blank"); if (!w) return;
    const rows = o.lines.map((l) => `<tr><td>${l.lineNumber}</td><td>${esc(l.description)}</td><td>${l.quantity}</td>${withPrices ? `<td>${money(l.unitPrice)}</td><td>${money(l.lineTotal)}</td>` : ""}</tr>`).join("");
    const head = withPrices ? `<th>${x.price}</th><th>${x.total}</th>` : "";
    const foot = withPrices ? `<p>${x.net}: ${money(o.subTotal)}<br>${x.tax}: ${money(o.vatAmount)}<br><b>${x.total}: ${money(o.totalAmount)}</b></p>` : `<p style="margin-top:60px">${x.recv}: ____________________ &nbsp;&nbsp; ${x.sender}: ____________________</p>`;
    w.document.write(`<html dir="${lang === "ar" ? "rtl" : "ltr"}"><head><meta charset="utf-8"><title>${o.orderNumber}</title><style>body{font-family:sans-serif;padding:24px}table{width:100%;border-collapse:collapse}td,th{border:1px solid #999;padding:6px;text-align:start}</style></head><body><h2>${withPrices ? x.title.split(" ")[0] + " " + x.title.split(" ")[1] : x.noteTitle} ${o.orderNumber}</h2><p>${x.customer}: ${esc(o.customerName)}${o.customerPhone ? `<br>${x.phone}: ${esc(o.customerPhone)}` : ""}<br>${x.date}: ${o.orderDate.slice(0, 10)}${o.deliveryDate ? `<br>${x.delivery}: ${o.deliveryDate.slice(0, 10)}` : ""}${o.deliveryAddress ? `<br>${x.where}: ${esc(o.deliveryAddress)}` : ""}</p><table><tr><th>#</th><th>${x.desc}</th><th>${x.qty}</th>${head}</tr>${rows}</table>${foot}${o.notes ? `<p>${esc(o.notes)}</p>` : ""}</body></html>`);
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
            <div className="form-field"><label>{x.customer}</label>
              <select required value={customerId} onChange={(e) => setCustomerId(e.target.value)}><option value="">{x.choose}</option>{customers.map((c) => <option key={c.id} value={c.id}>{c.nameAr}</option>)}</select></div>
            <div className="form-field"><label>{x.date}</label><input type="date" required value={date} onChange={(e) => setDate(e.target.value)} /></div>
            <div className="form-field"><label>{x.delivery}</label><input type="date" value={deliveryDate} onChange={(e) => setDeliveryDate(e.target.value)} /></div>
          </div>
          <div className="form-field"><label>{x.address}</label><input maxLength={300} value={address} onChange={(e) => setAddress(e.target.value)} /></div>
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
          <button type="button" className="btn btn-secondary btn-sm" onClick={() => setLines((p) => [...p, blank()])}>{x.addLine}</button>
          <div className="form-field" style={{ marginTop: 10 }}><label>{x.notes}</label><input maxLength={1000} value={notes} onChange={(e) => setNotes(e.target.value)} /></div>
          <div>{x.net}: {money(net)} — {x.tax}: {money(vat)} — <strong>{x.total}: {money(net + vat)}</strong></div>
          <button className="btn" style={{ marginTop: 10 }}>{x.save}</button>
        </form>
      )}

      {converting && (
        <div className="card" style={{ maxWidth: 520, marginBottom: 16 }}>
          <strong>{x.convertTitle}: {converting.orderNumber}</strong>
          <div className="form-grid" style={{ marginTop: 8 }}>
            <div className="form-field"><label>{x.invoiceDate}</label><input type="date" value={cv.invoiceDate} onChange={(e) => setCv({ ...cv, invoiceDate: e.target.value })} /></div>
            <div className="form-field"><label>{x.period}</label>
              <select value={cv.fiscalPeriodId} onChange={(e) => setCv({ ...cv, fiscalPeriodId: e.target.value })}><option value="">{x.choose}</option>{periods.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}</select></div>
            <div className="form-field"><label>{x.payment}</label>
              <select value={cv.paymentTerm} onChange={(e) => setCv({ ...cv, paymentTerm: Number(e.target.value) })}><option value={1}>{x.cash}</option><option value={2}>{x.card}</option><option value={3}>{x.credit}</option></select></div>
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
            <thead><tr><th>{x.number}</th><th>{x.customer}</th><th>{x.date}</th><th>{x.total}</th><th>{x.status}</th><th /></tr></thead>
            <tbody>{list.map((o) => (
              <tr key={o.id}>
                <td>{o.orderNumber}</td><td>{o.customerName}</td><td>{o.orderDate.slice(0, 10)}</td><td>{money(o.totalAmount)}</td><td>{x.s[o.status]}</td>
                <td style={{ display: "flex", gap: 6, flexWrap: "wrap" }}>
                  {o.status === 1 && <button className="btn btn-secondary btn-sm" onClick={() => open(o)}>{x.edit}</button>}
                  {o.status === 1 && <button className="btn btn-secondary btn-sm" onClick={() => act(() => SalesOrdersApi.confirm(o.id))}>{x.confirm}</button>}
                  {o.status === 2 && <button className="btn btn-secondary btn-sm" onClick={() => act(() => SalesOrdersApi.delivered(o.id))}>{x.delivered}</button>}
                  {(o.status === 2 || o.status === 3) && <button className="btn btn-sm" onClick={() => { setConverting(o); setCv({ invoiceDate: today(), fiscalPeriodId: "", paymentTerm: 3 }); }}>{x.toInvoice}</button>}
                  {o.status !== 4 && o.status !== 5 && <button className="btn btn-secondary btn-sm" onClick={() => act(() => SalesOrdersApi.cancel(o.id))}>{x.cancelOrder}</button>}
                  {o.status !== 1 && o.status !== 5 && <button className="btn btn-secondary btn-sm" onClick={() => printDoc(o, false)}>{x.printNote}</button>}
                  <button className="btn btn-secondary btn-sm" onClick={() => printDoc(o, true)}>{x.printOrder}</button>
                </td>
              </tr>
            ))}</tbody>
          </table>
        )}
      </div>
    </div>
  );
}
