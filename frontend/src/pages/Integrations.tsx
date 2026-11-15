import { useEffect, useState } from "react";
import { IntegrationApi } from "../api/services";
import type { CreatedIntegrationApiKey, IntegrationApiKey, IntegrationInboxItem } from "../api/types";
import { getErrorMessage } from "../api/client";
import { useLanguage } from "../i18n/LanguageContext";

const TEXT = {
  ar: {
    title: "الربط مع البوت", intro: "أي بيانات يبعتها البوت (عملاء، فواتير، مصروفات...) بتوصل هنا وتستنى موافقتك. مفيش حاجة بتتسجل في الحسابات إلا بعد ما تعتمدها.",
    keys: "مفاتيح الربط", keyName: "اسم المفتاح (مثلًا: البوت المحاسبي)", createKey: "إنشاء مفتاح", revoke: "إلغاء", active: "شغال", revoked: "ملغي",
    lastUsed: "آخر استخدام", never: "لم يُستخدم", newKeyTitle: "انسخ المفتاح دلوقتي", newKeyNote: "المفتاح بيظهر مرة واحدة بس ومش هتقدر تشوفه تاني. لو ضاع، ألغيه واعمل واحد جديد.",
    copy: "نسخ", copied: "اتنسخ",
    connect: "بيانات الاتصال", baseUrl: "العنوان", companyCode: "كود الشركة (X-Company-Code)", keyHeader: "المفتاح (X-Api-Key)",
    types: "الأنواع المقبولة", example: "مثال",
    inbox: "اللي بعته البوت", pending: "في الانتظار", failed: "فشل", approved: "معتمد", rejected: "مرفوض", all: "الكل",
    approve: "اعتماد", reject: "رفض", view: "التفاصيل", hide: "إخفاء", approveAll: "اعتماد كل اللي في الانتظار",
    approveAllConfirm: "تعتمد كل اللي في الانتظار؟ هيتسجل في الحسابات.", rejectReason: "سبب الرفض (اختياري):",
    empty: "مفيش حاجة هنا.", type: "النوع", item: "العنصر", amount: "المبلغ", reference: "المرجع", received: "وصل", status: "الحالة", source: "المصدر",
    errorLabel: "سبب الفشل", reviewedBy: "راجعه", noKeys: "لسه مفيش مفاتيح.", done: "تم.",
    typeNames: { Customer: "عميل", Vendor: "مورد", SalesInvoice: "فاتورة مبيعات", PurchaseInvoice: "فاتورة مشتريات", SalesPayment: "دفعة من عميل", PurchasePayment: "دفعة لمورد", Expense: "مصروف", JournalEntry: "قيد يومية", Item: "صنف", BankStatementLine: "حركة بنك", Data: "بيانات أخرى" } as Record<string, string>,
  },
  en: {
    title: "Bot connection", intro: "Anything the bot sends (customers, invoices, expenses...) arrives here and waits for your approval. Nothing reaches the books until you approve it.",
    keys: "Connection keys", keyName: "Key name (e.g. Accounting bot)", createKey: "Create key", revoke: "Revoke", active: "Active", revoked: "Revoked",
    lastUsed: "Last used", never: "Never used", newKeyTitle: "Copy the key now", newKeyNote: "The key is shown once and cannot be read again. If it is lost, revoke it and create a new one.",
    copy: "Copy", copied: "Copied",
    connect: "Connection details", baseUrl: "Address", companyCode: "Company code (X-Company-Code)", keyHeader: "Key (X-Api-Key)",
    types: "Accepted types", example: "Example",
    inbox: "What the bot sent", pending: "Pending", failed: "Failed", approved: "Approved", rejected: "Rejected", all: "All",
    approve: "Approve", reject: "Reject", view: "Details", hide: "Hide", approveAll: "Approve everything pending",
    approveAllConfirm: "Approve everything pending? It will be posted to the books.", rejectReason: "Reason for rejecting (optional):",
    empty: "Nothing here.", type: "Type", item: "Item", amount: "Amount", reference: "Reference", received: "Received", status: "Status", source: "Source",
    errorLabel: "Why it failed", reviewedBy: "Reviewed by", noKeys: "No keys yet.", done: "Done.",
    typeNames: { Customer: "Customer", Vendor: "Vendor", SalesInvoice: "Sales invoice", PurchaseInvoice: "Purchase invoice", SalesPayment: "Customer payment", PurchasePayment: "Vendor payment", Expense: "Expense", JournalEntry: "Journal entry", Item: "Stock item", BankStatementLine: "Bank statement line", Data: "Other data" } as Record<string, string>,
  },
};

const FILTERS = ["Pending", "Failed", "Approved", "Rejected", ""] as const;
const money = (n: number | null) => (n == null ? "—" : n.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 }));

