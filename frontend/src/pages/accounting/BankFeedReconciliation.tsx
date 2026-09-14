import { useEffect, useRef, useState } from "react";
import { AccountsApi, BankFeedReconciliationApi } from "../../api/services";
import type { Account, BankFeedProviderStatus, BankFeedReconciliationSummary } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";
import { bilingualName } from "../../i18n/bilingual";

function firstDayOfMonth() {
  const d = new Date();
  return new Date(d.getFullYear(), d.getMonth(), 1).toISOString().slice(0, 10);
}

export default function BankFeedReconciliation() {
  const { t, lang } = useLanguage();
  const [bankAccounts, setBankAccounts] = useState<Account[]>([]);
  const [selectedAccountId, setSelectedAccountId] = useState("");
  const [fromDate, setFromDate] = useState(firstDayOfMonth());
  const [toDate, setToDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [providerStatus, setProviderStatus] = useState<BankFeedProviderStatus | null>(null);
  const [summary, setSummary] = useState<BankFeedReconciliationSummary | null>(null);
  const [selectedFeedLineId, setSelectedFeedLineId] = useState("");
  const [selectedGlLineId, setSelectedGlLineId] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    async function loadAccounts() {
      const [accountsRes, statusRes] = await Promise.all([AccountsApi.getAll(), BankFeedReconciliationApi.getProviderStatus()]);
      setBankAccounts(accountsRes.data.filter((a) => a.code === "1112" || a.nameAr.includes("بنك") || a.nameEn.toLowerCase().includes("bank")));
      setProviderStatus(statusRes.data);
    }
    loadAccounts();
  }, []);

  async function loadSummary(accountId: string) {
    if (!accountId) return;
    const res = await BankFeedReconciliationApi.getSummary(accountId, fromDate, toDate);
    setSummary(res.data);
  }

  useEffect(() => {
    if (selectedAccountId) loadSummary(selectedAccountId);
    else setSummary(null);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedAccountId, fromDate, toDate]);

  async function handleImport(file: File) {
    if (!selectedAccountId) {
      setError(t.accounting.selectBankAccount);
      return;
    }
    setError(null);
    setMessage(null);
    setBusy(true);
    try {
      const res = await BankFeedReconciliationApi.import(file, selectedAccountId);
      setMessage(`${t.accounting.importedPrefix} ${res.data.importedCount} ${t.accounting.importedMiddle} ${res.data.autoMatchedCount} ${t.accounting.importedSuffix}`);
      await loadSummary(selectedAccountId);
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setBusy(false);
    }
  }

  async function handleSyncLive() {
    if (!selectedAccountId) {
      setError(t.accounting.selectBankAccount);
      return;
    }
    setError(null);
    setMessage(null);
    setBusy(true);
    try {
      const res = await BankFeedReconciliationApi.syncLive(selectedAccountId, fromDate, toDate);
      if (!res.data.success) {
        setError(res.data.failureReason ?? t.accounting.liveFeedNotConnected);
      } else {
        setMessage(`${t.accounting.importedPrefix} ${res.data.importedCount} ${t.accounting.importedMiddle} ${res.data.autoMatchedCount} ${t.accounting.importedSuffix}`);
        await loadSummary(selectedAccountId);
      }
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setBusy(false);
    }
  }

  async function handleAutoMatch() {
    if (!selectedAccountId) return;
    setError(null);
    setBusy(true);
    try {
      await BankFeedReconciliationApi.autoMatch(selectedAccountId);
      await loadSummary(selectedAccountId);
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setBusy(false);
    }
  }

  async function handleManualMatch() {
    if (!selectedFeedLineId || !selectedGlLineId) return;
    setError(null);
    try {
      await BankFeedReconciliationApi.matchManual(selectedFeedLineId, selectedGlLineId);
      setSelectedFeedLineId("");
      setSelectedGlLineId("");
      await loadSummary(selectedAccountId);
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  return (
    <div>
      <div className="page-header">
        <h1>{t.accounting.bankFeedReconciliationTitle}</h1>
        {providerStatus && (
          <span className={`badge ${providerStatus.isConfigured ? "badge-posted" : "badge-draft"}`}>
            {providerStatus.isConfigured ? t.accounting.liveFeedConnected : t.accounting.liveFeedNotConnected}
          </span>
        )}
      </div>
      <p className="text-muted">{t.accounting.bankFeedReconciliationIntro}</p>

      <div className="card">
        <div className="toolbar">
          <div className="form-field">
            <label>{t.accounting.bankAccountLabel}</label>
            <select value={selectedAccountId} onChange={(e) => setSelectedAccountId(e.target.value)}>
              <option value="">{t.accounting.selectBankAccount}</option>
              {bankAccounts.map((a) => (
                <option key={a.id} value={a.id}>
                  {a.code} - {bilingualName(a.nameAr, a.nameEn, lang)}
                </option>
              ))}
            </select>
          </div>
          <div className="form-field">
            <label>{t.accounting.fromDate}</label>
            <input type="date" value={fromDate} onChange={(e) => setFromDate(e.target.value)} />
          </div>
          <div className="form-field">
            <label>{t.accounting.toDate}</label>
            <input type="date" value={toDate} onChange={(e) => setToDate(e.target.value)} />
          </div>
          <button className="btn" style={{ alignSelf: "flex-end" }} onClick={() => fileInputRef.current?.click()} disabled={busy}>
            {t.accounting.uploadStatementCsv}
          </button>
          <input
            ref={fileInputRef}
            type="file"
            accept=".csv"
            style={{ display: "none" }}
            onChange={(e) => e.target.files?.[0] && handleImport(e.target.files[0])}
          />
          <button
            className="btn btn-secondary"
            style={{ alignSelf: "flex-end" }}
            onClick={handleSyncLive}
            disabled={busy || !providerStatus?.isConfigured}
            title={providerStatus?.isConfigured ? undefined : t.accounting.liveFeedNotConnected}
          >
            {t.accounting.syncLiveNow}
          </button>
          <button className="btn btn-secondary" style={{ alignSelf: "flex-end" }} onClick={handleAutoMatch} disabled={busy}>
            {t.accounting.retryAutoMatch}
          </button>
        </div>
      </div>

      {error && <div className="alert-error">{error}</div>}
      {message && (
        <div className="card" style={{ borderColor: "var(--color-success)" }}>
          <strong className="text-success">{message}</strong>
        </div>
      )}

      {summary && (
        <div className="card">
          <div className="toolbar" style={{ justifyContent: "space-between" }}>
            <div>
              <strong>{t.accounting.matchedCount}:</strong> {summary.matchedCount}
            </div>
            <div>
              <strong>{t.accounting.feedNetMovement}:</strong> {summary.feedNetMovement.toLocaleString()}
            </div>
            <div>
              <strong>{t.accounting.glNetMovement}:</strong> {summary.glNetMovement.toLocaleString()}
            </div>
            <span className={`badge ${summary.isBalanced ? "badge-posted" : "badge-draft"}`}>
              {summary.isBalanced ? t.accounting.reconciliationBalanced : t.accounting.reconciliationNotBalanced}
            </span>
          </div>
        </div>
      )}

      {summary && (summary.unmatchedFeedLines.length > 0 || summary.unmatchedGlLines.length > 0) && (
        <div className="card">
          <h3 style={{ marginTop: 0 }}>{t.accounting.matchSelectedTitle}</h3>
          <div className="form-grid">
            <div className="form-field">
              <label>{t.accounting.selectFeedLine}</label>
              <select value={selectedFeedLineId} onChange={(e) => setSelectedFeedLineId(e.target.value)}>
                <option value="">{t.accounting.selectFeedLine}</option>
                {summary.unmatchedFeedLines.map((l) => (
                  <option key={l.id} value={l.id}>
                    {l.description} — {l.amount.toLocaleString()} ({new Date(l.transactionDate).toLocaleDateString()})
                  </option>
                ))}
              </select>
            </div>
            <div className="form-field">
              <label>{t.accounting.selectGlLine}</label>
              <select value={selectedGlLineId} onChange={(e) => setSelectedGlLineId(e.target.value)}>
                <option value="">{t.accounting.selectGlLine}</option>
                {summary.unmatchedGlLines.map((l) => (
                  <option key={l.journalEntryLineId} value={l.journalEntryLineId}>
                    {l.entryNumber} — {l.description ?? ""} — {l.amount.toLocaleString()} ({new Date(l.entryDate).toLocaleDateString()})
                  </option>
                ))}
              </select>
            </div>
          </div>
          <button className="btn" style={{ marginTop: 14 }} onClick={handleManualMatch} disabled={!selectedFeedLineId || !selectedGlLineId}>
            {t.accounting.matchButton}
          </button>
        </div>
      )}

      {summary && (
        <div className="card">
          <h3 style={{ marginTop: 0 }}>{t.accounting.unmatchedFeedLinesTitle} ({summary.unmatchedFeedLines.length})</h3>
          <table>
            <thead>
              <tr>
                <th>{t.common.date}</th>
                <th>{t.common.description}</th>
                <th>{t.common.amount}</th>
                <th>{t.accounting.rateSource}</th>
              </tr>
            </thead>
            <tbody>
              {summary.unmatchedFeedLines.length === 0 && (
                <tr>
                  <td colSpan={4} className="text-muted" style={{ textAlign: "center", padding: 20 }}>
                    {t.common.noData}
                  </td>
                </tr>
              )}
              {summary.unmatchedFeedLines.map((l) => (
                <tr key={l.id}>
                  <td>{new Date(l.transactionDate).toLocaleDateString()}</td>
                  <td>{l.description}</td>
                  <td>{l.amount.toLocaleString()}</td>
                  <td>
                    <span className={`badge ${l.source === "Live" ? "badge-posted" : "badge-draft"}`}>
                      {l.source === "Live" ? t.accounting.rateSourceAuto : t.accounting.rateSourceManual}
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {summary && (
        <div className="card">
          <h3 style={{ marginTop: 0 }}>{t.accounting.unmatchedGlLinesTitle} ({summary.unmatchedGlLines.length})</h3>
          <table>
            <thead>
              <tr>
                <th>{t.common.date}</th>
                <th>{t.accounting.entryNumber}</th>
                <th>{t.common.description}</th>
                <th>{t.common.amount}</th>
              </tr>
            </thead>
            <tbody>
              {summary.unmatchedGlLines.length === 0 && (
                <tr>
                  <td colSpan={4} className="text-muted" style={{ textAlign: "center", padding: 20 }}>
                    {t.common.noData}
                  </td>
                </tr>
              )}
              {summary.unmatchedGlLines.map((l) => (
                <tr key={l.journalEntryLineId}>
                  <td>{new Date(l.entryDate).toLocaleDateString()}</td>
                  <td>{l.entryNumber}</td>
                  <td>{l.description}</td>
                  <td>{l.amount.toLocaleString()}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
