import { useEffect, useState } from "react";
import { QuotationsApi, SalesApi, LookupsApi, type Quotation, type TradeLineInput } from "../../api/services";
import type { Customer, FiscalPeriod } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";

const TEXT = {
  ar: {
    title: "عروض الأسعار", add: "عرض سعر جديد", cancel: "إلغاء", save: "حفظ", customer: "العميل", choose: "اختار...", date: "التاريخ", valid: "صالح لحد", notes: "ملاحظات", terms: "الشروط (توريد، دفع...)",
    lines: "البنود", desc: "الوصف", qty: "الكمية", price: "السعر", vat: "الضريبة %", addLine: "إضافة بند", removeLine: "حذف البند", total: "الإجمالي", net: "قبل الضريبة", tax: "الضريبة",
    number: "الرقم", status: "الحالة", empty: "مفيش عروض لسه.", edit: "تعديل", send: "اتبعت للعميل", accept: "العميل وافق", reject: "العميل رفض", reopen: "رجّعه مسودة", toInvoice: "حوّله لفاتورة", print: "طباعة",
    s: { 1: "مسودة", 2: "اتبعت", 3: "اتقبل", 4: "مرفوض", 5: "اتحوّل لفاتورة" } as Record<number, string>, expired: "منتهي الصلاحية",
    convertTitle: "تحويل لفاتورة مبيعات", invoiceDate: "تاريخ الفاتورة", period: "الفترة المحاسبية", payment: "طريقة الدفع", cash: "نقدي", card: "كارت", credit: "آجل", run: "حوّل", note: "الفاتورة بتتسجل في الدفاتر بالطريقة العادية.",
    hint: "العرض مش بيتسجل في الدفاتر. الدفاتر بتشوفه بس لما يتحوّل لفاتورة.",
  },
  en: {
    title: "Quotations", add: "New quotation", cancel: "Cancel", save: "Save", customer: "Customer", choose: "Choose...", date: "Date", valid: "Valid until", notes: "Notes", terms: "Terms (delivery, payment...)",
    lines: "Lines", desc: "Description", qty: "Qty", price: "Price", vat: "Tax %", addLine: "Add line", removeLine: "Remove line", total: "Total", net: "Before tax", tax: "Tax",
    number: "No.", status: "Status", empty: "No quotations yet.", edit: "Edit", send: "Sent to customer", accept: "Customer accepted", reject: "Customer rejected", reopen: "Back to draft", toInvoice: "Make an invoice", print: "Print",
    s: { 1: "Draft", 2: "Sent", 3: "Accepted", 4: "Rejected", 5: "Invoiced" } as Record<number, string>, expired: "Expired",
    convertTitle: "Make a sales invoice", invoiceDate: "Invoice date", period: "Fiscal period", payment: "Payment", cash: "Cash", card: "Card", credit: "On account", run: "Convert", note: "The invoice is booked in the ledger the normal way.",
    hint: "A quotation is not booked in the ledger. The ledger only sees it once it becomes an invoice.",
  },
};

const today = () => new Date().toISOString().slice(0, 10);
const emptyLine = (): TradeLineInput => ({ description: "", quantity: 1, unitPrice: 0, vatRate: null });
const money = (n: number) => n.toLocaleString(undefined, { maximumFractionDigits: 2 });

