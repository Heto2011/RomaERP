import { useEffect, useState } from "react";
import { AccountsApi, ExpensePoliciesApi } from "../../api/services";
import { AccountType, type Account, type ExpensePolicy } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";

export default function ExpensePolicies() {
  const { t, lang } = useLanguage();
  const [policies, setPolicies] = useState<ExpensePolicy[]>([]);
  const [accounts, setAccounts] = useState<Account[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [showForm, setShowForm] = useState(false);
  const [editing, setEditing] = useState<ExpensePolicy | null>(null);
  const [name, setName] = useState("");
  const [accountId, setAccountId] = useState("");
  const [maxAmount, setMaxAmount] = useState("");
  const [proofAbove, setProofAbove] = useState("");
  const [blocks, setBlocks] = useState(false);

  async function load() {
    setPolicies((await ExpensePoliciesApi.getAll()).data);
  }

  useEffect(() => {
    load().catch((e) => setError(getErrorMessage(e)));
    AccountsApi.getAll().then((r) => setAccounts(r.data.filter((a) => a.accountType === AccountType.Expense && !a.isControlAccount))).catch(() => undefined);
  }, []);

  function openForm(p: ExpensePolicy | null) {
    setEditing(p);
    setName(p?.name ?? "");
    setAccountId(p?.accountId ?? "");
    setMaxAmount(p?.maxAmount != null ? String(p.maxAmount) : "");
    setProofAbove(p?.requireProofAbove != null ? String(p.requireProofAbove) : "");
    setBlocks(p?.blocksApproval ?? false);
    setShowForm(true);
  }

  async function save(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    const body = {
      name,
      accountId: accountId || null,
      maxAmount: maxAmount ? Number(maxAmount) : null,
      requireProofAbove: proofAbove ? Number(proofAbove) : null,
      blocksApproval: blocks,
      isActive: editing?.isActive ?? true,
    };
    try {
      if (editing) await ExpensePoliciesApi.update(editing.id, body);
      else await ExpensePoliciesApi.create(body);
      setShowForm(false);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  async function toggleActive(p: ExpensePolicy) {
    setError(null);
    try {
      await ExpensePoliciesApi.update(p.id, { ...p, isActive: !p.isActive });
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  async function remove(p: ExpensePolicy) {
    if (!confirm(t.assistant.policyDeleteConfirm)) return;
    setError(null);
    try {
      await ExpensePoliciesApi.remove(p.id);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  return (
    <div>
      <div className="page-header">
        <h1>{t.assistant.policiesTitle}</h1>
        <button className="btn" onClick={() => (showForm ? setShowForm(false) : openForm(null))}>
          {showForm ? t.common.cancel : t.assistant.policyAdd}
        </button>
      </div>
      <p className="text-muted">{t.assistant.policiesIntro}</p>
      {error && <div className="alert-error">{error}</div>}

      {showForm && (
        <div className="card">
          <form onSubmit={save}>
            <div className="form-grid">
              <div className="form-field"><label>{t.assistant.policyName}</label><input value={name} onChange={(e) => setName(e.target.value)} required /></div>
              <div className="form-field">
                <label>{t.assistant.policyAccount}</label>
                <select value={accountId} onChange={(e) => setAccountId(e.target.value)}>
                  <option value="">{t.assistant.policyAllAccounts}</option>
                  {accounts.map((a) => <option key={a.id} value={a.id}>{a.code} - {lang === "ar" ? a.nameAr : a.nameEn}</option>)}
                </select>
              </div>
              <div className="form-field"><label>{t.assistant.policyMax}</label><input type="number" min="0" step="0.01" value={maxAmount} onChange={(e) => setMaxAmount(e.target.value)} /></div>
              <div className="form-field"><label>{t.assistant.policyProof}</label><input type="number" min="0" step="0.01" value={proofAbove} onChange={(e) => setProofAbove(e.target.value)} /></div>
              <div className="form-field">
                <label><input type="checkbox" checked={blocks} onChange={(e) => setBlocks(e.target.checked)} /> {t.assistant.policyBlocks}</label>
              </div>
            </div>
            <button className="btn" type="submit" style={{ marginTop: 12 }}>{t.common.save}</button>
          </form>
        </div>
      )}

      <div className="card">
        <table>
          <thead>
            <tr>
              <th>{t.assistant.policyName}</th>
              <th>{t.assistant.policyAccount.split(" (")[0]}</th>
              <th>{t.assistant.policyMax}</th>
              <th>{t.assistant.policyProof}</th>
              <th></th>
              <th>{t.common.actions}</th>
            </tr>
          </thead>
          <tbody>
            {policies.length === 0 && (
              <tr><td colSpan={6} className="text-muted" style={{ textAlign: "center", padding: 20 }}>{t.assistant.policyNone}</td></tr>
            )}
            {policies.map((p) => (
              <tr key={p.id} style={p.isActive ? undefined : { opacity: 0.5 }}>
                <td>{p.name}</td>
                <td>{p.accountName ?? t.assistant.policyAllAccounts}</td>
                <td>{p.maxAmount ?? "-"}</td>
                <td>{p.requireProofAbove ?? "-"}</td>
                <td>{p.blocksApproval ? t.assistant.policyBlocking : t.assistant.policyWarning}</td>
                <td>
                  <button className="btn btn-secondary btn-sm" onClick={() => openForm(p)}>{t.common.edit}</button>{" "}
                  <button className="btn btn-secondary btn-sm" onClick={() => toggleActive(p)}>{p.isActive ? t.common.active : "—"}</button>{" "}
                  <button className="btn btn-secondary btn-sm" onClick={() => remove(p)}>{t.common.delete}</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