export default function Integrations() {
  const { lang } = useLanguage();
  const x = TEXT[lang === "ar" ? "ar" : "en"];
  const [keys, setKeys] = useState<IntegrationApiKey[]>([]);
  const [items, setItems] = useState<IntegrationInboxItem[]>([]);
  const [filter, setFilter] = useState<(typeof FILTERS)[number]>("Pending");
  const [name, setName] = useState("");
  const [created, setCreated] = useState<CreatedIntegrationApiKey | null>(null);
  const [open, setOpen] = useState<IntegrationInboxItem | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [copied, setCopied] = useState<string | null>(null);

  const companyCode = (() => { try { return localStorage.getItem("companyCode") ?? "YOUR-COMPANY"; } catch { return "YOUR-COMPANY"; } })();
  const baseUrl = `${window.location.origin}/api/integration`;
  const example = `curl -X POST ${baseUrl}/items \\
  -H "X-Company-Code: ${companyCode}" \\
  -H "X-Api-Key: ${created?.key ?? "rk_..."}" \\
  -H "Content-Type: application/json" \\
  -d '{"type":"Expense","externalRef":"bot-1001","data":{"date":"2026-11-15","description":"بنزين","amount":100}}'`;

  async function loadKeys() { try { setKeys((await IntegrationApi.getKeys()).data); } catch (e) { setError(getErrorMessage(e)); } }
  async function loadInbox(f = filter) { try { setItems((await IntegrationApi.getInbox(f || undefined)).data); } catch (e) { setError(getErrorMessage(e)); } }
  useEffect(() => { loadKeys(); }, []);
  useEffect(() => { loadInbox(filter); setOpen(null); /* eslint-disable-next-line react-hooks/exhaustive-deps */ }, [filter]);

  async function act(fn: () => Promise<unknown>, done = true) {
    setError(null); setMessage(null);
    try { await fn(); if (done) setMessage(x.done); await loadInbox(); } catch (e) { setError(getErrorMessage(e)); }
  }
  async function copy(text: string, id: string) {
    try { await navigator.clipboard.writeText(text); setCopied(id); setTimeout(() => setCopied(null), 1500); } catch { /* ignore */ }
  }
  async function show(item: IntegrationInboxItem) {
    if (open?.id === item.id) { setOpen(null); return; }
    try { setOpen((await IntegrationApi.getItem(item.id)).data); } catch (e) { setError(getErrorMessage(e)); }
  }
  const statusLabel = (s: string) => ({ Pending: x.pending, Failed: x.failed, Approved: x.approved, Rejected: x.rejected } as Record<string, string>)[s] ?? s;
  const pretty = (json?: string | null) => { try { return JSON.stringify(JSON.parse(json ?? "{}"), null, 2); } catch { return json ?? ""; } };

  return (
    <div style={{ maxWidth: 1100 }}>
      <div className="page-header"><h1>{x.title}</h1></div>
      <p className="text-muted">{x.intro}</p>
      {error && <div className="alert-error">{error}</div>}
      {message && <div className="card" style={{ marginBottom: 12 }}>{message}</div>}

      <div className="card" style={{ marginBottom: 16 }}>
        <h3 style={{ marginTop: 0 }}>{x.keys}</h3>
        <form style={{ display: "flex", gap: 8, flexWrap: "wrap", marginBottom: 12 }}
          onSubmit={async (e) => { e.preventDefault(); setError(null); try { setCreated((await IntegrationApi.createKey(name)).data); setName(""); await loadKeys(); } catch (err) { setError(getErrorMessage(err)); } }}>
          <input style={{ flex: 1, minWidth: 220 }} value={name} onChange={(e) => setName(e.target.value)} placeholder={x.keyName} required />
          <button className="btn" type="submit">{x.createKey}</button>
        </form>
        {created && (
          <div className="card" style={{ borderColor: "#c8862e", marginBottom: 12 }}>
            <strong>{x.newKeyTitle}</strong>
            <div style={{ display: "flex", gap: 8, alignItems: "center", marginTop: 8, flexWrap: "wrap" }}>
              <code style={{ direction: "ltr", wordBreak: "break-all" }}>{created.key}</code>
              <button className="btn btn-secondary btn-sm" onClick={() => copy(created.key, "key")}>{copied === "key" ? x.copied : x.copy}</button>
            </div>
            <p className="text-muted" style={{ marginBottom: 0 }}>{x.newKeyNote}</p>
          </div>
        )}
        {keys.length === 0 ? <p className="text-muted">{x.noKeys}</p> : (
          <div style={{ overflowX: "auto" }}>
            <table>
              <tbody>
                {keys.map((k) => (
                  <tr key={k.id} style={{ opacity: k.isActive ? 1 : 0.5 }}>
                    <td><strong>{k.name}</strong></td>
                    <td><code>{k.keyPrefix}…</code></td>
                    <td>{k.isActive ? x.active : x.revoked}</td>
                    <td className="text-muted">{x.lastUsed}: {k.lastUsedAtUtc ? new Date(k.lastUsedAtUtc).toLocaleString() : x.never}</td>
                    <td>{k.isActive && <button className="btn btn-secondary btn-sm" onClick={async () => { await IntegrationApi.revokeKey(k.id); loadKeys(); }}>{x.revoke}</button>}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      <div className="card" style={{ marginBottom: 16 }}>
        <h3 style={{ marginTop: 0 }}>{x.connect}</h3>
        <table><tbody>
          <tr><td>{x.baseUrl}</td><td><code style={{ direction: "ltr" }}>{baseUrl}</code></td></tr>
          <tr><td>{x.companyCode}</td><td><code>{companyCode}</code></td></tr>
          <tr><td>{x.keyHeader}</td><td><code>rk_…</code></td></tr>
          <tr><td>{x.types}</td><td><code style={{ direction: "ltr" }}>{Object.keys(x.typeNames).join(", ")}</code></td></tr>
        </tbody></table>
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginTop: 12 }}>
          <strong>{x.example}</strong>
          <button className="btn btn-secondary btn-sm" onClick={() => copy(example, "ex")}>{copied === "ex" ? x.copied : x.copy}</button>
        </div>
        <pre style={{ direction: "ltr", textAlign: "left", overflowX: "auto", background: "rgba(0,0,0,.06)", padding: 12, borderRadius: 8, fontSize: 12 }}>{example}</pre>
      </div>

      <div className="card">
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: 8 }}>
          <h3 style={{ margin: 0 }}>{x.inbox}</h3>
          <button className="btn btn-sm" onClick={() => { if (window.confirm(x.approveAllConfirm)) act(async () => { const r = (await IntegrationApi.approveAll()).data; setMessage(`${x.approved}: ${r.approved} · ${x.failed}: ${r.failed}`); }, false); }}>{x.approveAll}</button>
        </div>
        <div style={{ display: "flex", gap: 6, flexWrap: "wrap", margin: "12px 0" }}>
          {FILTERS.map((f) => (
            <button key={f || "all"} className={filter === f ? "btn btn-sm" : "btn btn-secondary btn-sm"} onClick={() => setFilter(f)}>{f ? statusLabel(f) : x.all}</button>
          ))}
        </div>
        {items.length === 0 ? <p className="text-muted">{x.empty}</p> : (
          <div style={{ overflowX: "auto" }}>
            <table>
              <thead><tr><th>{x.type}</th><th>{x.item}</th><th>{x.amount}</th><th>{x.reference}</th><th>{x.received}</th><th>{x.status}</th><th></th></tr></thead>
              <tbody>
                {items.map((i) => (
                  <>
                    <tr key={i.id}>
                      <td>{x.typeNames[i.type] ?? i.type}</td>
                      <td>{i.title}{i.errorMessage && <div style={{ color: "#c0392b", fontSize: 12 }}>{x.errorLabel}: {i.errorMessage}</div>}</td>
                      <td>{money(i.amount)}</td>
                      <td><code>{i.externalRef}</code></td>
                      <td>{new Date(i.receivedAtUtc).toLocaleString()}</td>
                      <td><span className="badge">{statusLabel(i.status)}</span></td>
                      <td style={{ whiteSpace: "nowrap" }}>
                        <button className="btn btn-secondary btn-sm" onClick={() => show(i)}>{open?.id === i.id ? x.hide : x.view}</button>{" "}
                        {(i.status === "Pending" || i.status === "Failed") && (<>
                          <button className="btn btn-sm" onClick={() => act(() => IntegrationApi.approve(i.id))}>{x.approve}</button>{" "}
                          <button className="btn btn-secondary btn-sm" onClick={() => { const r = window.prompt(x.rejectReason); if (r !== null) act(() => IntegrationApi.reject(i.id, r)); }}>{x.reject}</button>
                        </>)}
                      </td>
                    </tr>
                    {open?.id === i.id && (
                      <tr key={i.id + "-d"}><td colSpan={7}>
                        <pre style={{ direction: "ltr", textAlign: "left", overflowX: "auto", background: "rgba(0,0,0,.06)", padding: 12, borderRadius: 8, fontSize: 12, margin: 0 }}>{pretty(open.payloadJson)}</pre>
                        {open.reviewedBy && <div className="text-muted" style={{ fontSize: 12, marginTop: 6 }}>{x.reviewedBy}: {open.reviewedBy}{open.reviewNote ? ` — ${open.reviewNote}` : ""}</div>}
                      </td></tr>
                    )}
                  </>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}
