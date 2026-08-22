import { useEffect, useRef, useState } from "react";
import { AccountsApi, AiAssistantApi, BankReconciliationApi } from "../../api/services";
import type { Account, BankStatementLine, ExpenseCapture } from "../../api/types";
import { getErrorMessage } from "../../api/client";

export default function BankReconciliation() {
  const [unmatchedLines, setUnmatchedLines] = useState<BankStatementLine[]>([]);
  const [pendingCaptures, setPendingCaptures] = useState<ExpenseCapture[]>([]);
  const [bankAccounts, setBankAccounts] = useState<Account[]>([]);
  const [selectedBankAccountId, setSelectedBankAccountId] = useState("");
  const [selectedLineId, setSelectedLineId] = useState("");
  const [selectedCaptureId, setSelectedCaptureId] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  async function load() {
    const [linesRes, capturesRes, accountsRes] = await Promise.all([
      BankReconciliationApi.getUnmatchedLines(),
      AiAssistantApi.getPendingReconciliation(),
      AccountsApi.getAll(),
    ]);
    setUnmatchedLines(linesRes.data);
    setPendingCaptures(capturesRes.data);
    setBankAccounts(accountsRes.data.filter((a) => a.code === "1112" || a.nameAr.includes("بنك")));
  }

  useEffect(() => {
    load();
  }, []);

  async function handleImport(file: File) {
    if (!selectedBankAccountId) {
      setError("اختار حساب البنك الأول.");
      return;
    }
    setError(null);
    setMessage(null);
    try {
      const res = await BankReconciliationApi.import(file, selectedBankAccountId);
      setMessage(`تم استيراد ${res.data.lineCount} حركة، وتمت مطابقة ${res.data.matchedCount} تلقائيًا.`);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  async function handleManualMatch() {
    if (!selectedCaptureId || !selectedLineId) {
      setError("اختار مصروف وحركة بنكية عشان تطابق بينهم.");
      return;
    }
    setError(null);
    try {
      await BankReconciliationApi.matchManual(selectedCaptureId, selectedLineId);
      setMessage("تمت المطابقة، وهيفضل المصروف في انتظار اعتماد المدير قبل الترحيل.");
      setSelectedCaptureId("");
      setSelectedLineId("");
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  async function handleAutoMatch() {
    setError(null);
    try {
      const res = await BankReconciliationApi.autoMatch();
      setMessage(`تمت مطابقة ${res.data} مصروف تلقائيًا.`);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  return (
    <div>
      <div className="page-header">
        <h1>المطابقة البنكية</h1>
      </div>

      <div className="card">
        <p className="text-muted" style={{ marginTop: 0 }}>
          ارفع كشف حساب البنك (CSV بالأعمدة: Date, Description, Amount — بحيث تكون قيمة كل عملية سحب موجبة)
          أسبوعيًا أو شهريًا حسب حجم حركة الحساب، وهيتم مطابقته تلقائيًا مع مصروفات الشبكة المعلّقة من المساعد الذكي.
          المصروفات المتطابقة بتروح بعد كده لشاشة "اعتماد المصروفات" ومش بترحّل إلا بعد موافقة المدير.
        </p>
        <div className="toolbar">
          <div className="form-field">
            <label>حساب البنك</label>
            <select value={selectedBankAccountId} onChange={(e) => setSelectedBankAccountId(e.target.value)}>
              <option value="">اختر الحساب</option>
              {bankAccounts.map((a) => (
                <option key={a.id} value={a.id}>
                  {a.code} - {a.nameAr}
                </option>
              ))}
            </select>
          </div>
          <button className="btn" style={{ alignSelf: "flex-end" }} onClick={() => fileInputRef.current?.click()}>
            رفع كشف حساب (CSV)
          </button>
          <input
            ref={fileInputRef}
            type="file"
            accept=".csv"
            style={{ display: "none" }}
            onChange={(e) => e.target.files?.[0] && handleImport(e.target.files[0])}
          />
          <button className="btn btn-secondary" style={{ alignSelf: "flex-end" }} onClick={handleAutoMatch}>
            إعادة محاولة المطابقة التلقائية
          </button>
        </div>
      </div>

      {error && <div className="alert-error">{error}</div>}
      {message && (
        <div className="card" style={{ borderColor: "var(--color-success)" }}>
          <strong className="text-success">{message}</strong>
        </div>
      )}

      <div className="card">
        <h3 style={{ marginTop: 0 }}>مطابقة يدوية</h3>
        <div className="form-grid">
          <div className="form-field">
            <label>المصروف المعلّق</label>
            <select value={selectedCaptureId} onChange={(e) => setSelectedCaptureId(e.target.value)}>
              <option value="">اختر مصروف</option>
              {pendingCaptures.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.description} — {c.amount} {c.currency} ({new Date(c.entryDate).toLocaleDateString("ar-EG")})
                </option>
              ))}
            </select>
          </div>
          <div className="form-field">
            <label>حركة كشف الحساب</label>
            <select value={selectedLineId} onChange={(e) => setSelectedLineId(e.target.value)}>
              <option value="">اختر حركة</option>
              {unmatchedLines.map((l) => (
                <option key={l.id} value={l.id}>
                  {l.description} — {l.amount} ({new Date(l.transactionDate).toLocaleDateString("ar-EG")})
                </option>
              ))}
            </select>
          </div>
        </div>
        <button className="btn" style={{ marginTop: 14 }} onClick={handleManualMatch}>
          طابق وترحيل
        </button>
      </div>

      <div className="card">
        <h3 style={{ marginTop: 0 }}>مصروفات في انتظار المطابقة ({pendingCaptures.length})</h3>
        <table>
          <thead>
            <tr>
              <th>الوصف</th>
              <th>المبلغ</th>
              <th>التاريخ</th>
              <th>الحساب المقترح</th>
              <th>إثبات مرفق</th>
            </tr>
          </thead>
          <tbody>
            {pendingCaptures.map((c) => (
              <tr key={c.id}>
                <td>{c.description}</td>
                <td>{c.amount} {c.currency}</td>
                <td>{new Date(c.entryDate).toLocaleDateString("ar-EG")}</td>
                <td>{c.suggestedAccountCode} - {c.suggestedAccountName}</td>
                <td>{c.proofFileName ?? "-"}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="card">
        <h3 style={{ marginTop: 0 }}>حركات كشف الحساب غير المطابقة ({unmatchedLines.length})</h3>
        <table>
          <thead>
            <tr>
              <th>التاريخ</th>
              <th>الوصف</th>
              <th>المبلغ</th>
            </tr>
          </thead>
          <tbody>
            {unmatchedLines.map((l) => (
              <tr key={l.id}>
                <td>{new Date(l.transactionDate).toLocaleDateString("ar-EG")}</td>
                <td>{l.description}</td>
                <td>{l.amount}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