export default function Quotations() {
  const { lang } = useLanguage();
  const x = TEXT[lang === "ar" ? "ar" : "en"];
  const [list, setList] = useState<Quotation[]>([]);
  const [customers, setCustomers] = useState<Customer[]>([]);
  const [periods, setPeriods] = useState<FiscalPeriod[]>([]);
  const [vatRate, setVatRate] = useState(0);
  const [error, setError] = useState<string | null>(null);
  const [editing, setEditing] = useState<string | "new" | null>(null);
  const [customerId, setCustomerId] = useState("");
  const [date, setDate] = useState(today());
  const [validUntil, setValidUntil] = useState("");
  const [notes, setNotes] = useState("");
  const [terms, setTerms] = useState("");
  const [lines, setLines] = useState<TradeLineInput[]>([emptyLine()]);
  const [converting, setConverting] = useState<Quotation | null>(null);
  const [cv, setCv] = useState({ invoiceDate: today(), fiscalPeriodId: "", paymentTerm: 3 });

  const load = async () => {
    const [q, c, p, s] = await Promise.all([QuotationsApi.getAll(), SalesApi.getCustomers(), LookupsApi.fiscalPeriods(), LookupsApi.companySettings()]);
    setList(q.data); setCustomers(c.data); setPeriods(p.data.filter((f) => !f.isClosed)); setVatRate(s.data.vatRate);
  };
  useEffect(() => { load().catch((e) => setError(getErrorMessage(e))); }, []);

  const rate = (l: TradeLineInput) => l.vatRate ?? vatRate;
  const net = lines.reduce((s, l) => s + l.quantity * l.unitPrice, 0);
  const vat = lines.reduce((s, l) => s + l.quantity * l.unitPrice * rate(l), 0);
  const setLine = (i: number, patch: Partial<TradeLineInput>) => setLines((p) => p.map((l, k) => (k === i ? { ...l, ...patch } : l)));

  function open(q?: Quotation) {
    setError(null);
    if (q) { setEditing(q.id); setCustomerId(q.customerId); setDate(q.quotationDate.slice(0, 10)); setValidUntil(q.validUntil?.slice(0, 10) ?? ""); setNotes(q.notes ?? ""); setTerms(q.terms ?? ""); setLines(q.lines.map((l) => ({ description: l.description, quantity: l.quantity, unitPrice: l.unitPrice, vatRate: l.vatRate }))); }
    else { setEditing("new"); setCustomerId(""); setDate(today()); setValidUntil(""); setNotes(""); setTerms(""); setLines([emptyLine()]); }
  }
  async function save(e: React.FormEvent) {
    e.preventDefault(); setError(null);
    const body = { customerId, quotationDate: date, validUntil: validUntil || null, notes: notes || null, terms: terms || null, lines };
    try { if (editing === "new") await QuotationsApi.create(body); else await QuotationsApi.update(editing!, body); setEditing(null); await load(); } catch (err) { setError(getErrorMessage(err)); }
  }
  async function status(id: string, s: number) { setError(null); try { await QuotationsApi.setStatus(id, s); await load(); } catch (err) { setError(getErrorMessage(err)); } }
  async function convert() {
    if (!converting) return; setError(null);
    try { await QuotationsApi.convert(converting.id, cv); setConverting(null); await load(); } catch (err) { setError(getErrorMessage(err)); }
  }
  function print(q: Quotation) {
    const w = window.open("", "_blank"); if (!w) return;
    const rows = q.lines.map((l) => `<tr><td>${l.lineNumber}</td><td>${esc(l.description)}</td><td>${l.quantity}</td><td>${money(l.unitPrice)}</td><td>${money(l.lineTotal)}</td></tr>`).join("");
    w.document.write(`<html dir="${lang === "ar" ? "rtl" : "ltr"}"><head><meta charset="utf-8"><title>${q.quotationNumber}</title><style>body{font-family:sans-serif;padding:24px}table{width:100%;border-collapse:collapse}td,th{border:1px solid #999;padding:6px;text-align:start}</style></head><body><h2>${x.title} ${q.quotationNumber}</h2><p>${x.customer}: ${esc(q.customerName)}<br>${x.date}: ${q.quotationDate.slice(0, 10)}${q.validUntil ? `<br>${x.valid}: ${q.validUntil.slice(0, 10)}` : ""}</p><table><tr><th>#</th><th>${x.desc}</th><th>${x.qty}</th><th>${x.price}</th><th>${x.total}</th></tr>${rows}</table><p>${x.net}: ${money(q.subTotal)}<br>${x.tax}: ${money(q.vatAmount)}<br><b>${x.total}: ${money(q.totalAmount)}</b></p>${q.terms ? `<p>${esc(q.terms)}</p>` : ""}${q.notes ? `<p>${esc(q.notes)}</p>` : ""}</body></html>`);
    w.document.close(); w.print();
  }
  const esc = (s: string) => s.replace(/[&<>"]/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" })[c]!);

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
            <div className="form-field"><label>{x.valid}</label><input type="date" value={validUntil} onChange={(e) => setValidUntil(e.target.value)} /></div>
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
          <div className="form-field" style={{ marginTop: 10 }}><label>{x.terms}</label><textarea rows={2} maxLength={2000} value={terms} onChange={(e) => setTerms(e.target.value)} /></div>
          <div className="form-field"><label>{x.notes}</label><input maxLength={1000} value={notes} onChange={(e) => setNotes(e.target.value)} /></div>
          <div>{x.net}: {money(net)} — {x.tax}: {money(vat)} — <strong>{x.total}: {money(net + vat)}</strong></div>
          <button className="btn" style={{ marginTop: 10 }}>{x.save}</button>
        </form>
      )}

      {converting && (
        <div className="card" style={{ maxWidth: 520, marginBottom: 16 }}>
          <strong>{x.convertTitle}: {converting.quotationNumber}</strong>
          <div className="text-muted">{x.note}</div>
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
            <tbody>
              {list.map((q) => (
                <tr key={q.id}>
                  <td>{q.quotationNumber}</td><td>{q.customerName}</td><td>{q.quotationDate.slice(0, 10)}</td><td>{money(q.totalAmount)}</td>
                  <td>{x.s[q.status]}{q.isExpired ? ` — ${x.expired}` : ""}</td>
                  <td style={{ display: "flex", gap: 6, flexWrap: "wrap" }}>
                    {(q.status === 1 || q.status === 2) && <button className="btn btn-secondary btn-sm" onClick={() => open(q)}>{x.edit}</button>}
                    {q.status === 1 && <button className="btn btn-secondary btn-sm" onClick={() => status(q.id, 2)}>{x.send}</button>}
                    {(q.status === 1 || q.status === 2) && <button className="btn btn-secondary btn-sm" onClick={() => status(q.id, 3)}>{x.accept}</button>}
                    {(q.status === 1 || q.status === 2) && <button className="btn btn-secondary btn-sm" onClick={() => status(q.id, 4)}>{x.reject}</button>}
                    {q.status === 4 && <button className="btn btn-secondary btn-sm" onClick={() => status(q.id, 1)}>{x.reopen}</button>}
                    {q.status !== 4 && q.status !== 5 && <button className="btn btn-sm" onClick={() => { setConverting(q); setCv({ invoiceDate: today(), fiscalPeriodId: "", paymentTerm: 3 }); }}>{x.toInvoice}</button>}
                    <button className="btn btn-secondary btn-sm" onClick={() => print(q)}>{x.print}</button>
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
