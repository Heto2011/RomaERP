import { useEffect, useState } from "react";
import { PayrollApi } from "../../api/services";
import type { PayrollSettings } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";

export default function PayrollSettingsPage() {
  const { t } = useLanguage();
  const [settings, setSettings] = useState<PayrollSettings | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);

  async function load() {
    const res = await PayrollApi.getSettings();
    setSettings(res.data);
  }

  useEffect(() => {
    load();
  }, []);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!settings) return;
    setError(null);
    setSaved(false);
    try {
      const res = await PayrollApi.updateSettings(settings);
      setSettings(res.data);
      setSaved(true);
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  if (!settings) return null;

  return (
    <div>
      <div className="page-header">
        <h1>{t.hr.payrollSettingsTitle}</h1>
      </div>
      <p className="text-muted">{t.hr.payrollSettingsIntro}</p>

      {error && <div className="alert-error">{error}</div>}
      {saved && <div className="alert-success">{t.common.saved}</div>}

      <div className="card" style={{ maxWidth: 520 }}>
        <form onSubmit={handleSubmit}>
          <div className="form-field">
            <label>{t.hr.payrollDaysPerMonth}</label>
            <input
              type="number"
              min={1}
              value={settings.payrollDaysPerMonth}
              onChange={(e) => setSettings({ ...settings, payrollDaysPerMonth: Number(e.target.value) })}
              required
            />
          </div>

          <hr style={{ margin: "16px 0" }} />

          <div className="form-field">
            <label>
              <input
                type="checkbox"
                checked={settings.gosiEnabled}
                onChange={(e) => setSettings({ ...settings, gosiEnabled: e.target.checked })}
                style={{ marginInlineEnd: 6 }}
              />
              {t.hr.gosiEnabled}
            </label>
          </div>

          <p className="text-muted" style={{ fontSize: 13 }}>{t.hr.gosiRatesDisclaimer}</p>

          <div className="form-grid">
            <div className="form-field">
              <label>{t.hr.gosiEmployeeRate}</label>
              <input
                type="number"
                step="0.01"
                min={0}
                value={settings.gosiEmployeeRatePercent}
                onChange={(e) => setSettings({ ...settings, gosiEmployeeRatePercent: Number(e.target.value) })}
                disabled={!settings.gosiEnabled}
              />
            </div>
            <div className="form-field">
              <label>{t.hr.gosiEmployerAnnuitiesRate}</label>
              <input
                type="number"
                step="0.01"
                min={0}
                value={settings.gosiEmployerAnnuitiesRatePercent}
                onChange={(e) => setSettings({ ...settings, gosiEmployerAnnuitiesRatePercent: Number(e.target.value) })}
                disabled={!settings.gosiEnabled}
              />
            </div>
            <div className="form-field">
              <label>{t.hr.gosiEmployerHazardsRate}</label>
              <input
                type="number"
                step="0.01"
                min={0}
                value={settings.gosiEmployerHazardsRatePercent}
                onChange={(e) => setSettings({ ...settings, gosiEmployerHazardsRatePercent: Number(e.target.value) })}
                disabled={!settings.gosiEnabled}
              />
            </div>
          </div>

          <p className="text-muted" style={{ fontSize: 13 }}>{t.hr.gosiEligibilityNote}</p>

          <button className="btn" type="submit" style={{ marginTop: 14 }}>
            {t.common.save}
          </button>
        </form>
      </div>
    </div>
  );
}
