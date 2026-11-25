import { useEffect, useState } from "react";
import { RecurringInvoicesApi, SalesApi, LookupsApi, type RecurringInvoice, type RecurringDue, type RecurringLine } from "../../api/services";
import type { Customer, FiscalPeriod } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";

const TEXT = {
  ar: {
    title: "فواتير متكررة", add: "فاتورة متكررة جديدة", cancel: "إلغاء", save: "حفظ", name: "الاسم (مثلًا: اشتراك شهري)", customer: "العميل", choose: "اختار...",
    every: "التكرار", m1: "كل شهر", m3: "كل 3 شهور", m6: "كل 6 شهور", m12: "كل سنة", day: "يوم الاستحقاق من الشهر", start: "تبدأ من", end: "تنتهي في (اختياري)",
    payment: "طريقة الدفع", cash: "نقدي", card: "كارت", credit: "آجل", lines: "البنود", desc: "الوصف", qty: "الكمية", price: "السعر", addLine: "إضافة بند", removeLine: "حذف البند", notes: "ملاحظات",
    pending: "مستنية إصدارك", noPending: "مفيش فواتير مستنية دلوقتي.", all: "كل الفواتير المتكررة", empty: "مفيش فواتير متكررة لسه.", total: "الإجمالي (قبل الضريبة)",
    issue: "أصدر الفاتورة", skip: "تخطّى الشهر ده", edit: "تعديل", pause: "إيقاف", resume: "تشغيل", paused: "موقوفة", due: "استحقاق", prepare: "حضّر المستحق دلوقتي",
    invoiceDate: "تاريخ الفاتورة", period: "الفترة المحاسبية", confirm: "أصدر", close: "إغلاق",
    hint: "النظام بيحضّر الفاتورة لما ميعادها يجي، وإنت اللي بتصدرها أو بتتخطاها. مفيش حاجة بتتسجل في الدفاتر من نفسها. وتقدر تعدّل البنود قبل الإصدار من غير ما تغيّر الأصل.",
  },
  en: {
    title: "Recurring invoices", add: "New recurring invoice", cancel: "Cancel", save: "Save", name: "Name (for example: monthly subscription)", customer: "Customer", choose: "Choose...",
    every: "Repeats", m1: "Every month", m3: "Every 3 months", m6: "Every 6 months", m12: "Every year", day: "Day of the month it falls due", start: "Starts on", end: "Ends on (optional)",
    payment: "Payment", cash: "Cash", card: "Card", credit: "On account", lines: "Lines", desc: "Description", qty: "Qty", price: "Price", addLine: "Add line", removeLine: "Remove line", notes: "Notes",
    pending: "Waiting for you to issue", noPending: "Nothing waiting right now.", all: "All recurring invoices", empty: "No recurring invoices yet.", total: "Total (before tax)",
    issue: "Issue the invoice", skip: "Skip this period", edit: "Edit", pause: "Pause", resume: "Resume", paused: "Paused", due: "Due", prepare: "Prepare what is due now",
    invoiceDate: "Invoice date", period: "Fiscal period", confirm: "Issue", close: "Close",
    hint: "The system prepares the invoice when it falls due, and you issue it or skip it. Nothing reaches the ledger by itself. You can edit the lines before issuing without changing the original.",
  },
};
const today = () => new Date().toISOString().slice(0, 10);
const blank = (): RecurringLine => ({ description: "", quantity: 1, unitPrice: 0, vatRate: null });
const money = (n: number) => n.toLocaleString(undefined, { maximumFractionDigits: 2 });

