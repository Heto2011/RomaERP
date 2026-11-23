import { useState } from "react";
import { Link } from "react-router-dom";
import { AiAssistantApi, type ActionProposal } from "../../api/services";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";

const TEXT = {
  ar: {
    title: "اطلب من المساعد", intro: "اكتب طلبك بكلامك العادي، والمساعد يجهّز لك المسودة. إنت بتراجعها وتعدّلها وتوافق، ومفيش حاجة بتتسجل قبل ما تدوس \"نفّذ\".",
    placeholder: "مثال: اعمل فاتورة لأحمد بـ 3 قهوة سعر الواحدة 40", prepare: "جهّز", preparing: "جاري التجهيز...",
    examples: ["اعمل فاتورة لمنى بـ 2 اشتراك شهري سعر الواحد 500", "سجّل دفعة من أحمد علي", "غيّر سعر القهوة لـ 50"],
    customer: "العميل", choose: "اختار...", lines: "البنود", desc: "الوصف", qty: "الكمية", price: "السعر", addLine: "إضافة بند", payment: "طريقة الدفع",
    cash: "نقدي", card: "كارت", credit: "آجل", note: "ملاحظة", invoice: "الفاتورة", outstanding: "المتبقي", amount: "المبلغ", method: "الطريقة",
    item: "الصنف", oldPrice: "السعر الحالي", newPrice: "السعر الجديد", run: "نفّذ", cancel: "إلغاء", done: "تم", open: "افتح الصفحة", another: "طلب جديد",
    review: "راجع المسودة قبل التنفيذ", total: "الإجمالي",
  },
  en: {
    title: "Ask the assistant to do it", intro: "Write your request in plain words and the assistant prepares the draft. You review, edit and approve it, and nothing is booked until you press Execute.",
    placeholder: "For example: make an invoice for Ahmed for 3 coffees at 40 each", prepare: "Prepare", preparing: "Preparing...",
    examples: ["Invoice Mona for 2 monthly subscriptions at 500 each", "Record a payment from Ahmed Ali", "Change the price of coffee to 50"],
    customer: "Customer", choose: "Choose...", lines: "Lines", desc: "Description", qty: "Qty", price: "Price", addLine: "Add line", payment: "Payment",
    cash: "Cash", card: "Card", credit: "On account", note: "Note", invoice: "Invoice", outstanding: "Outstanding", amount: "Amount", method: "Method",
    item: "Item", oldPrice: "Current price", newPrice: "New price", run: "Execute", cancel: "Cancel", done: "Done", open: "Open the page", another: "New request",
    review: "Review the draft before executing", total: "Total",
  },
};

const KIND = { None: 0, SalesInvoice: 1, CustomerPayment: 2, MenuPrice: 3 };

