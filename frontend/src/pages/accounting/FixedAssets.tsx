import { useEffect, useState } from "react";
import { AccountsApi, FixedAssetsApi } from "../../api/services";
import { DepreciationMethod, FixedAssetStatus, type Account, type FixedAsset } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";
import { bilingualName } from "../../i18n/bilingual";

export default function FixedAssets() {
  const { t, lang } = useLanguage();
  const [assets, setAssets] = useState<FixedAsset[]>([]);
  const [accounts, setAccounts] = useState<Account[]>([]);
  const [showForm, setShowForm] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [code, setCode] = useState("");
  const [nameAr, setNameAr] = useState("");
  const [nameEn, setNameEn] = useState("");
  const [assetAccountId, setAssetAccountId] = useState("");
  const [accumulatedDepreciationAccountId, setAccumulatedDepreciationAccountId] = useState("");
  const [acquisitionCost, setAcquisitionCost] = useState("");
  const [acquisitionDate, setAcquisitionDate] = useState(new Date().toISOString().slice(0, 10));
  const [usefulLifeYears, setUsefulLifeYears] = useState("");
  const [salvageValue, setSalvageValue] = useState("0");
  const [depreciationMethod, setDepreciationMethod] = useState(DepreciationMethod.StraightLine);
  const [decliningBalanceRate, setDecliningBalanceRate] = useState("");

  const methodLabel: Record<DepreciationMethod, string> = {
    [DepreciationMethod.StraightLine]: t.fixedAssets.straightLine,
    [DepreciationMethod.DecliningBalance]: t.fixedAssets.decliningBalance,
  };

  async function load() {
    const [assetsRes, accountsRes] = await Promise.all([FixedAssetsApi.getAll(), AccountsApi.getAll()]);
    setAssets(assetsRes.data);
    setAccounts(accountsRes.data.filter((a) => !a.isControlAccount));
  }

  useEffect(() => {
    load();
  }, []);

  function resetForm() {
    setCode("");
    setNameAr("");
    setNameEn("");
    setAssetAccountId("");
    setAccumulatedDepreciationAccountId("");
    setAcquisitionCost("");
    setUsefulLifeYears("");
    setSalvageValue("0");
    setDepreciationMethod(DepreciationMethod.StraightLine);
    setDecliningBalanceRate("");
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    try {
      await FixedAssetsApi.create({
        code,
        nameAr,
        nameEn,
        assetAccountId,
        accumulatedDepreciationAccountId,
        acquisitionCost: Number(acquisitionCost) || 0,
        acquisitionDate,
        usefulLifeYears: Number(usefulLifeYears) || 0,
        salvageValue: Number(salvageValue) || 0,
        depreciationMethod,
        decliningBalanceRate: depreciationMethod === DepreciationMethod.DecliningBalance ? Number(decliningBalanceRate) || 0 : null,
      });
      setShowForm(false);
      resetForm();
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  return (
    <div>
      <div className="page-header">
        <h1>{t.fixedAssets.title}</h1>
        <button className="btn" onClick={() => setShowForm((v) => !v)}>
          {showForm ? t.common.cancel : t.fixedAssets.newAsset}
        </button>
      </div>

      {error && <div className="alert-error">{error}</div>}

      {showForm && (
        <div className="card">
          <form onSubmit={handleSubmit}>
            <div className="form-grid">
              <div className="form-field">
                <label>{t.fixedAssets.assetCode}</label>
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
                <label>{t.fixedAssets.assetAccount}</label>
                <select value={assetAccountId} onChange={(e) => setAssetAccountId(e.target.value)} required>
                  <option value="">{t.fixedAssets.selectAccount}</option>
                  {accounts.map((a) => (
                    <option key={a.id} value={a.id}>
                      {a.code} - {bilingualName(a.nameAr, a.nameEn, lang)}
                    </option>
                  ))}
                </select>
              </div>
              <div className="form-field">
                <label>{t.fixedAssets.accumulatedDepreciationAccount}</label>
                <select value={accumulatedDepreciationAccountId} onChange={(e) => setAccumulatedDepreciationAccountId(e.target.value)} required>
                  <option value="">{t.fixedAssets.selectAccount}</option>
                  {accounts.map((a) => (
                    <option key={a.id} value={a.id}>
                      {a.code} - {bilingualName(a.nameAr, a.nameEn, lang)}
                    </option>
                  ))}
                </select>
              </div>
              <div className="form-field">
                <label>{t.fixedAssets.acquisitionCost}</label>
                <input type="number" step="0.01" value={acquisitionCost} onChange={(e) => setAcquisitionCost(e.target.value)} required />
              </div>
              <div className="form-field">
                <label>{t.fixedAssets.acquisitionDate}</label>
                <input type="date" value={acquisitionDate} onChange={(e) => setAcquisitionDate(e.target.value)} required />
              </div>
              <div className="form-field">
                <label>{t.fixedAssets.usefulLifeYears}</label>
                <input type="number" value={usefulLifeYears} onChange={(e) => setUsefulLifeYears(e.target.value)} required />
              </div>
              <div className="form-field">
                <label>{t.fixedAssets.salvageValue}</label>
                <input type="number" step="0.01" value={salvageValue} onChange={(e) => setSalvageValue(e.target.value)} />
              </div>
              <div className="form-field">
                <label>{t.fixedAssets.depreciationMethod}</label>
                <select value={depreciationMethod} onChange={(e) => setDepreciationMethod(Number(e.target.value))}>
                  <option value={DepreciationMethod.StraightLine}>{t.fixedAssets.straightLine}</option>
                  <option value={DepreciationMethod.DecliningBalance}>{t.fixedAssets.decliningBalance}</option>
                </select>
              </div>
              {depreciationMethod === DepreciationMethod.DecliningBalance && (
                <div className="form-field">
                  <label>{t.fixedAssets.decliningBalanceRate}</label>
                  <input type="number" step="0.01" value={decliningBalanceRate} onChange={(e) => setDecliningBalanceRate(e.target.value)} required />
                </div>
              )}
            </div>
            <button className="btn" type="submit" style={{ marginTop: 14 }}>
              {t.common.save}
            </button>
          </form>
        </div>
      )}

      <div className="card">
        <table>
          <thead>
            <tr>
              <th>{t.fixedAssets.assetCode}</th>
              <th>{t.common.nameAr}</th>
              <th>{t.fixedAssets.assetAccount}</th>
              <th>{t.fixedAssets.acquisitionCost}</th>
              <th>{t.fixedAssets.depreciationMethod}</th>
              <th>{t.fixedAssets.accumulatedDepreciation}</th>
              <th>{t.fixedAssets.bookValue}</th>
              <th>{t.common.status}</th>
            </tr>
          </thead>
          <tbody>
            {assets.length === 0 && (
              <tr>
                <td colSpan={8} className="text-muted" style={{ textAlign: "center", padding: 20 }}>
                  {t.common.noData}
                </td>
              </tr>
            )}
            {assets.map((a) => (
              <tr key={a.id}>
                <td>{a.code}</td>
                <td>{bilingualName(a.nameAr, a.nameEn, lang)}</td>
                <td>{a.assetAccountCode} - {a.assetAccountName}</td>
                <td>{a.acquisitionCost.toLocaleString()}</td>
                <td>{methodLabel[a.depreciationMethod]}</td>
                <td>{a.accumulatedDepreciation.toLocaleString()}</td>
                <td>{a.bookValue.toLocaleString()}</td>
                <td>
                  <span className={a.status === FixedAssetStatus.Active ? "text-success" : "text-danger"}>
                    {a.status === FixedAssetStatus.Active ? t.fixedAssets.active : t.fixedAssets.disposed}
                  </span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
