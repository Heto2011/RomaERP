import { useEffect, useState } from "react";
import { PurchaseNotesApi, PurchasingApi, AccountsApi, LookupsApi, type PurchaseNote } from "../../api/services";
import type { Account, FiscalPeriod, PurchaseInvoice } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";

const TEXT = {
  ar: {
    title: "مرتجعات المشتريات والرسوم الإضافية", add: "إشعار جديد", cancel: "إلغاء", save: "سجّل الإشعار", invoice: "فاتورة المورد", choose: "اختار...", type: "النوع",
    ret: "مرتجع للمورد (علينا أقل)", extra: "رسوم إضافية من المورد (علينا أكتر)", date: "التاريخ", period: "الفترة المحاسبية", reason: "السبب", notes: "ملاحظات",
    lines: "البنود", desc: "الوصف", qty: "الكمية", price: "السعر", vat: "الضريبة %", account: "الحساب", addLine: "إضافة بند", removeLine: "حذف البند", total: "الإجمالي", net: "قبل الضريبة", tax: "الضريبة",
    number: "الرقم", vendor: "المورد", kind: "النوع", empty: "مفيش إشعارات لسه.",
    hint: "الإشعار بيتسجل في الدفاتر فورًا ويعدّل رصيد المورد. لو رجّعت بضاعة من المخزن بالفعل، سجّل حركة مخزون منفصلة من صفحة تسويات المخزون.",
    hint2: "اختار حساب كل بند: نفس الحساب اللي اتسجلت عليه المشتريات (مثلًا المخزون أو المصروف).",
  },
  en: {
    title: "Purchase returns and extra charges", add: "New note", cancel: "Cancel", save: "Post the note", invoice: "Supplier invoice", choose: "Choose...", type: "Type",
    ret: "Return to the supplier (we owe less)", extra: "Extra charge from the supplier (we owe more)", date: "Date", period: "Fiscal period", reason: "Reason", notes: "Notes",
    lines: "Lines", desc: "Description", qty: "Qty", price: "Price", vat: "Tax %", account: "Account", addLine: "Add line", removeLine: "Remove line", total: "Total", net: "Before tax", tax: "Tax",
    number: "No.", vendor: "Supplier", kind: "Type", empty: "No notes yet.",
    hint: "The note is posted to the ledger right away and changes the supplier balance. If goods physically went back, record a separate stock movement from the stock adjustments page.",
    hint2: "Choose the account of each line: the same account the purchase was booked to (for example stock or an expense).",
  },
};
type Line = { description: string; quantity: number; unitPrice: number; vatRate: number | null; accountId: string };
const today = () => new Date().toISOString().slice(0, 10);
const money = (n: number) => n.toLocaleString(undefined, { maximumFractionDigits: 2 });

