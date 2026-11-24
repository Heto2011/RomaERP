import { useState } from "react";
import { AiAssistantApi, type ActionProposal, type AgentMessage } from "../../api/services";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";

const TEXT = {
  ar: {
    title: "اطلب من المساعد", intro: "اسأل عن شغلك أو اطلب إجراء بكلامك العادي. المساعد بيدوّر في بياناتك ويجاوب، ولو طلبت إجراء بيجهّز مسودة، وإنت بتراجعها وتوافق. مفيش حاجة بتتسجل قبل ما تدوس \"نفّذ\".",
    placeholder: "اكتب سؤالك أو طلبك...", prepare: "جهّز", preparing: "جاري التفكير...", send: "إرسال", you: "إنت", assistant: "المساعد",
    examples: ["كام اللي العملاء عليهم لينا؟", "إيه الأصناف اللي قربت تخلص؟", "ضيف مورد اسمه شركة النور", "اعمل فاتورة لمنى بـ 2 اشتراك سعر الواحد 500"],
    customer: "العميل", choose: "اختار...", lines: "البنود", desc: "الوصف", qty: "الكمية", price: "السعر", addLine: "إضافة بند", payment: "طريقة الدفع",
    cash: "نقدي", card: "كارت", credit: "آجل", note: "ملاحظة", invoice: "الفاتورة", outstanding: "المتبقي", amount: "المبلغ", method: "الطريقة",
    item: "الصنف", oldPrice: "السعر الحالي", newPrice: "السعر الجديد", run: "نفّذ", cancel: "إلغاء", done: "تم", open: "افتح الصفحة", another: "طلب جديد",
    review: "راجع المسودة قبل التنفيذ", total: "الإجمالي", name: "الاسم", debit: "مدين", credit2: "دائن", diff: "الفرق (لازم يكون صفر)", phone: "التليفون", email: "الإيميل", similar: "أسماء قريبة موجودة", vendor: "المورد",
  },
  en: {
    title: "Ask the assistant to do it", intro: "Ask about your business or request an action in plain words. The assistant looks through your data and answers; for an action it prepares a draft that you review and approve. Nothing is booked until you press Execute.",
    placeholder: "Type your question or request...", prepare: "Prepare", preparing: "Thinking...", send: "Send", you: "You", assistant: "Assistant",
    examples: ["How much do customers owe us?", "Which items are running low?", "Invoice Mona for 2 monthly subscriptions at 500 each"],
    customer: "Customer", choose: "Choose...", lines: "Lines", desc: "Description", qty: "Qty", price: "Price", addLine: "Add line", payment: "Payment",
    cash: "Cash", card: "Card", credit: "On account", note: "Note", invoice: "Invoice", outstanding: "Outstanding", amount: "Amount", method: "Method",
    item: "Item", oldPrice: "Current price", newPrice: "New price", run: "Execute", cancel: "Cancel", done: "Done", open: "Open the page", another: "New request",
    review: "Review the draft before executing", total: "Total", name: "Name", debit: "Debit", credit2: "Credit", diff: "Difference (must be zero)", phone: "Phone", email: "Email", similar: "Similar names already there", vendor: "Supplier",
  },
};

const KIND = { None: 0, SalesInvoice: 1, CustomerPayment: 2, MenuPrice: 3, CreateCustomer: 4, CreateVendor: 5, VendorPayment: 6, PurchaseInvoice: 7, JournalEntry: 8 };

