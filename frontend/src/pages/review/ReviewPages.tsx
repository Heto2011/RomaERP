import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { apiClient, getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";

const money = (n: number) => n.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const useText = <T,>(ar: T, en: T): T => (useLanguage().lang === "ar" ? ar : en);

function usePageData<T>(url: string, params?: Record<string, unknown>, deps: unknown[] = []) {
  const [data, setData] = useState<T | null>(null);
  const [error, setError] = useState<string | null>(null);
  useEffect(() => {
    setError(null);
    apiClient.get<T>(url, { params }).then((r) => setData(r.data)).catch((e) => setError(getErrorMessage(e)));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, deps);
  return { data, error };
}

// ---------------- 1. ledger against the detail behind it ----------------
type Recon = { key: string; ledgerLabel: string; detailLabel: string; ledgerBalance: number; detailBalance: number; difference: number; ok: boolean };
export function ReconciliationPage() {
  const x = useText(
    { title: "تسوية الحسابات مع التفاصيل", intro: "بتقارن كل رصيد في الدفاتر بالتفاصيل اللي وراه. أي فرق أكتر من جنيه واحد بيظهر أحمر، ومعناه إن في حاجة ما اتسجلتش صح.",
      check: "الفحص", ledger: "رصيد الدفاتر", detail: "رصيد التفاصيل", diff: "الفرق", status: "الحالة", ok: "مطابق", bad: "في فرق",
      names: { trial: "ميزان المراجعة: إجمالي المدين مقابل الدائن", ar: "العملاء (1120) مقابل أرصدة العملاء", ap: "الموردون (2120) مقابل أرصدة الموردين", inventory: "المخزون (1160) مقابل الكمية × التكلفة", custody: "عهد وسلف الموظفين (1170) مقابل العهد والمديونيات" } as Record<string, string> },
    { title: "Ledger against its detail", intro: "Compares each balance in the books with the detail behind it. A difference over 1.00 shows red: something was not recorded properly.",
      check: "Check", ledger: "Ledger", detail: "Detail", diff: "Difference", status: "Status", ok: "Matches", bad: "Differs",
      names: { trial: "Trial balance: total debit against credit", ar: "Receivables (1120) against customer balances", ap: "Payables (2120) against vendor balances", inventory: "Inventory (1160) against quantity × cost", custody: "Employee custody (1170) against custody and debts" } as Record<string, string> });
  const { data, error } = usePageData<Recon[]>("/review/reconciliation");
  return (
    <div>
      <div className="page-header"><h1>{x.title}</h1></div>
      <p className="text-muted">{x.intro}</p>
      {error && <div className="alert-error">{error}</div>}
      {data && (
        <div className="card" style={{ overflowX: "auto" }}>
          <table>
            <thead><tr><th>{x.check}</th><th>{x.ledger}</th><th>{x.detail}</th><th>{x.diff}</th><th>{x.status}</th></tr></thead>
            <tbody>
              {data.map((c) => (
                <tr key={c.key}>
                  <td>{x.names[c.key] ?? c.key}</td><td>{money(c.ledgerBalance)}</td><td>{money(c.detailBalance)}</td>
                  <td className={c.ok ? "" : "text-danger"}>{money(c.difference)}</td>
                  <td><span className="badge" style={c.ok ? undefined : { background: "#b42318", color: "#fff" }}>{c.ok ? x.ok : x.bad}</span></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

// ---------------- 2. month-end checklist ----------------
type Check = { key: string; ok: boolean; count: number; amount: number; link: string };
export function CloseChecklistPage() {
  const now = new Date();
  const [year, setYear] = useState(now.getFullYear());
  const [month, setMonth] = useState(now.getMonth() + 1);
  const x = useText(
    { title: "قائمة إقفال الشهر", intro: "قبل ما تقفل الفترة، النظام بيفحص إن مفيش حاجة ناقصة. كل بند «تمام» أو «محتاج إجراء» برابط ليه.", year: "السنة", month: "الشهر", show: "عرض", ok: "تمام", todo: "محتاج إجراء", open: "افتح",
      ready: "كل البنود تمام: الشهر جاهز للإقفال.", notReady: "في بنود محتاجة إجراء قبل الإقفال.",
      names: { drafts: "قيود مسودة لسه ما اتحطتش", counts: "فروقات جرد منتظرة اعتماد", pendingPurchases: "مشتريات منتظرة اعتماد", payroll: "دورة رواتب مش مرحّلة في الشهر", transfers: "تحويلات بين الفروع لسه في الطريق", depreciation: "الإهلاك ما اتشغّلش للشهر", sharedCosts: "مصروفات مشتركة لسه ما اتوزعتش على الفروع", trialBalanced: "ميزان المراجعة متوازن" } as Record<string, string> },
    { title: "Month-end close checklist", intro: "Before closing the period, the system checks nothing is left open. Each item is fine or needs action, with a link.", year: "Year", month: "Month", show: "Show", ok: "Done", todo: "Needs action", open: "Open",
      ready: "Everything is done: the month is ready to close.", notReady: "Some items need action before closing.",
      names: { drafts: "Draft journal entries not posted", counts: "Count differences waiting approval", pendingPurchases: "Purchases waiting approval", payroll: "Payroll run not posted for the month", transfers: "Branch transfers still in transit", depreciation: "Depreciation not run for the month", sharedCosts: "Shared costs not yet allocated to branches", trialBalanced: "Trial balance is balanced" } as Record<string, string> });
  const [data, setData] = useState<Check[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  async function load() { setError(null); try { setData((await apiClient.get<Check[]>("/review/close-checklist", { params: { year, month } })).data); } catch (e) { setError(getErrorMessage(e)); } }
  useEffect(() => { load(); /* eslint-disable-next-line react-hooks/exhaustive-deps */ }, []);
  const allOk = data?.every((c) => c.ok) ?? false;
  return (
    <div>
      <div className="page-header"><h1>{x.title}</h1></div>
      <p className="text-muted">{x.intro}</p>
      <div className="card toolbar" style={{ display: "flex", gap: 8, alignItems: "end" }}>
        <div className="form-field"><label>{x.year}</label><input type="number" value={year} onChange={(e) => setYear(Number(e.target.value))} style={{ width: 90 }} /></div>
        <div className="form-field"><label>{x.month}</label><input type="number" min={1} max={12} value={month} onChange={(e) => setMonth(Number(e.target.value))} style={{ width: 70 }} /></div>
        <button className="btn" onClick={load}>{x.show}</button>
      </div>
      {error && <div className="alert-error">{error}</div>}
      {data && (
        <>
          <div className="card" style={{ marginBottom: 12, fontWeight: 700 }} >{allOk ? `✓ ${x.ready}` : `⚠ ${x.notReady}`}</div>
          <div className="card" style={{ overflowX: "auto" }}>
            <table>
              <tbody>
                {data.map((c) => (
                  <tr key={c.key}>
                    <td style={{ width: 28 }}>{c.ok ? "✓" : "⚠"}</td>
                    <td>{x.names[c.key] ?? c.key}{!c.ok && c.count > 0 ? ` (${c.count})` : ""}{!c.ok && c.amount ? ` — ${money(c.amount)}` : ""}</td>
                    <td><span className="badge" style={c.ok ? undefined : { background: "#c8862e", color: "#fff" }}>{c.ok ? x.ok : x.todo}</span></td>
                    <td>{!c.ok && <Link to={c.link}>{x.open}</Link>}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      )}
    </div>
  );
}

// ---------------- 3. approvals log ----------------
type Approval = { kind: string; detail: string; decision: string; by?: string | null; atUtc: string; amount?: number | null; note?: string | null };
export function ApprovalsLogPage() {
  const x = useText(
    { title: "سجل الاعتمادات", intro: "كل حاجة اتعتمدت أو اترفضت في النظام: مين، وإمتى، وإيه القرار.", when: "الوقت", what: "النوع", detail: "التفاصيل", decision: "القرار", by: "بواسطة", amount: "المبلغ", note: "ملاحظة", approved: "اعتمد", rejected: "رفض", empty: "مفيش اعتمادات لسه.",
      kinds: { StockCount: "فرق جرد", PayrollDebtDeduction: "خصم مديونية من راتب", PendingPurchase: "فاتورة مشتريات", CostAllocation: "توزيع مصروفات مشتركة" } as Record<string, string> },
    { title: "Approvals log", intro: "Everything approved or rejected in the system: who, when and the decision.", when: "When", what: "Type", detail: "Details", decision: "Decision", by: "By", amount: "Amount", note: "Note", approved: "Approved", rejected: "Rejected", empty: "No approvals yet.",
      kinds: { StockCount: "Count difference", PayrollDebtDeduction: "Debt deduction from pay", PendingPurchase: "Purchase invoice", CostAllocation: "Shared-cost allocation" } as Record<string, string> });
  const { data, error } = usePageData<Approval[]>("/review/approvals");
  return (
    <div>
      <div className="page-header"><h1>{x.title}</h1></div>
      <p className="text-muted">{x.intro}</p>
      {error && <div className="alert-error">{error}</div>}
      {data && (data.length === 0 ? <div className="card text-muted">{x.empty}</div> : (
        <div className="card" style={{ overflowX: "auto" }}>
          <table>
            <thead><tr><th>{x.when}</th><th>{x.what}</th><th>{x.detail}</th><th>{x.amount}</th><th>{x.decision}</th><th>{x.by}</th><th>{x.note}</th></tr></thead>
            <tbody>
              {data.map((a, i) => (
                <tr key={i}>
                  <td>{new Date(a.atUtc).toLocaleString()}</td><td>{x.kinds[a.kind] ?? a.kind}</td><td>{a.detail}</td><td>{a.amount != null ? money(a.amount) : "—"}</td>
                  <td><span className="badge" style={a.decision === "Rejected" ? { background: "#b42318", color: "#fff" } : undefined}>{a.decision === "Rejected" ? x.rejected : x.approved}</span></td>
                  <td>{a.by ?? "—"}</td><td className="text-muted">{a.note ?? ""}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ))}
    </div>
  );
}

// ---------------- 4. entries that stand out ----------------
type Unusual = { entryId: string; entryNumber: string; entryDate: string; createdAtUtc: string; createdBy?: string | null; description?: string | null; total: number; reasons: string[] };
export function UnusualEntriesPage() {
  const [from, setFrom] = useState(() => new Date(Date.now() - 90 * 86400000).toISOString().slice(0, 10));
  const [to, setTo] = useState(() => new Date().toISOString().slice(0, 10));
  const x = useText(
    { title: "القيود غير المعتادة", intro: "قيود تستاهل نظرة المراجع: مبالغ مدورة كبيرة، قيد كبير جدًا عن المعتاد، قيد بتاريخ قديم اتسجل متأخر، قيد معكوس، أو قيد على حقوق الملكية. ده تنبيه للمراجعة مش اتهام.",
      from: "من", to: "إلى", show: "عرض", number: "رقم القيد", date: "التاريخ", entered: "اتسجل", by: "بواسطة", desc: "الوصف", total: "الإجمالي", why: "السبب", empty: "مفيش قيود غير معتادة في الفترة دي.",
      reasons: { Round: "مبلغ مدوّر كبير", Large: "كبير جدًا عن المعتاد", Backdated: "تاريخ قديم اتسجل متأخر", Reversal: "قيد معكوس", Equity: "على حقوق الملكية" } as Record<string, string> },
    { title: "Unusual entries", intro: "Entries worth a reviewer's look: large round amounts, very large entries, old-dated entries recorded late, reversals, or entries on equity. A prompt to review, not an accusation.",
      from: "From", to: "To", show: "Show", number: "Entry", date: "Date", entered: "Recorded", by: "By", desc: "Description", total: "Total", why: "Why", empty: "No unusual entries in this period.",
      reasons: { Round: "Large round amount", Large: "Far above usual", Backdated: "Old date, recorded late", Reversal: "Reversal", Equity: "On equity" } as Record<string, string> });
  const [data, setData] = useState<Unusual[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  async function load() { setError(null); try { setData((await apiClient.get<Unusual[]>("/review/unusual-entries", { params: { from, to } })).data); } catch (e) { setError(getErrorMessage(e)); } }
  useEffect(() => { load(); /* eslint-disable-next-line react-hooks/exhaustive-deps */ }, []);
  return (
    <div>
      <div className="page-header"><h1>{x.title}</h1></div>
      <p className="text-muted">{x.intro}</p>
      <div className="card toolbar" style={{ display: "flex", gap: 8, alignItems: "end" }}>
        <div className="form-field"><label>{x.from}</label><input type="date" value={from} onChange={(e) => setFrom(e.target.value)} /></div>
        <div className="form-field"><label>{x.to}</label><input type="date" value={to} onChange={(e) => setTo(e.target.value)} /></div>
        <button className="btn" onClick={load}>{x.show}</button>
      </div>
      {error && <div className="alert-error">{error}</div>}
      {data && (data.length === 0 ? <div className="card text-muted">{x.empty}</div> : (
        <div className="card" style={{ overflowX: "auto" }}>
          <table>
            <thead><tr><th>{x.number}</th><th>{x.date}</th><th>{x.entered}</th><th>{x.by}</th><th>{x.desc}</th><th>{x.total}</th><th>{x.why}</th></tr></thead>
            <tbody>
              {data.map((e) => (
                <tr key={e.entryId}>
                  <td>{e.entryNumber}</td><td>{e.entryDate.slice(0, 10)}</td><td>{e.createdAtUtc.slice(0, 10)}</td><td>{e.createdBy ?? "—"}</td><td>{e.description ?? ""}</td><td>{money(e.total)}</td>
                  <td>{e.reasons.map((r) => <span key={r} className="badge" style={{ marginInlineEnd: 4 }}>{x.reasons[r] ?? r}</span>)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ))}
    </div>
  );
}

// ---------------- 5. numbering gaps ----------------
type Gap = { series: string; count: number; first: number; last: number; missingCount: number; missing: number[] };
export function NumberGapsPage() {
  const x = useText(
    { title: "ثغرات الترقيم", intro: "بتكشف لو رقم ناقص من تسلسل الفواتير أو القيود (مثلًا 105 ثم 107). الأرقام الناقصة ممكن تكون محذوفة أو اتعملت في اختبار، والمراجعة الضريبية بتسأل عنها.",
      series: "التسلسل", count: "العدد", first: "أول رقم", last: "آخر رقم", missing: "ناقص", none: "مفيش ثغرات", more: "وغيرهم",
      names: { SalesInvoice: "فواتير المبيعات", PurchaseInvoice: "فواتير المشتريات", JournalEntry: "القيود" } as Record<string, string> },
    { title: "Numbering gaps", intro: "Shows where a number is missing from the invoice or entry sequence (e.g. 105 then 107). Missing numbers may be deleted or test documents, and tax reviews ask about them.",
      series: "Sequence", count: "Count", first: "First", last: "Last", missing: "Missing", none: "No gaps", more: "and more",
      names: { SalesInvoice: "Sales invoices", PurchaseInvoice: "Purchase invoices", JournalEntry: "Journal entries" } as Record<string, string> });
  const { data, error } = usePageData<Gap[]>("/review/number-gaps");
  const label = (s: string) => { const [name, ...rest] = s.split(" "); return `${x.names[name] ?? name} ${rest.join(" ")}`.trim(); };
  return (
    <div>
      <div className="page-header"><h1>{x.title}</h1></div>
      <p className="text-muted">{x.intro}</p>
      {error && <div className="alert-error">{error}</div>}
      {data && (
        <div className="card" style={{ overflowX: "auto" }}>
          <table>
            <thead><tr><th>{x.series}</th><th>{x.count}</th><th>{x.first}</th><th>{x.last}</th><th>{x.missing}</th></tr></thead>
            <tbody>
              {data.map((g) => (
                <tr key={g.series}>
                  <td>{label(g.series)}</td><td>{g.count}</td><td>{g.first}</td><td>{g.last}</td>
                  <td className={g.missingCount ? "text-danger" : ""}>{g.missingCount === 0 ? x.none : `${g.missingCount}: ${g.missing.slice(0, 30).join(", ")}${g.missingCount > 30 ? ` ${x.more}` : ""}`}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
