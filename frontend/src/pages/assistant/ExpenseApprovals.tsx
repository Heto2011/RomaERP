import { useEffect, useState } from "react";
import { AiAssistantApi } from "../../api/services";
import { ExpenseFundingSource, ExpensePaymentMethod, type ExpenseCapture } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";

export default function ExpenseApprovals() {
  const { t } = useLanguage();
  const paymentLabel: Record<ExpensePaymentMethod, string> = {
    [ExpensePaymentMethod.Unknown]: "-",
    [ExpensePaymentMethod.Cash]: t.assistant.paymentMethods.cash,
    [ExpensePaymentMethod.Card]: t.assistant.paymentMethods.card,
  };
  const fundingSourceLabel: Record<ExpenseFundingSource, string> = {
    [ExpenseFundingSource.Unknown]: "-",
    [ExpenseFundingSource.CompanyAccount]: t.assistant.fundingSources.companyAccount,
    [ExpenseFundingSource.EmployeeCustody]: t.assistant.fundingSources.employeeCustody,
  };
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
    if (!confirm(t.assistant.rejectConfirm)) return;
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
        <h1>{t.assistant.approvalsTitle}</h1>
      </div>

      <p className="text-muted">
        {t.assistant.approvalsIntro}
      </p>

      {error && <div className="alert-error">{error}</div>}

      <div className="card">
        <table>
          <thead>
            <tr>
              <th>{t.common.description}</th>
              <th>{t.common.amount}</th>
              <th>{t.common.date}</th>
              <th>{t.assistant.fundingSource}</th>
              <th>{t.assistant.paymentMethod}</th>
              <th>{t.assistant.suggestedAccount}</th>
              <th>{t.assistant.proof}</th>
              <th>{t.common.actions}</th>
            </tr>
          </thead>
          <tbody>
            {captures.length === 0 && (
              <tr>
                <td colSpan={8} className="text-muted" style={{ textAlign: "center", padding: 20 }}>
                  {t.assistant.noPendingApprovals}
                </td>
              </tr>
            )}
            {captures.map((c) => (
              <tr key={c.id}>
                <td>{c.description}</td>
                <td>{c.amount} {c.currency}</td>
                <td>{new Date(c.entryDate).toLocaleDateString()}</td>
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
                    {t.assistant.approveAndPost}
                  </button>
                  <button className="btn btn-secondary btn-sm" onClick={() => handleReject(c.id)}>
                    {t.assistant.reject}
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
