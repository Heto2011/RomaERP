import { useEffect, useRef, useState } from "react";
import { AccountsApi, AiAssistantApi, BankReconciliationApi } from "../../api/services";
import type { Account, BankStatementLine, ExpenseCapture } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";
import { bilingualName } from "../../i18n/bilingual";

export default function BankReconciliation() {
  const { t, lang } = useLanguage();
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
    setBankAccounts(accountsRes.data.filter((a) => a.code === "1112" || a.nameAr.includes("بنك") || a.nameEn.toLowerCase().includes("bank")));
  }

  useEffect(() => {
    load();
  }, []);

  async function handleImport(file: File) {
    if (!selectedBankAccountId) {
      setError(t.assistant.selectBankAccountFirst);
      return;
    }
    setError(null);
    setMessage(null);
    try {
      const res = await BankReconciliationApi.import(file, selectedBankAccountId);
      setMessage(`${t.assistant.importedPrefix} ${res.data.lineCount} ${t.assistant.importedMiddle} ${res.data.matchedCount} ${t.assistant.importedSuffix}`);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  async function handleManualMatch() {
    if (!selectedCaptureId || !selectedLineId) {
      setError(t.assistant.selectExpenseAndLine);
      return;
    }
    setError(null);
    try {
      await BankReconciliationApi.matchManual(selectedCaptureId, selectedLineId);
      setMessage(t.assistant.matchedManualNote);
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
      setMessage(`${t.assistant.autoMatchedPrefix} ${res.data} ${t.assistant.autoMatchedSuffix}`);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  return (
    <div>
      <div className="page-header">
        <h1>{t.assistant.reconciliationTitle}</h1>
      </div>

      <div className="card">
        <p className="text-muted" style={{ marginTop: 0 }}>
          {t.assistant.reconciliationIntro}
        </p>
        <div className="toolbar">
          <div className="form-field">
            <label>{t.assistant.bankAccount}</label>
            <select value={selectedBankAccountId} onChange={(e) => setSelectedBankAccountId(e.target.value)}>
              <option value="">{t.assistant.selectBankAccount}</option>
              {bankAccounts.map((a) => (
                <option key={a.id} value={a.id}>
                  {a.code} - {bilingualName(a.nameAr, a.nameEn, lang)}
                </option>
              ))}
            </select>
          </div>
          <button className="btn" style={{ alignSelf: "flex-end" }} onClick={() => fileInputRef.current?.click()}>
            {t.assistant.uploadStatement}
          </button>
          <input
            ref={fileInputRef}
            type="file"
            accept=".csv"
            style={{ display: "none" }}
            onChange={(e) => e.target.files?.[0] && handleImport(e.target.files[0])}
          />
          <button className="btn btn-secondary" style={{ alignSelf: "flex-end" }} onClick={handleAutoMatch}>
            {t.assistant.retryAutoMatch}
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
        <h3 style={{ marginTop: 0 }}>{t.assistant.manualMatchTitle}</h3>
        <div className="form-grid">
          <div className="form-field">
            <label>{t.assistant.pendingExpense}</label>
            <select value={selectedCaptureId} onChange={(e) => setSelectedCaptureId(e.target.value)}>
              <option value="">{t.assistant.selectExpense}</option>
              {pendingCaptures.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.description} — {c.amount} {c.currency} ({new Date(c.entryDate).toLocaleDateString()})
                </option>
              ))}
            </select>
          </div>
          <div className="form-field">
            <label>{t.assistant.statementLine}</label>
            <select value={selectedLineId} onChange={(e) => setSelectedLineId(e.target.value)}>
              <option value="">{t.assistant.selectLine}</option>
              {unmatchedLines.map((l) => (
                <option key={l.id} value={l.id}>
                  {l.description} — {l.amount} ({new Date(l.transactionDate).toLocaleDateString()})
                </option>
              ))}
            </select>
          </div>
        </div>
        <button className="btn" style={{ marginTop: 14 }} onClick={handleManualMatch}>
          {t.assistant.matchAndPost}
        </button>
      </div>

      <div className="card">
        <h3 style={{ marginTop: 0 }}>{t.assistant.pendingExpensesTitle} ({pendingCaptures.length})</h3>
        <table>
          <thead>
            <tr>
              <th>{t.common.description}</th>
              <th>{t.common.amount}</th>
              <th>{t.common.date}</th>
              <th>{t.assistant.suggestedAccount}</th>
              <th>{t.assistant.attachedProof}</th>
            </tr>
          </thead>
          <tbody>
            {pendingCaptures.map((c) => (
              <tr key={c.id}>
                <td>{c.description}</td>
                <td>{c.amount} {c.currency}</td>
                <td>{new Date(c.entryDate).toLocaleDateString()}</td>
                <td>{c.suggestedAccountCode} - {c.suggestedAccountName}</td>
                <td>{c.proofFileName ?? "-"}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="card">
        <h3 style={{ marginTop: 0 }}>{t.assistant.unmatchedLinesTitle} ({unmatchedLines.length})</h3>
        <table>
          <thead>
            <tr>
              <th>{t.common.date}</th>
              <th>{t.common.description}</th>
              <th>{t.common.amount}</th>
            </tr>
          </thead>
          <tbody>
            {unmatchedLines.map((l) => (
              <tr key={l.id}>
                <td>{new Date(l.transactionDate).toLocaleDateString()}</td>
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
