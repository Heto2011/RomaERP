import { useEffect, useState } from "react";
import { EmployeeDebtsApi } from "../api/services";
import { getErrorMessage } from "../api/client";
import { useAuth } from "../context/AuthContext";
import { useLanguage } from "../i18n/LanguageContext";

const TEXT = {
  ar: { title: "خصم المديونيات من الرواتب", intro: "النظام بيقترح القسط بس. عدّل المبلغ لو عايز، وبعدها اعتمد. مفيش حاجة بتتخصم من غير اعتمادك.",
    employee: "الموظف", debt: "المديونية", remaining: "المتبقي", net: "صافي الراتب", amount: "المبلغ اللي هيتخصم", save: "اعتماد الخصم", saved: "تم اعتماد الخصم من الدورة دي.",
    approved: "معتمد قبل كده", none: "مفيش مديونيات على موظفين الدورة دي.", only: "الاعتماد للمالك أو المحاسب بس." },
  en: { title: "Debt repayments from pay", intro: "The system only proposes the instalment. Edit the amount if you wish, then approve. Nothing is deducted without your approval.",
    employee: "Employee", debt: "Debt", remaining: "Remaining", net: "Net pay", amount: "Amount to deduct", save: "Approve deductions", saved: "Deductions approved for this run.",
    approved: "Already approved", none: "No employee debts on this run.", only: "Only the owner or accountant can approve." },
};
type Proposal = { debtId: string; employeeId: string; employeeName: string; description?: string | null; remainingAmount: number; netSalaryBefore: number; proposedAmount: number; approvedAmount: number };

export default function DebtDeductionsPanel({ runId, onChanged }: { runId: string; onChanged: () => void }) {
  const { lang } = useLanguage();
  const { user } = useAuth();
  const x = TEXT[lang === "ar" ? "ar" : "en"];
  const allowed = !!user && (user.roles.includes("Admin") || user.roles.includes("Accountant"));
  const [rows, setRows] = useState<Proposal[]>([]);
  const [amounts, setAmounts] = useState<Record<string, string>>({});
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  useEffect(() => {
    if (!allowed) return;
    EmployeeDebtsApi.proposals(runId).then((r) => {
      setRows(r.data as Proposal[]);
      setAmounts(Object.fromEntries((r.data as Proposal[]).map((p) => [p.debtId, String(p.approvedAmount > 0 ? p.approvedAmount : p.proposedAmount)])));
    }).catch((e) => setError(getErrorMessage(e)));
  }, [runId, allowed]);

  if (!allowed || (rows.length === 0 && !error)) return null;

  async function save() {
    setError(null); setMessage(null);
    try {
      await EmployeeDebtsApi.approve(runId, rows.map((p) => ({ debtId: p.debtId, amount: Number(amounts[p.debtId]) || 0 })));
      setMessage(x.saved);
      onChanged();
    } catch (e) { setError(getErrorMessage(e)); }
  }

  return (
    <div className="card" style={{ marginBottom: 12, borderColor: "#c8862e" }}>
      <h4 style={{ marginTop: 0 }}>{x.title}</h4>
      <p className="text-muted" style={{ fontSize: 13 }}>{x.intro}</p>
      {error && <div className="alert-error">{error}</div>}
      {message && <div className="text-success">{message}</div>}
      <table>
        <thead><tr><th>{x.employee}</th><th>{x.debt}</th><th>{x.remaining}</th><th>{x.net}</th><th>{x.amount}</th></tr></thead>
        <tbody>
          {rows.map((p) => (
            <tr key={p.debtId}>
              <td>{p.employeeName}</td><td>{p.description ?? "—"}</td><td>{p.remainingAmount.toLocaleString()}</td><td>{p.netSalaryBefore.toLocaleString()}</td>
              <td><input type="number" min="0" step="0.01" max={p.remainingAmount} style={{ width: 110 }} value={amounts[p.debtId] ?? ""} onChange={(e) => setAmounts({ ...amounts, [p.debtId]: e.target.value })} />
                {p.approvedAmount > 0 && <span className="text-muted" style={{ fontSize: 12 }}> {x.approved}: {p.approvedAmount.toLocaleString()}</span>}</td>
            </tr>
          ))}
        </tbody>
      </table>
      <button className="btn" style={{ marginTop: 8 }} onClick={save}>{x.save}</button>
    </div>
  );
}