/** A draft the assistant prepared. The owner edits it and presses Execute; only then does the normal service book it. */
function ProposalCard({ initial, onExecuted, onCancel }: { initial: ActionProposal; onExecuted: (message: string, link: string | null) => void; onCancel: () => void }) {
  const { lang } = useLanguage();
  const x = TEXT[lang === "ar" ? "ar" : "en"];
  const [p, setP] = useState<ActionProposal | null>(initial);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const set = (patch: Partial<ActionProposal>) => setP((cur) => (cur ? { ...cur, ...patch } : cur));
  const setLine = (i: number, patch: Partial<ActionProposal["lines"][number]>) => p && set({ lines: p.lines.map((l, k) => (k === i ? { ...l, ...patch } : l)) });

  const journalDiff = p && p.kind === KIND.JournalEntry ? Math.round((p.journalLines.reduce((s, l) => s + l.debit, 0) - p.journalLines.reduce((s, l) => s + l.credit, 0)) * 100) / 100 : 0;
  const ready = (() => {
    if (!p) return false;
    if (p.kind === KIND.SalesInvoice) return !!p.customerId && p.lines.length > 0 && p.lines.every((l) => l.description.trim() && l.quantity > 0 && l.unitPrice !== null && l.unitPrice >= 0);
    if (p.kind === KIND.CustomerPayment || p.kind === KIND.VendorPayment) return !!p.invoiceId && (p.amount ?? 0) > 0;
    if (p.kind === KIND.CreateCustomer || p.kind === KIND.CreateVendor) return !!p.partyName?.trim();
    if (p.kind === KIND.JournalEntry) return p.journalLines.length >= 2 && p.journalLines.every((l) => !!l.accountId && l.debit >= 0 && l.credit >= 0 && (l.debit > 0) !== (l.credit > 0)) && journalDiff === 0;
    if (p.kind === KIND.PurchaseInvoice) return !!p.vendorId && p.lines.length > 0 && p.lines.every((l) => l.description.trim() && l.quantity > 0 && l.unitPrice !== null && l.unitPrice >= 0 && !!l.accountId);
    if (p.kind === KIND.MenuPrice) return !!p.itemId && p.newPrice !== null && p.newPrice >= 0;
    return false;
  })();
  const total = p && (p.kind === KIND.SalesInvoice || p.kind === KIND.PurchaseInvoice) ? p.lines.reduce((s, l) => s + l.quantity * (l.unitPrice ?? 0), 0) : 0;

  async function run() {
    if (!p) return;
    setBusy(true); setError(null);
    try {
      const r = await AiAssistantApi.executeAction({
        kind: p.kind, customerId: p.customerId, lines: p.lines, paymentTerm: p.paymentTerm, note: p.note,
        invoiceId: p.invoiceId, amount: p.amount, method: p.method, itemId: p.itemId, newPrice: p.newPrice,
        vendorId: p.vendorId, partyName: p.partyName, partyPhone: p.partyPhone, partyEmail: p.partyEmail, journalLines: p.journalLines,
      });
      onExecuted(r.data.message, r.data.link);
    } catch (err) { setError(getErrorMessage(err)); } finally { setBusy(false); }
  }

  const pick = (list: { id: string; name: string; extra: string | null }[], value: string | null, onChange: (id: string, name: string) => void) => (
    <select value={value ?? ""} onChange={(e) => { const c = list.find((k) => k.id === e.target.value); if (c) onChange(c.id, c.name); }}>
      <option value="">{x.choose}</option>
      {list.map((c) => <option key={c.id} value={c.id}>{c.name}{c.extra ? ` (${c.extra})` : ""}</option>)}
    </select>
  );

  if (!p) return null;
  return (
        <div className="card" style={{ maxWidth: 760, marginTop: 8 }}>
          {error && <div className="alert-error" style={{ marginBottom: 8 }}>{error}</div>}
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

          {(p.kind === KIND.CustomerPayment || p.kind === KIND.VendorPayment) && (
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

          {p.kind === KIND.JournalEntry && (
            <div style={{ marginTop: 10 }}>
              <div className="form-field"><label>{x.note}</label><input value={p.note ?? ""} onChange={(e) => set({ note: e.target.value })} maxLength={200} /></div>
              {p.journalLines.map((l, i) => (
                <div key={i} style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 8, marginBottom: 6 }}>
                  <div style={{ gridColumn: "1 / -1" }}>{pick(p.accountCandidates, l.accountId, (id, name) => set({ journalLines: p.journalLines.map((k, n) => (n === i ? { ...k, accountId: id, accountName: name } : k)) }))}</div>
                  <input type="number" min={0} step="any" value={l.debit || ""} placeholder={x.debit} onChange={(e) => set({ journalLines: p.journalLines.map((k, n) => (n === i ? { ...k, debit: Number(e.target.value) || 0 } : k)) })} />
                  <input type="number" min={0} step="any" value={l.credit || ""} placeholder={x.credit2} onChange={(e) => set({ journalLines: p.journalLines.map((k, n) => (n === i ? { ...k, credit: Number(e.target.value) || 0 } : k)) })} />
                </div>
              ))}
              <button type="button" className="btn btn-secondary btn-sm" onClick={() => set({ journalLines: [...p.journalLines, { account: null, accountId: null, accountName: null, debit: 0, credit: 0, description: null }] })}>{x.addLine}</button>
              <div style={{ marginTop: 6 }}><strong>{x.diff}: {journalDiff}</strong></div>
            </div>
          )}

          {(p.kind === KIND.CreateCustomer || p.kind === KIND.CreateVendor) && (
            <div style={{ marginTop: 10 }}>
              <div className="form-grid">
                <div className="form-field"><label>{x.name}</label><input value={p.partyName ?? ""} maxLength={150} onChange={(e) => set({ partyName: e.target.value })} /></div>
                <div className="form-field"><label>{x.phone}</label><input value={p.partyPhone ?? ""} maxLength={40} onChange={(e) => set({ partyPhone: e.target.value })} /></div>
                <div className="form-field"><label>{x.email}</label><input type="email" value={p.partyEmail ?? ""} maxLength={120} onChange={(e) => set({ partyEmail: e.target.value })} /></div>
              </div>
              {p.similarParties.length > 0 && <div className="text-muted" style={{ marginTop: 6 }}>{x.similar}: {p.similarParties.map((s) => s.name).join("، ")}</div>}
            </div>
          )}

          {p.kind === KIND.PurchaseInvoice && (
            <div style={{ marginTop: 10 }}>
              <div className="form-field"><label>{x.vendor}</label>
                {p.vendorCandidates.length > 0 ? pick(p.vendorCandidates, p.vendorId, (id, name) => set({ vendorId: id, vendorName: name })) : <span className="text-muted">-</span>}
              </div>
              <label>{x.lines}</label>
              {p.lines.map((l, i) => (
                <div key={i} style={{ display: "grid", gridTemplateColumns: "2fr 1fr 1fr", gap: 8, marginBottom: 6 }}>
                  <input value={l.description} placeholder={x.desc} onChange={(e) => setLine(i, { description: e.target.value })} />
                  <input type="number" min={0} step="any" value={l.quantity} placeholder={x.qty} onChange={(e) => setLine(i, { quantity: Number(e.target.value) })} />
                  <input type="number" min={0} step="any" value={l.unitPrice ?? ""} placeholder={x.price} onChange={(e) => setLine(i, { unitPrice: e.target.value === "" ? null : Number(e.target.value) })} />
                  <div style={{ gridColumn: "1 / -1" }}>{pick(p.accountCandidates, l.accountId ?? null, (id) => setLine(i, { accountId: id }))}</div>
                </div>
              ))}
              <button type="button" className="btn btn-secondary btn-sm" onClick={() => set({ lines: [...p.lines, { description: "", quantity: 1, unitPrice: null, accountId: null }] })}>{x.addLine}</button>
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
              <button className="btn btn-secondary" disabled={busy} onClick={onCancel}>{x.cancel}</button>
            </div>
          )}
        </div>
  );
}

