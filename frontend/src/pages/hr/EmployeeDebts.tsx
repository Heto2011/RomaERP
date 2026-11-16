import { useEffect, useState } from "react";
import { EmployeeDebtsApi, EmployeesApi } from "../../api/services";
import type { Employee } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useAuth } from "../../context/AuthContext";
import { useLanguage } from "../../i18n/LanguageContext";

const TEXT = {
  ar: { title: "مديونيات الموظفين", intro: "كل اللي على الموظفين للشركة في مكان واحد: سلف، نقص مخزون اتحمّل عليهم. بتتسدد من الراتب بأقساط، وكل خصم بيتعرض عليك تعتمده في دورة الرواتب.",
    add: "سلفة جديدة", employee: "الموظف", amount: "المبلغ", date: "التاريخ", note: "ملاحظة", installment: "القسط الشهري", installmentHint: "سيبه فاضي = يتخصم كله في أول دورة",
    paidFrom: "اتدفعت من", cash: "الخزينة", bank: "البنك", save: "تسجيل السلفة", saved: "تم تسجيل السلفة.", reason: "السبب", remaining: "المتبقي", status: "الحالة",
    advance: "سلفة", shortage: "نقص مخزون", other: "أخرى", active: "نشطة", settled: "مسدّدة", cancelled: "ملغية", choose: "اختار", edit: "تعديل القسط", cancel: "إلغاء المديونية",
    cancelConfirm: "تلغي المديونية دي؟", empty: "مفيش مديونيات.", by: "سجّلها", onlyApprover: "الصفحة دي للمالك أو المحاسب بس.", newInstallment: "القسط الشهري الجديد:" },
  en: { title: "Employee debts", intro: "Everything employees owe the company in one place: advances and stock shortages charged to them. Repaid from pay in instalments; each deduction is put to you to approve in the payroll run.",
    add: "New advance", employee: "Employee", amount: "Amount", date: "Date", note: "Note", installment: "Monthly instalment", installmentHint: "Empty = taken in full at the first run",
    paidFrom: "Paid from", cash: "Cash", bank: "Bank", save: "Record advance", saved: "Advance recorded.", reason: "Reason", remaining: "Remaining", status: "Status",
    advance: "Advance", shortage: "Stock shortage", other: "Other", active: "Active", settled: "Settled", cancelled: "Cancelled", choose: "Choose", edit: "Edit instalment", cancel: "Cancel debt",
    cancelConfirm: "Cancel this debt?", empty: "No debts.", by: "By", onlyApprover: "This page is for the owner or accountant only.", newInstallment: "New monthly instalment:" },
};
type Debt = { id: string; employeeName: string; reason: number; description?: string; debtDate: string; amount: number; remainingAmount: number; monthlyInstallment?: number | null; status: number; createdByName?: string };