export default function RecurringInvoices() {
  const { lang } = useLanguage();
  const x = TEXT[lang === "ar" ? "ar" : "en"];
  const [list, setList] = useState<RecurringInvoice[]>([]);
  const [pending, setPending] = useState<RecurringDue[]>([]);
  const [customers, setCustomers] = useState<Customer[]>([]);
  const [periods, setPeriods] = useState<FiscalPeriod[]>([]);
  const [vatRate, setVatRate] = useState(0);
  const [error, setError] = useState<string | null>(null);
  const [editing, setEditing] = useState<string | "new" | null>(null);
  const [f, setF] = useState({ title: "", customerId: "", intervalMonths: 1, dayOfMonth: 1, startDate: today(), endDate: "", paymentTerm: 3, notes: "" });
  const [lines, setLines] = useState<RecurringLine[]>([blank()]);
  const [issuing, setIssuing] = useState<RecurringDue | null>(null);
  const [iss, setIss] = useState({ invoiceDate: today(), fiscalPeriodId: "" });
  const [issLines, setIssLines] = useState<RecurringLine[]>([]);

  const load = async () => {
    const [a, p, c, per, s] = await Promise.all([RecurringInvoicesApi.getAll(), RecurringInvoicesApi.pending(), SalesApi.getCustomers(), LookupsApi.fiscalPeriods(), LookupsApi.companySettings()]);
    setList(a.data); setPending(p.data); setCustomers(c.data); setPeriods(per.data.filter((k) => !k.isClosed)); setVatRate(s.data.vatRate);
  };
  useEffect(() => { load().catch((e) => setError(getErrorMessage(e))); }, []);

  function open(r?: RecurringInvoice) {
    setError(null);
    if (r) { setEditing(r.id); setF({ title: r.title, customerId: r.customerId, intervalMonths: r.intervalMonths, dayOfMonth: r.dayOfMonth, startDate: r.startDate.slice(0, 10), endDate: r.endDate?.slice(0, 10) ?? "", paymentTerm: r.paymentTerm, notes: r.notes ?? "" }); setLines(r.lines.map((l) => ({ ...l }))); }
    else { setEditing("new"); setF({ title: "", customerId: "", intervalMonths: 1, dayOfMonth: 1, startDate: today(), endDate: "", paymentTerm: 3, notes: "" }); setLines([blank()]); }
  }
  async function save(e: React.FormEvent) {
    e.preventDefault(); setError(null);
    const body = { ...f, endDate: f.endDate || null, notes: f.notes || null, lines };
    try { if (editing === "new") await RecurringInvoicesApi.create(body); else await RecurringInvoicesApi.update(editing!, body); setEditing(null); await load(); } catch (err) { setError(getErrorMessage(err)); }
  }
  async function act(fn: () => Promise<unknown>) { setError(null); try { await fn(); await load(); } catch (err) { setError(getErrorMessage(err)); } }
  async function issue() {
    if (!issuing) return; setError(null);
    try { await RecurringInvoicesApi.issue(issuing.id, { ...iss, lines: issLines }); setIssuing(null); await load(); } catch (err) { setError(getErrorMessage(err)); }
  }
  const setLine = (i: number, patch: Partial<RecurringLine>) => setLines((p) => p.map((l, k) => (k === i ? { ...l, ...patch } : l)));
  const setIssLine = (i: number, patch: Partial<RecurringLine>) => setIssLines((p) => p.map((l, k) => (k === i ? { ...l, ...patch } : l)));
  const every = (m: number) => ({ 1: x.m1, 3: x.m3, 6: x.m6, 12: x.m12 } as Record<number, string>)[m];

  return (
    <div>
      <div className="page-header"><h1>{x.title}</h1><button className="btn" onClick={() => (editing ? setEditing(null) : open())}>{editing ? x.cancel : x.add}</button></div>
      <p className="text-muted">{x.hint}</p>
      {error && <div className="alert-error">{error}</div>}

      <div className="card">
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", gap: 8, flexWrap: "wrap" }}>
          <strong>{x.pending}</strong>
          <button className="btn btn-secondary btn-sm" onClick={() => act(() => RecurringInvoicesApi.prepareNow())}>{x.prepare}</button>
        </div>
        {pending.length === 0 ? <p className="text-muted">{x.noPending}</p> : pending.map((d) => (
          <div key={d.id} style={{ display: "flex", justifyContent: "space-between", alignItems: "center", gap: 8, flexWrap: "wrap", borderTop: "1px solid var(--color-border)", padding: "8px 0" }}>
            <span>{d.title} — {d.customerName} — {x.due} {d.dueDate.slice(0, 10)} — <strong>{money(d.total)}</strong></span>
            <span style={{ display: "flex", gap: 6 }}>
              <button className="btn btn-sm" onClick={() => { setIssuing(d); setIss({ invoiceDate: today(), fiscalPeriodId: "" }); setIssLines(d.lines.map((l) => ({ ...l }))); }}>{x.issue}</button>
              <button className="btn btn-secondary btn-sm" onClick={() => act(() => RecurringInvoicesApi.skip(d.id))}>{x.skip}</button>
            </span>
          </div>
        ))}
      </div>

      {issuing && (
        <div className="card" style={{ maxWidth: 760 }}>
          <strong>{issuing.title} — {issuing.customerName} ({issuing.periodKey})</strong>
          <div className="form-grid" style={{ marginTop: 8 }}>
            <div className="form-field"><label>{x.invoiceDate}</label><input type="date" value={iss.invoiceDate} onChange={(e) => setIss({ ...iss, invoiceDate: e.target.value })} /></div>
            <div className="form-field"><label>{x.period}</label>
              <select value={iss.fiscalPeriodId} onChange={(e) => setIss({ ...iss, fiscalPeriodId: e.target.value })}><option value="">{x.choose}</option>{periods.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}</select></div>
          </div>
          {issLines.map((l, i) => (
            <div key={i} style={{ display: "grid", gridTemplateColumns: "3fr 1fr 1fr", gap: 8, marginBottom: 6 }}>
              <input value={l.description} placeholder={x.desc} onChange={(e) => setIssLine(i, { description: e.target.value })} />
              <input type="number" min={0} step="any" value={l.quantity} onChange={(e) => setIssLine(i, { quantity: Number(e.target.value) })} />
              <input type="number" min={0} step="any" value={l.unitPrice} onChange={(e) => setIssLine(i, { unitPrice: Number(e.target.value) })} />
            </div>
          ))}
          <div style={{ display: "flex", gap: 8, marginTop: 8 }}>
            <button className="btn" disabled={!iss.fiscalPeriodId} onClick={issue}>{x.confirm}</button>
            <button className="btn btn-secondary" onClick={() => setIssuing(null)}>{x.close}</button>
          </div>
        </div>
      )}

      {editing && (
        <form className="card" onSubmit={save} style={{ maxWidth: 820 }}>
          <div className="form-grid">
            <div className="form-field"><label>{x.name}</label><input required maxLength={200} value={f.title} onChange={(e) => setF({ ...f, title: e.target.value })} /></div>
            <div className="form-field"><label>{x.customer}</label>
              <select required value={f.customerId} onChange={(e) => setF({ ...f, customerId: e.target.value })}><option value="">{x.choose}</option>{customers.map((c) => <option key={c.id} value={c.id}>{c.nameAr}</option>)}</select></div>
            <div className="form-field"><label>{x.every}</label>
              <select value={f.intervalMonths} onChange={(e) => setF({ ...f, intervalMonths: Number(e.target.value) })}><option value={1}>{x.m1}</option><option value={3}>{x.m3}</option><option value={6}>{x.m6}</option><option value={12}>{x.m12}</option></select></div>
            <div className="form-field"><label>{x.day}</label><input type="number" min={1} max={31} value={f.dayOfMonth} onChange={(e) => setF({ ...f, dayOfMonth: Number(e.target.value) })} /></div>
            <div className="form-field"><label>{x.start}</label><input type="date" required value={f.startDate} onChange={(e) => setF({ ...f, startDate: e.target.value })} /></div>
            <div className="form-field"><label>{x.end}</label><input type="date" value={f.endDate} onChange={(e) => setF({ ...f, endDate: e.target.value })} /></div>
            <div className="form-field"><label>{x.payment}</label>
              <select value={f.paymentTerm} onChange={(e) => setF({ ...f, paymentTerm: Number(e.target.value) })}><option value={1}>{x.cash}</option><option value={2}>{x.card}</option><option value={3}>{x.credit}</option></select></div>
          </div>
          <label>{x.lines}</label>
          {lines.map((l, i) => (
            <div key={i} style={{ display: "grid", gridTemplateColumns: "3fr 1fr 1fr 1fr auto", gap: 8, marginBottom: 6 }}>
              <input required value={l.description} placeholder={x.desc} onChange={(e) => setLine(i, { description: e.target.value })} />
              <input type="number" min={0} step="any" value={l.quantity} placeholder={x.qty} onChange={(e) => setLine(i, { quantity: Number(e.target.value) })} />
              <input type="number" min={0} step="any" value={l.unitPrice} placeholder={x.price} onChange={(e) => setLine(i, { unitPrice: Number(e.target.value) })} />
              <input type="number" min={0} max={100} step="any" value={l.vatRate === null ? vatRate * 100 : l.vatRate * 100} onChange={(e) => setLine(i, { vatRate: Number(e.target.value) / 100 })} />
              <button type="button" className="btn btn-secondary btn-sm" disabled={lines.length === 1} title={x.removeLine} onClick={() => setLines((p) => p.filter((_, k) => k !== i))}>×</button>
            </div>
          ))}
          <button type="button" className="btn btn-secondary btn-sm" onClick={() => setLines((p) => [...p, blank()])}>{x.addLine}</button>
          <div className="form-field" style={{ marginTop: 10 }}><label>{x.notes}</label><input maxLength={1000} value={f.notes} onChange={(e) => setF({ ...f, notes: e.target.value })} /></div>
          <button className="btn" style={{ marginTop: 10 }}>{x.save}</button>
        </form>
      )}

      <div className="card" style={{ overflowX: "auto" }}>
        <strong>{x.all}</strong>
        {list.length === 0 ? <p className="text-muted">{x.empty}</p> : (
          <table>
            <thead><tr><th>{x.name.split(" (")[0]}</th><th>{x.customer}</th><th>{x.every}</th><th>{x.total}</th><th /></tr></thead>
            <tbody>{list.map((r) => (
              <tr key={r.id}>
                <td>{r.title}{!r.isActive ? ` (${x.paused})` : ""}</td><td>{r.customerName}</td><td>{every(r.intervalMonths)}</td><td>{money(r.total)}</td>
                <td style={{ display: "flex", gap: 6 }}>
                  <button className="btn btn-secondary btn-sm" onClick={() => open(r)}>{x.edit}</button>
                  <button className="btn btn-secondary btn-sm" onClick={() => act(() => RecurringInvoicesApi.setActive(r.id, !r.isActive))}>{r.isActive ? x.pause : x.resume}</button>
                </td>
              </tr>
            ))}</tbody>
          </table>
        )}
      </div>
    </div>
  );
}
