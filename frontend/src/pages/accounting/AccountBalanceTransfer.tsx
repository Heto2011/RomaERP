import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { apiClient, getErrorMessage } from "../../api/client";
import { AccountsApi } from "../../api/services";
import { useLanguage } from "../../i18n/LanguageContext";
import type { Account } from "../../api/types";

const TEXT = {
  ar: {
    title: "نقل رصيد حساب قديم لحساب جديد", back: "رجوع لشجرة الحسابات",
    intro: "بعد ما تغيّر شجرة الحسابات، الحساب القديم ممكن يفضل عليه رصيد. هنا بتنقله للحساب الجديد بقيد تسوية واحد. القيود الأصلية بتفضل زي ما هي، ورصيد الحساب القديم بيبقى صفر. مفيش حاجة بتتسجّل غير بعد اعتمادك.",
    scope: "بيشتغل مع حسابات الأصول والخصوم وحقوق الملكية، من نفس النوع. الإيرادات والمصروفات بتتنقل بقيد تسوية من المحاسب لأنها بتأثر على أرباح الفترة.",
    from: "الحساب القديم", to: "الحساب الجديد", date: "التاريخ", pick: "اختار...", show: "عرض القيد المقترح", approve: "اعتماد وتسجيل القيد",
    balance: "رصيد الحساب القديم", amount: "المبلغ اللي هيتنقل", debit: "مدين", credit: "دائن", done: "اتسجّل قيد النقل رقم",
    confirm: "هيتسجّل قيد تسوية بالمبلغ ده. تأكيد؟",
  },
  en: {
    title: "Move an old account's balance to a new account", back: "Back to the chart of accounts",
    intro: "After changing the chart, the old account may still hold a balance. Here you move it to the new account with one adjustment entry. The original entries stay as they are and the old account ends at zero. Nothing is posted until you approve.",
    scope: "Works for asset, liability and equity accounts of the same type. Income and expense balances are moved by an accountant's adjustment entry because they affect the period's profit.",
    from: "Old account", to: "New account", date: "Date", pick: "Choose...", show: "Show the proposed entry", approve: "Approve and post the entry",
    balance: "Old account balance", amount: "Amount to move", debit: "Debit", credit: "Credit", done: "Transfer entry posted:",
    confirm: "An adjustment entry for this amount will be posted. Confirm?",
  },
};

type Preview = { fromCode: string; fromName: string; toCode: string; toName: string; balance: number; amount: number; debitAccount: string; creditAccount: string; description: string; canApply: boolean; blocked: string | null };

export default function AccountBalanceTransfer() {
  const { lang } = useLanguage();
  const x = TEXT[lang === "ar" ? "ar" : "en"];
  const [accounts, setAccounts] = useState<Account[]>([]);
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [date, setDate] = useState(new Date().toISOString().slice(0, 10));
  const [preview, setPreview] = useState<Preview | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  useEffect(() => { AccountsApi.getAll().then((r) => setAccounts(r.data)).catch((e) => setError(getErrorMessage(e))); }, []);
  const body = () => ({ fromAccountId: from, toAccountId: to, date });
  const label = (a: Account) => `${a.code} - ${lang === "ar" ? a.nameAr : a.nameEn || a.nameAr}${a.isActive ? "" : " (inactive)"}`;

  async function show() {
    setError(null); setMessage(null); setPreview(null);
    try { setPreview((await apiClient.post<Preview>("/accounts/transfer-balance/preview", body())).data); }
    catch (e) { setError(getErrorMessage(e)); }
  }
  async function approve() {
    if (!preview?.canApply || !window.confirm(x.confirm)) return;
    setError(null);
    try {
      const res = (await apiClient.post<{ entryNumber: string }>("/accounts/transfer-balance/apply", body())).data;
      setMessage(`${x.done} ${res.entryNumber}`); setPreview(null);
    } catch (e) { setError(getErrorMessage(e)); }
  }

  const options = accounts.filter((a) => a.accountType <= 3);
  return (
    <div>
      <div className="page-header"><h1>{x.title}</h1><Link className="btn btn-secondary" to="/accounting/chart-of-accounts">{x.back}</Link></div>
      <p className="text-muted">{x.intro}</p>
      <p className="text-muted">{x.scope}</p>
      <div className="card" style={{ display: "flex", gap: 10, flexWrap: "wrap", alignItems: "end" }}>
        <div className="form-field"><label>{x.from}</label>
          <select value={from} onChange={(e) => { setFrom(e.target.value); setPreview(null); }}><option value="">{x.pick}</option>{options.map((a) => <option key={a.id} value={a.id}>{label(a)}</option>)}</select></div>
        <div className="form-field"><label>{x.to}</label>
          <select value={to} onChange={(e) => { setTo(e.target.value); setPreview(null); }}><option value="">{x.pick}</option>{options.filter((a) => a.isActive).map((a) => <option key={a.id} value={a.id}>{label(a)}</option>)}</select></div>
        <div className="form-field"><label>{x.date}</label><input type="date" value={date} onChange={(e) => { setDate(e.target.value); setPreview(null); }} /></div>
        <button className="btn" onClick={show} disabled={!from || !to}>{x.show}</button>
      </div>
      {error && <div className="alert-error">{error}</div>}
      {message && <div className="card">{message}</div>}
      {preview && (preview.blocked ? <div className="card text-muted">{preview.blocked}</div> : (
        <div className="card">
          <div>{x.balance}: <strong>{preview.balance.toLocaleString(undefined, { minimumFractionDigits: 2 })}</strong> · {x.amount}: <strong>{preview.amount.toLocaleString(undefined, { minimumFractionDigits: 2 })}</strong></div>
          <table style={{ marginTop: 10 }}><tbody>
            <tr><td>{x.debit}</td><td>{preview.debitAccount}</td></tr>
            <tr><td>{x.credit}</td><td>{preview.creditAccount}</td></tr>
          </tbody></table>
          <p className="text-muted">{preview.description}</p>
          <button className="btn" onClick={approve}>{x.approve}</button>
        </div>
      ))}
    </div>
  );
}
