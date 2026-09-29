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
  const [refreshing, setRefreshing] = useState(false);

  const [trackCurrencyCode, setTrackCurrencyCode] = useState("");
  const [tracking, setTracking] = useState(false);

  const [showManualForm, setShowManualForm] = useState(false);
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

  async function handleTrack(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    setTracking(true);
    try {
      await ExchangeRatesApi.track(trackCurrencyCode);
      setTrackCurrencyCode("");
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setTracking(false);
    }
  }

  async function handleRefreshNow() {
    setError(null);
    setRefreshing(true);
    try {
      await ExchangeRatesApi.refresh();
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setRefreshing(false);
    }
  }

  async function handleManualSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    try {
      await ExchangeRatesApi.set({ currencyCode, rateDate, rateToFunctional });
      setShowManualForm(false);
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
        <button className="btn btn-secondary" onClick={handleRefreshNow} disabled={refreshing}>
          {refreshing ? t.common.loading : `🔄 ${t.accounting.refreshRatesNow}`}
        </button>
      </div>
      <p className="text-muted">{t.accounting.exchangeRatesIntro}</p>

      {error && <div className="alert-error">{error}</div>}

      <div className="card">
        <form onSubmit={handleTrack} style={{ display: "flex", gap: 10, alignItems: "flex-end", flexWrap: "wrap" }}>
          <div className="form-field" style={{ maxWidth: 200 }}>
            <label>{t.accounting.trackCurrencyLabel}</label>
            <input
              value={trackCurrencyCode}
              onChange={(e) => setTrackCurrencyCode(e.target.value.toUpperCase())}
              placeholder="USD"
              maxLength={10}
              required
            />
          </div>
          <button className="btn" type="submit" disabled={tracking}>
            {tracking ? t.common.loading : `📡 ${t.accounting.trackCurrencyButton}`}
          </button>
        </form>
        <p className="text-muted" style={{ marginTop: 8, marginBottom: 0 }}>{t.accounting.trackCurrencyHint}</p>
      </div>

      <div className="card">
        <table>
          <thead>
            <tr>
              <th>{t.accounting.currencyCode}</th>
              <th>{t.accounting.rateDate}</th>
              <th>{t.accounting.rateToFunctional}</th>
              <th>{t.accounting.rateSource}</th>
            </tr>
          </thead>
          <tbody>
            {rates.length === 0 && (
              <tr>
                <td colSpan={4} className="text-muted" style={{ textAlign: "center", padding: 20 }}>
                  {t.common.noData}
                </td>
              </tr>
            )}
            {rates.map((r) => (
              <tr key={r.id}>
                <td>{r.currencyCode}</td>
                <td>{new Date(r.rateDate).toLocaleDateString()}</td>
                <td>{r.rateToFunctional.toLocaleString(undefined, { maximumFractionDigits: 6 })} {functionalCurrency}</td>
                <td>
                  <span className={`badge ${r.source === "Auto" ? "badge-posted" : "badge-draft"}`}>
                    {r.source === "Auto" ? t.accounting.rateSourceAuto : t.accounting.rateSourceManual}
                  </span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="card">
        <button className="btn btn-secondary btn-sm" type="button" onClick={() => setShowManualForm((v) => !v)}>
          {showManualForm ? t.common.cancel : t.accounting.manualOverrideToggle}
        </button>
        <p className="text-muted" style={{ marginTop: 8 }}>{t.accounting.manualOverrideHint}</p>

        {showManualForm && (
          <form onSubmit={handleManualSubmit} style={{ marginTop: 10 }}>
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
        )}
      </div>
    </div>
  );
}