/** A conversation with the assistant. It looks things up in the books and prepares drafts; the owner approves any change before it is booked. */
export default function AssistantActions() {
  const { lang } = useLanguage();
  const x = TEXT[lang === "ar" ? "ar" : "en"];
  const [messages, setMessages] = useState<(AgentMessage & { drafts?: ActionProposal[] })[]>([]);
  const [text, setText] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [closed, setClosed] = useState<Record<string, boolean>>({});

  async function send(e?: React.FormEvent, override?: string) {
    e?.preventDefault();
    const q = (override ?? text).trim();
    if (!q || busy) return;
    const history = [...messages, { role: "user", content: q } as AgentMessage];
    setMessages(history); setText(""); setBusy(true); setError(null);
    try {
      const r = await AiAssistantApi.agentChat(history.map(({ role, content }) => ({ role, content })));
      setMessages([...history, { role: "assistant", content: r.data.reply, drafts: r.data.drafts }]);
    } catch (err) { setError(getErrorMessage(err)); } finally { setBusy(false); }
  }

  const executed = (message: string, link: string | null) =>
    setMessages((m) => [...m, { role: "assistant", content: `${x.done}: ${message}${link ? "" : ""}` }]);

  return (
    <div>
      <div className="page-header"><h1>{x.title}</h1></div>
      <div className="card" style={{ maxWidth: 760 }}>
        <p className="text-muted" style={{ marginTop: 0 }}>{x.intro}</p>
        {messages.length === 0 && (
          <div style={{ display: "flex", gap: 8, flexWrap: "wrap", marginBottom: 8 }}>
            {x.examples.map((ex) => <button key={ex} type="button" className="btn btn-secondary btn-sm" onClick={() => send(undefined, ex)}>{ex}</button>)}
          </div>
        )}
        {messages.map((m, i) => (
          <div key={i} style={{ margin: "10px 0" }}>
            <div style={{ fontSize: 12, opacity: 0.7 }}>{m.role === "user" ? x.you : x.assistant}</div>
            <div style={{ whiteSpace: "pre-wrap", padding: "8px 10px", borderRadius: 8, background: m.role === "user" ? "rgba(128,128,128,.12)" : "transparent", border: m.role === "user" ? "none" : "1px solid rgba(128,128,128,.3)" }}>{m.content}</div>
            {m.drafts?.map((d, k) => closed[`${i}-${k}`] ? null : (
              <ProposalCard key={k} initial={d} onCancel={() => setClosed((c) => ({ ...c, [`${i}-${k}`]: true }))}
                onExecuted={(msg, link) => { setClosed((c) => ({ ...c, [`${i}-${k}`]: true })); executed(msg, link); }} />
            ))}
          </div>
        ))}
        {busy && <p className="text-muted">{x.preparing}</p>}
        {error && <div className="alert-error" style={{ marginTop: 10 }}>{error}</div>}
        <form onSubmit={send} style={{ marginTop: 10 }}>
          <textarea rows={2} maxLength={1000} value={text} onChange={(e) => setText(e.target.value)} placeholder={x.placeholder} style={{ width: "100%" }}
            onKeyDown={(e) => { if (e.key === "Enter" && !e.shiftKey) { e.preventDefault(); send(); } }} />
          <div style={{ display: "flex", gap: 8, marginTop: 6 }}>
            <button className="btn" type="submit" disabled={busy || !text.trim()}>{x.send}</button>
            {messages.length > 0 && <button type="button" className="btn btn-secondary" onClick={() => { setMessages([]); setClosed({}); setError(null); }}>{x.another}</button>}
          </div>
        </form>
      </div>
    </div>
  );
}
