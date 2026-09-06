import { useEffect, useState } from "react";
import { ExchangeRatesApi, LookupsApi } from "../../api/services";
import type { ExchangeRate } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";

export default function ExchangeRates() {
  const { t } = useLanguage();
  const [rates, setRates] = useState<ExchangeRate[]>([]);
  const [functionalCurrency, setFunctionalCurrency] = useState("");
  const [error, setError] = useState<string | null>(null);

  const [showForm, setShowForm] = useState(false);
  const [currencyCode, setCurrencyCode] = useState("");
  const [rateDate, setRateDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [rateToFunctional, setRateToFunctional] = useState(1);

  async function load() {
    const [ratesRes, settingsRes] = await Promise.all([ExchangeRatesApi.getAll(), LookupsApi.companySettings()]);
    setRates(ratesRes.data);
    setFunctionalCurrency(settingsRes.data.defaultCurrency);
  }

  useEffect(() => {
    load();
  }, []);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    try {
      await ExchangeRatesApi.set({ currencyCode, rateDate, rateToFunctional });
      setShowForm(false);
      setCurrencyCode("");
      setRateToFunctional(1);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  return (
    <div>
      <div className="page-header">
        <h1>{t.accounting.exchangeRatesTitle}</h1>
        <button className="btn" onClick={() => setShowForm((v) => !v)}>
          {showForm ? t.common.cancel : t.accounting.addRate}
        </button>
      </div>
      <p className="text-muted">{t.accounting.exchangeRatesIntro}</p>

      {error && <div className="alert-error">{error}</div>}

      {showForm && (
        <div className="card">
          <form onSubmit={handleSubmit}>
            <div className="form-grid">
              <div className="form-field">
                <label>{t.accounting.currencyCode}</label>
                <input
                  value={currencyCode}
                  onChange={(e) => setCurrencyCode(e.target.value.toUpperCase())}
                  placeholder="USD"
                  maxLength={10}
                  required
                />
              </div>
              <div className="form-field">
                <label>{t.accounting.rateDate}</label>
                <input type="date" value={rateDate} onChange={(e) => setRateDate(e.target.value)} required />
              </div>
              <div className="form-field">
                <label>{t.accounting.rateToFunctional} (1 {currencyCode || "..."} = ? {functionalCurrency})</label>
                <input type="number" min={0.000001} step="0.000001" value={rateToFunctional} onChange={(e) => setRateToFunctional(Number(e.target.value))} required />
              </div>
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
              <th>{t.accounting.currencyCode}</th>
              <th>{t.accounting.rateDate}</th>
              <th>{t.accounting.rateToFunctional}</th>
            </tr>
          </thead>
          <tbody>
            {rates.length === 0 && (
              <tr>
                <td colSpan={3} className="text-muted" style={{ textAlign: "center", padding: 20 }}>
                  {t.common.noData}
                </td>
              </tr>
            )}
            {rates.map((r) => (
              <tr key={r.id}>
                <td>{r.currencyCode}</td>
                <td>{new Date(r.rateDate).toLocaleDateString()}</td>
                <td>{r.rateToFunctional.toLocaleString(undefined, { maximumFractionDigits: 6 })} {functionalCurrency}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