/** One sentence in, a draft out. The assistant only prepares: the owner edits it and presses Execute, and the normal services book it. */
export default function AssistantActions() {
  const { lang } = useLanguage();
  const x = TEXT[lang === "ar" ? "ar" : "en"];
  const [text, setText] = useState("");
  const [p, setP] = useState<ActionProposal | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<{ message: string; link: string | null } | null>(null);

  async function prepare(e?: React.FormEvent) {
    e?.preventDefault();
    if (!text.trim()) return;
    setBusy(true); setError(null); setResult(null); setP(null);
    try { setP((await AiAssistantApi.proposeAction(text)).data); } catch (err) { setError(getErrorMessage(err)); } finally { setBusy(false); }
  }

  const set = (patch: Partial<ActionProposal>) => setP((cur) => (cur ? { ...cur, ...patch } : cur));
  const setLine = (i: number, patch: Partial<ActionProposal["lines"][number]>) => p && set({ lines: p.lines.map((l, k) => (k === i ? { ...l, ...patch } : l)) });

  const ready = (() => {
    if (!p) return false;
    if (p.kind === KIND.SalesInvoice) return !!p.customerId && p.lines.length > 0 && p.lines.every((l) => l.description.trim() && l.quantity > 0 && l.unitPrice !== null && l.unitPrice >= 0);
    if (p.kind === KIND.CustomerPayment) return !!p.invoiceId && (p.amount ?? 0) > 0;
    if (p.kind === KIND.MenuPrice) return !!p.itemId && p.newPrice !== null && p.newPrice >= 0;
    return false;
  })();
  const total = p && p.kind === KIND.SalesInvoice ? p.lines.reduce((s, l) => s + l.quantity * (l.unitPrice ?? 0), 0) : 0;

  async function run() {
    if (!p) return;
    setBusy(true); setError(null);
    try {
      const r = await AiAssistantApi.executeAction({
        kind: p.kind, customerId: p.customerId, lines: p.lines, paymentTerm: p.paymentTerm, note: p.note,
        invoiceId: p.invoiceId, amount: p.amount, method: p.method, itemId: p.itemId, newPrice: p.newPrice,
      });
      setResult(r.data); setP(null); setText("");
    } catch (err) { setError(getErrorMessage(err)); } finally { setBusy(false); }
  }

  const pick = (list: { id: string; name: string; extra: string | null }[], value: string | null, onChange: (id: string, name: string) => void) => (
    <select value={value ?? ""} onChange={(e) => { const c = list.find((k) => k.id === e.target.value); if (c) onChange(c.id, c.name); }}>
      <option value="">{x.choose}</option>
      {list.map((c) => <option key={c.id} value={c.id}>{c.name}{c.extra ? ` (${c.extra})` : ""}</option>)}
    </select>
  );

  return (
    <div>
      <div className="page-header"><h1>{x.title}</h1></div>
      <div className="card" style={{ maxWidth: 760 }}>
        <p className="text-muted" style={{ marginTop: 0 }}>{x.intro}</p>
        <form onSubmit={prepare}>
          <textarea rows={3} maxLength={1000} value={text} onChange={(e) => setText(e.target.value)} placeholder={x.placeholder} style={{ width: "100%" }} />
          <div style={{ display: "flex", gap: 8, flexWrap: "wrap", margin: "8px 0" }}>
            {x.examples.map((ex) => <button key={ex} type="button" className="btn btn-secondary btn-sm" onClick={() => setText(ex)}>{ex}</button>)}
          </div>
          <button className="btn" type="submit" disabled={busy || !text.trim()}>{busy && !p ? x.preparing : x.prepare}</button>
        </form>
        {error && <div className="alert-error" style={{ marginTop: 10 }}>{error}</div>}
        {result && (
          <div className="alert-success" style={{ marginTop: 10 }}>
            <div>{x.done}: {result.message}</div>
            <div style={{ display: "flex", gap: 8, marginTop: 8 }}>
              {result.link && <Link className="btn btn-secondary btn-sm" to={result.link}>{x.open}</Link>}
              <button className="btn btn-secondary btn-sm" onClick={() => setResult(null)}>{x.another}</button>
            </div>
          </div>
        )}
      </div>

      {p && (
        <div className="card" style={{ maxWidth: 760, marginTop: 12 }}>
          <strong>{p.kind === KIND.None ? p.summary : x.review}</strong>
          {p.kind !== KIND.None && <div className="text-muted" style={{ marginTop: 4 }}>{p.summary}</div>}
          {p.question && p.kind !== KIND.None && <div style={{ marginTop: 8 }}>{p.question}</div>}
          {p.warnings.map((w, i) => <div key={i} className="alert-error" style={{ marginTop: 8 }}>{w}</div>)}

          {p.kind === KIND.SalesInvoice && (
            <div style={{ marginTop: 10 }}>
              <div className="form-field"><label>{x.customer}</label>
                {p.customerCandidates.length > 0 ? pick(p.customerCandidates, p.customerId, (id, name) => set({ customerId: id, customerName: name })) : <span className="text-muted">-</span>}
              </div>
              <label>{x.lines}</label>
              {p.lines.map((l, i) => (
                <div key={i} style={{ display: "grid", gridTemplateColumns: "2fr 1fr 1fr", gap: 8, marginBottom: 6 }}>
                  <input value={l.description} placeholder={x.desc} onChange={(e) => setLine(i, { description: e.target.value })} />
                  <input type="number" min={0} step="any" value={l.quantity} placeholder={x.qty} onChange={(e) => setLine(i, { quantity: Number(e.target.value) })} />
                  <input type="number" min={0} step="any" value={l.unitPrice ?? ""} placeholder={x.price} onChange={(e) => setLine(i, { unitPrice: e.target.value === "" ? null : Number(e.target.value) })} />
                </div>
              ))}
              <button type="button" className="btn btn-secondary btn-sm" onClick={() => set({ lines: [...p.lines, { description: "", quantity: 1, unitPrice: null }] })}>{x.addLine}</button>
              <div className="form-grid" style={{ marginTop: 10 }}>
                <div className="form-field"><label>{x.payment}</label>
                  <select value={p.paymentTerm} onChange={(e) => set({ paymentTerm: Number(e.target.value) })}>
                    <option value={1}>{x.cash}</option><option value={2}>{x.card}</option><option value={3}>{x.credit}</option>
                  </select></div>
                <div className="form-field"><label>{x.note}</label><input value={p.note ?? ""} onChange={(e) => set({ note: e.target.value })} maxLength={200} /></div>
              </div>
              <div style={{ marginTop: 6 }}><strong>{x.total}: {total.toLocaleString(undefined, { maximumFractionDigits: 2 })}</strong></div>
            </div>
          )}

          {p.kind === KIND.CustomerPayment && (
            <div className="form-grid" style={{ marginTop: 10 }}>
              <div className="form-field"><label>{x.invoice}</label>
                {p.invoiceCandidates.length > 0
                  ? pick(p.invoiceCandidates, p.invoiceId, (id) => { const c = p.invoiceCandidates.find((k) => k.id === id); set({ invoiceId: id, invoiceNumber: c?.name ?? null }); })
                  : <span className="text-muted">-</span>}
                {p.outstandingAmount !== null && <small className="text-muted">{x.outstanding}: {p.outstandingAmount}</small>}
              </div>
              <div className="form-field"><label>{x.amount}</label><input type="number" min={0} step="any" value={p.amount ?? ""} onChange={(e) => set({ amount: e.target.value === "" ? null : Number(e.target.value) })} /></div>
              <div className="form-field"><label>{x.method}</label>
                <select value={p.method} onChange={(e) => set({ method: Number(e.target.value) })}><option value={1}>{x.cash}</option><option value={2}>{x.card}</option></select></div>
            </div>
          )}

          {p.kind === KIND.MenuPrice && (
            <div className="form-grid" style={{ marginTop: 10 }}>
              <div className="form-field"><label>{x.item}</label>
                {p.itemCandidates.length > 0
                  ? pick(p.itemCandidates, p.itemId, (id, name) => { const c = p.itemCandidates.find((k) => k.id === id); set({ itemId: id, itemName: name, oldPrice: c?.extra ? Number(c.extra) : null }); })
                  : <span className="text-muted">-</span>}
              </div>
              <div className="form-field"><label>{x.oldPrice}</label><input value={p.oldPrice ?? ""} disabled /></div>
              <div className="form-field"><label>{x.newPrice}</label><input type="number" min={0} step="any" value={p.newPrice ?? ""} onChange={(e) => set({ newPrice: e.target.value === "" ? null : Number(e.target.value) })} /></div>
            </div>
          )}

          {p.kind !== KIND.None && (
            <div style={{ display: "flex", gap: 8, marginTop: 12 }}>
              <button className="btn" disabled={!ready || busy} onClick={run}>{x.run}</button>
              <button className="btn btn-secondary" disabled={busy} onClick={() => setP(null)}>{x.cancel}</button>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