export default function EmployeeDebts() {
  const { lang } = useLanguage();
  const { user } = useAuth();
  const x = TEXT[lang === "ar" ? "ar" : "en"];
  const allowed = !!user && (user.roles.includes("Admin") || user.roles.includes("Accountant"));
  const [debts, setDebts] = useState<Debt[]>([]);
  const [employees, setEmployees] = useState<Employee[]>([]);
  const [open, setOpen] = useState(false);
  const [employeeId, setEmployeeId] = useState("");
  const [amount, setAmount] = useState("");
  const [date, setDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [description, setDescription] = useState("");
  const [installment, setInstallment] = useState("");
  const [paidFrom, setPaidFrom] = useState("cash");
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  async function load() { try { setDebts((await EmployeeDebtsApi.getAll()).data as Debt[]); } catch (e) { setError(getErrorMessage(e)); } }
  useEffect(() => { if (allowed) { load(); EmployeesApi.getAll().then((r) => setEmployees(r.data)).catch(() => setEmployees([])); } /* eslint-disable-next-line react-hooks/exhaustive-deps */ }, [allowed]);
  if (!allowed) return <div className="card text-muted">{x.onlyApprover}</div>;

  const reasonLabel = (r: number) => (r === 1 ? x.advance : r === 2 ? x.shortage : x.other);
  const statusLabel = (s: number) => (s === 1 ? x.active : s === 2 ? x.settled : x.cancelled);

  async function submit(e: React.FormEvent) {
    e.preventDefault(); setError(null); setMessage(null);
    try {
      await EmployeeDebtsApi.createAdvance({ employeeId, amount: Number(amount), debtDate: date, description: description || undefined, monthlyInstallment: installment ? Number(installment) : undefined, paidFrom });
      setMessage(x.saved); setOpen(false); setAmount(""); setDescription(""); setInstallment("");
      await load();
    } catch (err) { setError(getErrorMessage(err)); }
  }
  async function editInstallment(d: Debt) {
    const v = window.prompt(x.newInstallment, d.monthlyInstallment ? String(d.monthlyInstallment) : "");
    if (v === null || !Number(v)) return;
    try { await EmployeeDebtsApi.update(d.id, { monthlyInstallment: Number(v) }); await load(); } catch (err) { setError(getErrorMessage(err)); }
  }
  async function cancelDebt(d: Debt) {
    if (!window.confirm(x.cancelConfirm)) return;
    try { await EmployeeDebtsApi.update(d.id, { cancel: true }); await load(); } catch (err) { setError(getErrorMessage(err)); }
  }

  return (
    <div>
      <div className="page-header"><h1>{x.title}</h1><button className="btn btn-sm" onClick={() => setOpen(!open)}>{x.add}</button></div>
      <p className="text-muted">{x.intro}</p>
      {error && <div className="alert-error">{error}</div>}
      {message && <div className="card" style={{ marginBottom: 12 }}>{message}</div>}
      {open && (
        <form className="card" style={{ marginBottom: 16 }} onSubmit={submit}>
          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", gap: 12 }}>
            <div className="form-field"><label>{x.employee}</label>
              <select value={employeeId} onChange={(e) => setEmployeeId(e.target.value)} required><option value="">{x.choose}</option>{employees.map((em) => <option key={em.id} value={em.id}>{em.fullNameAr}</option>)}</select></div>
            <div className="form-field"><label>{x.amount}</label><input type="number" min="0" step="0.01" value={amount} onChange={(e) => setAmount(e.target.value)} required /></div>
            <div className="form-field"><label>{x.installment}</label><input type="number" min="0" step="0.01" value={installment} onChange={(e) => setInstallment(e.target.value)} placeholder={x.installmentHint} /></div>
            <div className="form-field"><label>{x.paidFrom}</label><select value={paidFrom} onChange={(e) => setPaidFrom(e.target.value)}><option value="cash">{x.cash}</option><option value="bank">{x.bank}</option></select></div>
            <div className="form-field"><label>{x.date}</label><input type="date" value={date} onChange={(e) => setDate(e.target.value)} required /></div>
          </div>
          <div className="form-field"><label>{x.note}</label><input value={description} onChange={(e) => setDescription(e.target.value)} maxLength={300} /></div>
          <button className="btn" type="submit">{x.save}</button>
        </form>
      )}
      {debts.length === 0 ? <div className="card text-muted">{x.empty}</div> : (
        <div className="card" style={{ overflowX: "auto" }}>
          <table>
            <thead><tr><th>{x.employee}</th><th>{x.reason}</th><th>{x.date}</th><th>{x.amount}</th><th>{x.remaining}</th><th>{x.installment}</th><th>{x.status}</th><th></th></tr></thead>
            <tbody>
              {debts.map((d) => (
                <tr key={d.id}>
                  <td>{d.employeeName}</td><td>{reasonLabel(d.reason)}{d.description ? ` — ${d.description}` : ""}</td><td>{d.debtDate.slice(0, 10)}</td>
                  <td>{d.amount.toLocaleString()}</td><td><strong>{d.remainingAmount.toLocaleString()}</strong></td><td>{d.monthlyInstallment ? d.monthlyInstallment.toLocaleString() : "—"}</td>
                  <td><span className="badge">{statusLabel(d.status)}</span></td>
                  <td>{d.status === 1 && <><button className="btn btn-secondary btn-sm" onClick={() => editInstallment(d)}>{x.edit}</button>{" "}<button className="btn btn-danger btn-sm" onClick={() => cancelDebt(d)}>{x.cancel}</button></>}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
