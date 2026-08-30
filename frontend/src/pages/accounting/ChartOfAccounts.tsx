import { useEffect, useState } from "react";
import { AccountsApi } from "../../api/services";
import { AccountNature, AccountType, type Account } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";
import type { dictionaries, Lang } from "../../i18n/translations";
import { bilingualName } from "../../i18n/bilingual";

function TreeNode({ account, t, lang }: { account: Account; t: (typeof dictionaries)["ar"]; lang: Lang }) {
  return (
    <div className="tree-item">
      <div>
        <strong>{account.code}</strong> — {bilingualName(account.nameAr, account.nameEn, lang)}
        {account.isControlAccount && <span className="text-muted"> ({t.accounting.controlAccountBadge})</span>}
        {!account.isActive && <span className="badge badge-reversed" style={{ marginRight: 8 }}>{t.accounting.inactiveBadge}</span>}
      </div>
      {account.children.length > 0 && (
        <div className="tree-children">
          {account.children.map((child) => (
            <TreeNode key={child.id} account={child} t={t} lang={lang} />
          ))}
        </div>
      )}
    </div>
  );
}

export default function ChartOfAccounts() {
  const { t, lang } = useLanguage();
  const typeLabels: Record<AccountType, string> = {
    [AccountType.Asset]: t.accounting.types.asset,
    [AccountType.Liability]: t.accounting.types.liability,
    [AccountType.Equity]: t.accounting.types.equity,
    [AccountType.Revenue]: t.accounting.types.revenue,
    [AccountType.Expense]: t.accounting.types.expense,
  };
  const [tree, setTree] = useState<Account[]>([]);
  const [flat, setFlat] = useState<Account[]>([]);
  const [showForm, setShowForm] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [code, setCode] = useState("");
  const [nameAr, setNameAr] = useState("");
  const [nameEn, setNameEn] = useState("");
  const [accountType, setAccountType] = useState(AccountType.Asset);
  const [nature, setNature] = useState(AccountNature.Debit);
  const [parentAccountId, setParentAccountId] = useState("");
  const [isControlAccount, setIsControlAccount] = useState(false);

  async function load() {
    const [treeRes, flatRes] = await Promise.all([AccountsApi.getTree(), AccountsApi.getAll()]);
    setTree(treeRes.data);
    setFlat(flatRes.data);
  }

  useEffect(() => {
    load();
  }, []);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    try {
      await AccountsApi.create({
        code,
        nameAr,
        nameEn,
        accountType,
        nature,
        parentAccountId: parentAccountId || null,
        isControlAccount,
      });
      setShowForm(false);
      setCode("");
      setNameAr("");
      setNameEn("");
      setParentAccountId("");
      setIsControlAccount(false);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  return (
    <div>
      <div className="page-header">
        <h1>{t.nav.chartOfAccounts}</h1>
        <button className="btn" onClick={() => setShowForm((v) => !v)}>
          {showForm ? t.common.cancel : t.accounting.newAccount}
        </button>
      </div>

      {showForm && (
        <div className="card">
          {error && <div className="alert-error">{error}</div>}
          <form onSubmit={handleSubmit}>
            <div className="form-grid">
              <div className="form-field">
                <label>{t.accounting.accountCode}</label>
                <input value={code} onChange={(e) => setCode(e.target.value)} required />
              </div>
              <div className="form-field">
                <label>{t.common.nameAr}</label>
                <input value={nameAr} onChange={(e) => setNameAr(e.target.value)} required />
              </div>
              <div className="form-field">
                <label>{t.common.nameEn}</label>
                <input value={nameEn} onChange={(e) => setNameEn(e.target.value)} required />
              </div>
              <div className="form-field">
                <label>{t.accounting.accountType}</label>
                <select value={accountType} onChange={(e) => setAccountType(Number(e.target.value))}>
                  {Object.entries(typeLabels).map(([value, label]) => (
                    <option key={value} value={value}>
                      {label}
                    </option>
                  ))}
                </select>
              </div>
              <div className="form-field">
                <label>{t.accounting.accountNature}</label>
                <select value={nature} onChange={(e) => setNature(Number(e.target.value))}>
                  <option value={AccountNature.Debit}>{t.accounting.debit}</option>
                  <option value={AccountNature.Credit}>{t.accounting.credit}</option>
                </select>
              </div>
              <div className="form-field">
                <label>{t.accounting.parentAccount}</label>
                <select value={parentAccountId} onChange={(e) => setParentAccountId(e.target.value)}>
                  <option value="">{t.common.none}</option>
                  {flat.map((a) => (
                    <option key={a.id} value={a.id}>
                      {a.code} - {bilingualName(a.nameAr, a.nameEn, lang)}
                    </option>
                  ))}
                </select>
              </div>
              <div className="form-field">
                <label>
                  <input
                    type="checkbox"
                    checked={isControlAccount}
                    onChange={(e) => setIsControlAccount(e.target.checked)}
                  />{" "}
                  {t.accounting.controlAccountLabel}
                </label>
              </div>
            </div>
            <button className="btn" type="submit" style={{ marginTop: 14 }}>
              {t.common.save}
            </button>
          </form>
        </div>
      )}

      <div className="card">
        {tree.map((a) => (
          <TreeNode key={a.id} account={a} t={t} lang={lang} />
        ))}
      </div>
    </div>
  );
}
