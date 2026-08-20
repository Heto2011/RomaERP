import { useEffect, useState } from "react";
import { AccountsApi } from "../../api/services";
import { AccountNature, AccountType, type Account } from "../../api/types";
import { getErrorMessage } from "../../api/client";

const typeLabels: Record<AccountType, string> = {
  [AccountType.Asset]: "أصول",
  [AccountType.Liability]: "خصوم",
  [AccountType.Equity]: "حقوق ملكية",
  [AccountType.Revenue]: "إيرادات",
  [AccountType.Expense]: "مصروفات",
};

function TreeNode({ account }: { account: Account }) {
  return (
    <div className="tree-item">
      <div>
        <strong>{account.code}</strong> — {account.nameAr}
        {account.isControlAccount && <span className="text-muted"> (إجمالي)</span>}
        {!account.isActive && <span className="badge badge-reversed" style={{ marginRight: 8 }}>غير نشط</span>}
      </div>
      {account.children.length > 0 && (
        <div className="tree-children">
          {account.children.map((child) => (
            <TreeNode key={child.id} account={child} />
          ))}
        </div>
      )}
    </div>
  );
}

export default function ChartOfAccounts() {
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
        <h1>شجرة الحسابات</h1>
        <button className="btn" onClick={() => setShowForm((v) => !v)}>
          {showForm ? "إلغاء" : "+ حساب جديد"}
        </button>
      </div>

      {showForm && (
        <div className="card">
          {error && <div className="alert-error">{error}</div>}
          <form onSubmit={handleSubmit}>
            <div className="form-grid">
              <div className="form-field">
                <label>كود الحساب</label>
                <input value={code} onChange={(e) => setCode(e.target.value)} required />
              </div>
              <div className="form-field">
                <label>الاسم بالعربي</label>
                <input value={nameAr} onChange={(e) => setNameAr(e.target.value)} required />
              </div>
              <div className="form-field">
                <label>الاسم بالإنجليزي</label>
                <input value={nameEn} onChange={(e) => setNameEn(e.target.value)} required />
              </div>
              <div className="form-field">
                <label>نوع الحساب</label>
                <select value={accountType} onChange={(e) => setAccountType(Number(e.target.value))}>
                  {Object.entries(typeLabels).map(([value, label]) => (
                    <option key={value} value={value}>
                      {label}
                    </option>
                  ))}
                </select>
              </div>
              <div className="form-field">
                <label>طبيعة الحساب</label>
                <select value={nature} onChange={(e) => setNature(Number(e.target.value))}>
                  <option value={AccountNature.Debit}>مدين</option>
                  <option value={AccountNature.Credit}>دائن</option>
                </select>
              </div>
              <div className="form-field">
                <label>الحساب الأب (اختياري)</label>
                <select value={parentAccountId} onChange={(e) => setParentAccountId(e.target.value)}>
                  <option value="">بدون</option>
                  {flat.map((a) => (
                    <option key={a.id} value={a.id}>
                      {a.code} - {a.nameAr}
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
                  حساب إجمالي (لا يقبل ترحيل مباشر)
                </label>
              </div>
            </div>
            <button className="btn" type="submit" style={{ marginTop: 14 }}>
              حفظ
            </button>
          </form>
        </div>
      )}

      <div className="card">
        {tree.map((a) => (
          <TreeNode key={a.id} account={a} />
        ))}
      </div>
    </div>
  );
}
