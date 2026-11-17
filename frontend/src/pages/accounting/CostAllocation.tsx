import { useState } from "react";
import { apiClient, getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";

const TEXT = {
  ar: { title: "توزيع المصروفات المشتركة على الفروع", intro: "آخر الشهر: المصروفات اللي مش تابعة لفرع (إيجار المكتب الرئيسي، مرتبات الإدارة...) بتتوزع على الفروع بنسبة مبيعات كل فرع، في قيد واحد. نفس الحسابات، ومفيش حساب جديد، وإجمالي المصروفات مبيتغيرش. مفيش حاجة بتتنفذ من غير اعتمادك، وتقدر تعدل النسب.",
    year: "السنة", month: "الشهر", show: "عرض المقترح", account: "الحساب", amount: "المبلغ على الإدارة", total: "الإجمالي", branch: "الفرع", sales: "مبيعات الشهر", share: "النسبة %", share_amount: "نصيبه",
    sum: "مجموع النسب", mustBe100: "لازم يكون 100", note: "ملاحظة", apply: "اعتماد وتنفيذ التوزيع", applied: "اتعمل قيد التوزيع.", nothing: "مفيش حاجة تتوزع.", accounts: "المصروفات المشتركة" },
  en: { title: "Share head-office costs with the branches", intro: "At month end, costs that belong to no branch (head-office rent, office payroll...) are shared out to the branches on each one's share of sales, in one entry. Same accounts, no new account, total expenses unchanged. Nothing posts without your approval, and you can edit the shares.",
    year: "Year", month: "Month", show: "Show proposal", account: "Account", amount: "Amount on head office", total: "Total", branch: "Branch", sales: "Sales in the month", share: "Share %", share_amount: "Its part",
    sum: "Shares total", mustBe100: "must be 100", note: "Note", apply: "Approve and post the allocation", applied: "Allocation entry posted.", nothing: "Nothing to share out.", accounts: "Shared costs" },
};
type Account = { accountId: string; accountCode: string; accountName: string; amount: number };
type Branch = { warehouseId: string; warehouseName: string; sales: number; sharePercent: number };
type Preview = { accounts: Account[]; totalToAllocate: number; branches: Branch[]; blocked?: string | null };
const money = (n: number) => n.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });

export default function CostAllocation() {
  const { lang } = useLanguage();
  const x = TEXT[lang === "ar" ? "ar" : "en"];
  const now = new Date();
  const [year, setYear] = useState(now.getFullYear());
  const [month, setMonth] = useState(now.getMonth() + 1);
  const [preview, setPreview] = useState<Preview | null>(null);
  const [shares, setShares] = useState<Record<string, string>>({});
  const [note, setNote] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  async function load() {
    setError(null); setMessage(null);
    try {
      const p = (await apiClient.get<Preview>("/cost-allocation/preview", { params: { year, month } })).data;
      setPreview(p); setShares(Object.fromEntries(p.branches.map((b) => [b.warehouseId, String(b.sharePercent)])));
    } catch (e) { setError(getErrorMessage(e)); }
  }
  const sumShares = preview ? preview.branches.reduce((s, b) => s + (Number(shares[b.warehouseId]) || 0), 0) : 0;

  async function apply() {
    if (!preview) return;
    setError(null); setMessage(null);
    try {
      await apiClient.post("/cost-allocation/apply", { year, month, note: note || undefined, shares: preview.branches.map((b) => ({ warehouseId: b.warehouseId, sharePercent: Number(shares[b.warehouseId]) || 0 })) });
      setMessage(x.applied); await load();
    } catch (e) { setError(getErrorMessage(e)); }
  }

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
      {message && <div className="card" style={{ marginBottom: 12 }}>{message}</div>}

      {preview && (preview.blocked ? <div className="card text-muted">{preview.blocked}</div> : (
        <>
          <h3>{x.accounts}</h3>
          <div className="card" style={{ overflowX: "auto" }}>
            <table>
              <thead><tr><th>{x.account}</th><th>{x.amount}</th></tr></thead>
              <tbody>
                {preview.accounts.map((a) => <tr key={a.accountId}><td>{a.accountCode} - {a.accountName}</td><td>{money(a.amount)}</td></tr>)}
                <tr style={{ fontWeight: 700 }}><td>{x.total}</td><td>{money(preview.totalToAllocate)}</td></tr>
              </tbody>
            </table>
          </div>
          <h3 style={{ marginTop: 20 }}>{x.branch}</h3>
          <div className="card" style={{ overflowX: "auto" }}>
            <table>
              <thead><tr><th>{x.branch}</th><th>{x.sales}</th><th>{x.share}</th><th>{x.share_amount}</th></tr></thead>
              <tbody>
                {preview.branches.map((b) => (
                  <tr key={b.warehouseId}>
                    <td>{b.warehouseName}</td><td>{money(b.sales)}</td>
                    <td><input type="number" min="0" step="0.01" style={{ width: 90 }} value={shares[b.warehouseId] ?? ""} onChange={(e) => setShares({ ...shares, [b.warehouseId]: e.target.value })} /></td>
                    <td>{money(preview.totalToAllocate * (Number(shares[b.warehouseId]) || 0) / 100)}</td>
                  </tr>
                ))}
                <tr style={{ fontWeight: 700 }}><td colSpan={2}>{x.sum}</td><td className={Math.abs(sumShares - 100) > 0.01 ? "text-danger" : ""}>{sumShares.toFixed(2)}{Math.abs(sumShares - 100) > 0.01 ? ` (${x.mustBe100})` : ""}</td><td></td></tr>
              </tbody>
            </table>
            <div className="form-field" style={{ marginTop: 8 }}><label>{x.note}</label><input value={note} onChange={(e) => setNote(e.target.value)} maxLength={200} /></div>
            <button className="btn" onClick={apply} disabled={Math.abs(sumShares - 100) > 0.01}>{x.apply}</button>
          </div>
        </>
      ))}
    </div>
  );
}
