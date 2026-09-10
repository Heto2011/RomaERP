import { useEffect, useState } from "react";
import { AccountsApi, SalaryComponentsApi } from "../../api/services";
import { CalculationType, SalaryComponentType, type Account, type SalaryComponent } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";
import { bilingualName } from "../../i18n/bilingual";
import InfoTooltip from "../../components/InfoTooltip";

export default function SalaryComponentsPage() {
  const { t, lang } = useLanguage();
  const [components, setComponents] = useState<SalaryComponent[]>([]);
  const [accounts, setAccounts] = useState<Account[]>([]);
  const [showForm, setShowForm] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [code, setCode] = useState("");
  const [nameAr, setNameAr] = useState("");
  const [nameEn, setNameEn] = useState("");
  const [componentType, setComponentType] = useState(SalaryComponentType.Allowance);
  const [calculationType, setCalculationType] = useState(CalculationType.FixedAmount);
  const [defaultValue, setDefaultValue] = useState("");
  const [isTaxable, setIsTaxable] = useState(false);
  const [linkedAccountId, setLinkedAccountId] = useState("");

  async function load() {
    const [compRes, accRes] = await Promise.all([SalaryComponentsApi.getAll(), AccountsApi.getAll()]);
    setComponents(compRes.data);
    setAccounts(accRes.data);
  }

  useEffect(() => {
    load();
  }, []);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    try {
      await SalaryComponentsApi.create({
        code,
        nameAr,
        nameEn,
        componentType,
        calculationType,
        defaultValue: Number(defaultValue) || 0,
        isTaxable,
        linkedAccountId: linkedAccountId || null,
      });
      setShowForm(false);
      setCode("");
      setNameAr("");
      setNameEn("");
      setDefaultValue("");
      setIsTaxable(false);
      setLinkedAccountId("");
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  const typeLabel: Record<SalaryComponentType, string> = {
    [SalaryComponentType.Allowance]: t.hr.allowanceType,
    [SalaryComponentType.Deduction]: t.hr.deductionType,
  };
  const calcLabel: Record<CalculationType, string> = {
    [CalculationType.FixedAmount]: t.hr.fixedAmount,
    [CalculationType.PercentageOfBasic]: t.hr.percentageOfBasic,
  };

  return (
    <div>
      <div className="page-header">
        <h1>{t.hr.salaryComponentsTitle}<InfoTooltip text={t.hr.salaryComponentsIntro} /></h1>
        <button className="btn" onClick={() => setShowForm((v) => !v)}>
          {showForm ? t.common.cancel : t.hr.newSalaryComponent}
        </button>
      </div>
      <p className="text-muted">{t.hr.salaryComponentsIntro}</p>

      {error && <div className="alert-error">{error}</div>}

      {showForm && (
        <div className="card">
          <form onSubmit={handleSubmit}>
            <div className="form-grid">
              <div className="form-field">
                <label>{t.hr.componentCode}</label>
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
                <label>{t.hr.componentType}</label>
                <select value={componentType} onChange={(e) => setComponentType(Number(e.target.value))}>
                  <option value={SalaryComponentType.Allowance}>{t.hr.allowanceType}</option>
                  <option value={SalaryComponentType.Deduction}>{t.hr.deductionType}</option>
                </select>
              </div>
              <div className="form-field">
                <label>{t.hr.calculationType}</label>
                <select value={calculationType} onChange={(e) => setCalculationType(Number(e.target.value))}>
                  <option value={CalculationType.FixedAmount}>{t.hr.fixedAmount}</option>
                  <option value={CalculationType.PercentageOfBasic}>{t.hr.percentageOfBasic}</option>
                </select>
              </div>
              <div className="form-field">
                <label>{t.hr.defaultValue}</label>
                <input type="number" step="0.01" value={defaultValue} onChange={(e) => setDefaultValue(e.target.value)} required />
              </div>
              <div className="form-field">
                <label>{t.hr.linkedAccount}</label>
                <select value={linkedAccountId} onChange={(e) => setLinkedAccountId(e.target.value)}>
                  <option value="">-</option>
                  {accounts.map((a) => (
                    <option key={a.id} value={a.id}>{a.code} - {bilingualName(a.nameAr, a.nameEn, lang)}</option>
                  ))}
                </select>
              </div>
              <div className="form-field" style={{ justifyContent: "flex-end" }}>
                <label style={{ display: "flex", alignItems: "center", gap: 6, fontWeight: "normal" }}>
                  <input type="checkbox" checked={isTaxable} onChange={(e) => setIsTaxable(e.target.checked)} />
                  {t.hr.isTaxable}
                </label>
              </div>
            </div>
            <button className="btn" type="submit" style={{ marginTop: 14 }}>{t.common.save}</button>
          </form>
        </div>
      )}

      <div className="card">
        <table>
          <thead>
            <tr>
              <th>{t.common.code}</th>
              <th>{t.common.nameAr}</th>
              <th>{t.hr.componentType}</th>
              <th>{t.hr.calculationType}</th>
              <th>{t.hr.defaultValue}</th>
            </tr>
          </thead>
          <tbody>
            {components.map((c) => (
              <tr key={c.id}>
                <td>{c.code}</td>
                <td>{bilingualName(c.nameAr, c.nameEn, lang)}</td>
                <td>{typeLabel[c.componentType]}</td>
                <td>{calcLabel[c.calculationType]}</td>
                <td>{c.defaultValue.toLocaleString()}{c.calculationType === CalculationType.PercentageOfBasic ? "%" : ""}</td>
              </tr>
            ))}
            {components.length === 0 && (
              <tr><td colSpan={5} className="text-muted">{t.common.noData}</td></tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}
