import { useEffect, useState } from "react";
import { AiAssistantApi, RecurringExpensesApi } from "../../api/services";
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
  const [notice, setNotice] = useState<string | null>(null);
  // Amounts the approver typed or corrected, keyed by capture id (a fixed monthly item may arrive without one).
  const [amounts, setAmounts] = useState<Record<string, string>>({});
  const [markFor, setMarkFor] = useState<ExpenseCapture | null>(null);
  const [markFixed, setMarkFixed] = useState(true);
  const [markDay, setMarkDay] = useState(1);

  async function load() {
    const res = await AiAssistantApi.getPendingApproval();
    setCaptures(res.data);
  }

  useEffect(() => {
    load();
  }, []);

  function amountOf(c: ExpenseCapture) {
    return amounts[c.id] ?? (c.amount != null ? String(c.amount) : "");
  }

  async function handleApprove(c: ExpenseCapture) {
    setError(null);
    setNotice(null);
    try {
      const typed = Number(amountOf(c));
      if (!(typed > 0)) {
        setError(t.assistant.typeAmount);
        return;
      }
      if (typed !== c.amount) await AiAssistantApi.setAmount(c.id, typed);
      await AiAssistantApi.approve(c.id);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  function openMark(c: ExpenseCapture) {
    setError(null);
    setMarkFor(c);
    setMarkFixed(c.amount != null);
    setMarkDay(Math.min(28, Math.max(1, new Date(c.entryDate).getDate())));
  }

  async function confirmMark() {
    if (!markFor) return;
    setError(null);
    try {
      await RecurringExpensesApi.fromCapture(markFor.id, markFixed, markDay);
      setMarkFor(null);
      setNotice(t.assistant.recurringSaved);
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
      {notice && <div className="alert-success">{notice}</div>}

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
                <td>
                  {c.description}
                  {(c.policyIssues ?? []).map((i, idx) => (
                    <div key={idx} style={{ fontSize: 12, color: i.blocks ? "#b42318" : "#b54708" }}>
                      ⚠ {t.assistant.policyViolates}: {i.policyName} — {i.message}
                    </div>
                  ))}
                </td>
                <td style={{ whiteSpace: "nowrap" }}>
                  <input
                    type="number" min={0} step="0.01" style={{ width: 110 }}
                    value={amountOf(c)} placeholder={t.assistant.needsAmount}
                    onChange={(e) => setAmounts({ ...amounts, [c.id]: e.target.value })}
                  /> {c.currency}
                </td>
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
                  <button className="btn btn-sm" onClick={() => handleApprove(c)}>
                    {t.assistant.approveAndPost}
                  </button>
                  <button className="btn btn-secondary btn-sm" onClick={() => handleReject(c.id)}>
                    {t.assistant.reject}
                  </button>
                  <button className="btn btn-secondary btn-sm" onClick={() => openMark(c)} title={t.assistant.markRecurringTitle}>
                    📌 {t.assistant.markRecurring}
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {markFor && (
        <div className="modal-overlay" onClick={() => setMarkFor(null)}>
          <div className="card" style={{ maxWidth: 480, margin: "8% auto" }} onClick={(e) => e.stopPropagation()}>
            <h3 style={{ marginTop: 0 }}>{t.assistant.markRecurringTitle}</h3>
            <p><strong>{markFor.description}</strong></p>
            <div className="form-field">
              <label>{t.assistant.amountIsFixedQuestion}</label>
              <label style={{ display: "block", padding: "6px 0" }}>
                <input type="radio" checked={markFixed} disabled={markFor.amount == null} onChange={() => setMarkFixed(true)} style={{ marginInlineEnd: 8 }} />
                {t.assistant.amountFixedYes}{markFor.amount != null ? ` (${markFor.amount} ${markFor.currency})` : ""}
              </label>
              <label style={{ display: "block", padding: "6px 0" }}>
                <input type="radio" checked={!markFixed} onChange={() => setMarkFixed(false)} style={{ marginInlineEnd: 8 }} />
                {t.assistant.amountFixedNo}
              </label>
            </div>
            <div className="form-field">
              <label>{t.assistant.dayOfMonth}</label>
              <input type="number" min={1} max={28} value={markDay} onChange={(e) => setMarkDay(Number(e.target.value))} />
            </div>
            <p className="text-muted" style={{ fontSize: 13 }}>{t.assistant.recurringHowTo}</p>
            <div style={{ display: "flex", gap: 10, marginTop: 12 }}>
              <button className="btn" onClick={confirmMark}>{t.common.save}</button>
              <button className="btn btn-secondary" onClick={() => setMarkFor(null)}>{t.common.cancel}</button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
