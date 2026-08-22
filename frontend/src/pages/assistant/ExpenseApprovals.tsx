import { useEffect, useState } from "react";
import { AiAssistantApi } from "../../api/services";
import { ExpenseFundingSource, ExpensePaymentMethod, type ExpenseCapture } from "../../api/types";
import { getErrorMessage } from "../../api/client";

const paymentLabel: Record<ExpensePaymentMethod, string> = {
  [ExpensePaymentMethod.Unknown]: "-",
  [ExpensePaymentMethod.Cash]: "كاش",
  [ExpensePaymentMethod.Card]: "شبكة (متطابق مع كشف الحساب)",
};

const fundingSourceLabel: Record<ExpenseFundingSource, string> = {
  [ExpenseFundingSource.Unknown]: "-",
  [ExpenseFundingSource.CompanyAccount]: "جاري الشركة",
  [ExpenseFundingSource.EmployeeCustody]: "عهدة موظف",
};

export default function ExpenseApprovals() {
  const [captures, setCaptures] = useState<ExpenseCapture[]>([]);
  const [error, setError] = useState<string | null>(null);

  async function load() {
    const res = await AiAssistantApi.getPendingApproval();
    setCaptures(res.data);
  }

  useEffect(() => {
    load();
  }, []);

  async function handleApprove(id: string) {
    setError(null);
    try {
      await AiAssistantApi.approve(id);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  async function handleReject(id: string) {
    setError(null);
    if (!confirm("متأكد إنك عايز ترفض المصروف ده؟ لن يترحّل في الحسابات.")) return;
    try {
      await AiAssistantApi.reject(id);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  return (
    <div>
      <div className="page-header">
        <h1>اعتماد المصروفات</h1>
      </div>

      <p className="text-muted">
        كل مصروف اتسجل عن طريق المساعد الذكي (كاش أو شبكة بعد ما يتطابق مع كشف الحساب) بيقف هنا لحد ما توافق
        عليه — مفيش أي حاجة بترحّل في الحسابات من غير اعتمادك.
      </p>

      {error && <div className="alert-error">{error}</div>}

      <div className="card">
        <table>
          <thead>
            <tr>
              <th>الوصف</th>
              <th>المبلغ</th>
              <th>التاريخ</th>
              <th>مصدر الصرف</th>
              <th>طريقة الدفع</th>
              <th>الحساب المقترح</th>
              <th>إثبات</th>
              <th>إجراءات</th>
            </tr>
          </thead>
          <tbody>
            {captures.length === 0 && (
              <tr>
                <td colSpan={8} className="text-muted" style={{ textAlign: "center", padding: 20 }}>
                  مفيش مصروفات في انتظار الاعتماد دلوقتي.
                </td>
              </tr>
            )}
            {captures.map((c) => (
              <tr key={c.id}>
                <td>{c.description}</td>
                <td>{c.amount} {c.currency}</td>
                <td>{new Date(c.entryDate).toLocaleDateString("ar-EG")}</td>
                <td>
                  {fundingSourceLabel[c.fundingSource]}
                  {c.fundingSource === ExpenseFundingSource.EmployeeCustody && c.custodyEmployeeName && (
                    <div className="text-muted" style={{ fontSize: 12 }}>{c.custodyEmployeeName}</div>
                  )}
                </td>
                <td>{c.fundingSource === ExpenseFundingSource.EmployeeCustody ? "-" : paymentLabel[c.paymentMethod]}</td>
                <td>{c.suggestedAccountCode} - {c.suggestedAccountName}</td>
                <td>{c.proofFileName ?? "-"}</td>
                <td style={{ display: "flex", gap: 6 }}>
                  <button className="btn btn-sm" onClick={() => handleApprove(c.id)}>
                    اعتماد وترحيل
                  </button>
                  <button className="btn btn-secondary btn-sm" onClick={() => handleReject(c.id)}>
                    رفض
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