export default function PurchaseNotes() {
  const { lang } = useLanguage();
  const x = TEXT[lang === "ar" ? "ar" : "en"];
  const [list, setList] = useState<PurchaseNote[]>([]);
  const [invoices, setInvoices] = useState<PurchaseInvoice[]>([]);
  const [accounts, setAccounts] = useState<Account[]>([]);
  const [periods, setPeriods] = useState<FiscalPeriod[]>([]);
  const [vatRate, setVatRate] = useState(0);
  const [error, setError] = useState<string | null>(null);
  const [open, setOpen] = useState(false);
  const [invoiceId, setInvoiceId] = useState("");
  const [noteType, setNoteType] = useState(1);
  const [date, setDate] = useState(today());
  const [periodId, setPeriodId] = useState("");
  const [reason, setReason] = useState("");
  const [notes, setNotes] = useState("");
  const [lines, setLines] = useState<Line[]>([{ description: "", quantity: 1, unitPrice: 0, vatRate: null, accountId: "" }]);

  const load = async () => {
    const [n, i, a, p, s] = await Promise.all([PurchaseNotesApi.getAll(), PurchasingApi.getInvoices(), AccountsApi.getAll(), LookupsApi.fiscalPeriods(), LookupsApi.companySettings()]);
    setList(n.data); setInvoices(i.data); setAccounts(a.data.filter((k) => k.isActive && !k.isControlAccount)); setPeriods(p.data.filter((f) => !f.isClosed)); setVatRate(s.data.vatRate);
  };
  useEffect(() => { load().catch((e) => setError(getErrorMessage(e))); }, []);

  const rate = (l: Line) => l.vatRate ?? vatRate;
  const net = lines.reduce((s, l) => s + l.quantity * l.unitPrice, 0);
  const vat = lines.reduce((s, l) => s + l.quantity * l.unitPrice * rate(l), 0);
  const setLine = (i: number, patch: Partial<Line>) => setLines((p) => p.map((l, k) => (k === i ? { ...l, ...patch } : l)));

  async function save(e: React.FormEvent) {
    e.preventDefault(); setError(null);
    try {
      await PurchaseNotesApi.create({ originalInvoiceId: invoiceId, noteType, noteDate: date, fiscalPeriodId: periodId, reason, notes: notes || null, lines });
      setOpen(false); setInvoiceId(""); setReason(""); setNotes(""); setLines([{ description: "", quantity: 1, unitPrice: 0, vatRate: null, accountId: "" }]);
      await load();
    } catch (err) { setError(getErrorMessage(err)); }
  }

  return (
    <div>
      <div className="page-header"><h1>{x.title}</h1><button className="btn" onClick={() => setOpen((o) => !o)}>{open ? x.cancel : x.add}</button></div>
      <p className="text-muted">{x.hint}</p>
      {error && <div className="alert-error">{error}</div>}

      {open && (
        <form className="card" onSubmit={save} style={{ maxWidth: 860, marginBottom: 16 }}>
          <div className="form-grid">
            <div className="form-field"><label>{x.invoice}</label>
              <select required value={invoiceId} onChange={(e) => setInvoiceId(e.target.value)}><option value="">{x.choose}</option>{invoices.map((i) => <option key={i.id} value={i.id}>{i.invoiceNumber} — {i.vendorName}</option>)}</select></div>
            <div className="form-field"><label>{x.type}</label>
              <select value={noteType} onChange={(e) => setNoteType(Number(e.target.value))}><option value={1}>{x.ret}</option><option value={2}>{x.extra}</option></select></div>
            <div className="form-field"><label>{x.date}</label><input type="date" required value={date} onChange={(e) => setDate(e.target.value)} /></div>
            <div className="form-field"><label>{x.period}</label>
              <select required value={periodId} onChange={(e) => setPeriodId(e.target.value)}><option value="">{x.choose}</option>{periods.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}</select></div>
          </div>
          <div className="form-field"><label>{x.reason}</label><input required maxLength={500} value={reason} onChange={(e) => setReason(e.target.value)} /></div>
          <label>{x.lines}</label>
          <p className="text-muted" style={{ margin: "2px 0 6px", fontSize: 13 }}>{x.hint2}</p>
          {lines.map((l, i) => (
            <div key={i} style={{ display: "grid", gridTemplateColumns: "3fr 1fr 1fr 1fr 2fr auto", gap: 8, marginBottom: 6 }}>
              <input required value={l.description} placeholder={x.desc} onChange={(e) => setLine(i, { description: e.target.value })} />
              <input type="number" min={0} step="any" value={l.quantity} placeholder={x.qty} onChange={(e) => setLine(i, { quantity: Number(e.target.value) })} />
              <input type="number" min={0} step="any" value={l.unitPrice} placeholder={x.price} onChange={(e) => setLine(i, { unitPrice: Number(e.target.value) })} />
              <input type="number" min={0} max={100} step="any" value={l.vatRate === null ? vatRate * 100 : l.vatRate * 100} placeholder={x.vat} onChange={(e) => setLine(i, { vatRate: Number(e.target.value) / 100 })} />
              <select required value={l.accountId} onChange={(e) => setLine(i, { accountId: e.target.value })}><option value="">{x.account}</option>{accounts.map((a) => <option key={a.id} value={a.id}>{a.code} {a.nameAr}</option>)}</select>
              <button type="button" className="btn btn-secondary btn-sm" disabled={lines.length === 1} title={x.removeLine} onClick={() => setLines((p) => p.filter((_, k) => k !== i))}>×</button>
            </div>
          ))}
          <button type="button" className="btn btn-secondary btn-sm" onClick={() => setLines((p) => [...p, { description: "", quantity: 1, unitPrice: 0, vatRate: null, accountId: "" }])}>{x.addLine}</button>
          <div className="form-field" style={{ marginTop: 10 }}><label>{x.notes}</label><input maxLength={1000} value={notes} onChange={(e) => setNotes(e.target.value)} /></div>
          <div>{x.net}: {money(net)} — {x.tax}: {money(vat)} — <strong>{x.total}: {money(net + vat)}</strong></div>
          <button className="btn" style={{ marginTop: 10 }}>{x.save}</button>
        </form>
      )}

      <div className="card" style={{ overflowX: "auto" }}>
        {list.length === 0 ? <p className="text-muted">{x.empty}</p> : (
          <table>
            <thead><tr><th>{x.number}</th><th>{x.vendor}</th><th>{x.invoice}</th><th>{x.kind}</th><th>{x.date}</th><th>{x.total}</th></tr></thead>
            <tbody>{list.map((n) => (
              <tr key={n.id}><td>{n.noteNumber}</td><td>{n.vendorName}</td><td>{n.originalInvoiceNumber}</td><td>{n.noteType === 1 ? x.ret : x.extra}</td><td>{n.noteDate.slice(0, 10)}</td><td>{money(n.totalAmount)}</td></tr>
            ))}</tbody>
          </table>
        )}
      </div>
    </div>
  );
}
