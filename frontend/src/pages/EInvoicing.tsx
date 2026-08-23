import { useEffect, useState } from "react";
import { EInvoicingApi } from "../api/services";
import { EInvoicingEnvironment, EInvoicingProvider, type EInvoicingSettings } from "../api/types";
import { getErrorMessage } from "../api/client";
import { useLanguage } from "../i18n/LanguageContext";

export default function EInvoicing() {
  const { t } = useLanguage();
  const [settings, setSettings] = useState<EInvoicingSettings | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  const [provider, setProvider] = useState<EInvoicingProvider>(EInvoicingProvider.None);
  const [environment, setEnvironment] = useState<EInvoicingEnvironment>(EInvoicingEnvironment.Sandbox);
  const [clientId, setClientId] = useState("");
  const [clientSecret, setClientSecret] = useState("");
  const [certificate, setCertificate] = useState("");
  const [privateKey, setPrivateKey] = useState("");

  async function load() {
    const res = await EInvoicingApi.getSettings();
    setSettings(res.data);
    setProvider(res.data.provider);
    setEnvironment(res.data.environment);
  }

  useEffect(() => {
    load();
  }, []);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    setSuccess(null);
    try {
      const res = await EInvoicingApi.updateSettings({
        provider,
        environment,
        clientId: clientId || null,
        clientSecret: clientSecret || null,
        certificate: certificate || null,
        privateKey: privateKey || null,
      });
      setSettings(res.data);
      setClientId("");
      setClientSecret("");
      setCertificate("");
      setPrivateKey("");
      setSuccess(t.eInvoicing.savedSuccessfully);
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  return (
    <div>
      <div className="page-header">
        <h1>{t.eInvoicing.title}</h1>
      </div>

      <p className="text-muted" style={{ maxWidth: 720 }}>{t.eInvoicing.intro}</p>

      {error && <div className="alert-error">{error}</div>}
      {success && <div className="alert-success">{success}</div>}

      <div className="card">
        <form onSubmit={handleSubmit}>
          <div className="form-grid">
            <div className="form-field">
              <label>{t.eInvoicing.provider}</label>
              <select value={provider} onChange={(e) => setProvider(Number(e.target.value))}>
                <option value={EInvoicingProvider.None}>{t.eInvoicing.providers.none}</option>
                <option value={EInvoicingProvider.Eta}>{t.eInvoicing.providers.eta}</option>
                <option value={EInvoicingProvider.Zatca}>{t.eInvoicing.providers.zatca}</option>
              </select>
            </div>
            {provider !== EInvoicingProvider.None && (
              <div className="form-field">
                <label>{t.eInvoicing.environment}</label>
                <select value={environment} onChange={(e) => setEnvironment(Number(e.target.value))}>
                  <option value={EInvoicingEnvironment.Sandbox}>{t.eInvoicing.environments.sandbox}</option>
                  <option value={EInvoicingEnvironment.Production}>{t.eInvoicing.environments.production}</option>
                </select>
              </div>
            )}
          </div>

          {provider !== EInvoicingProvider.None && (
            <>
              <div className="form-grid" style={{ marginTop: 14 }}>
                <div className="form-field">
                  <label>{t.eInvoicing.clientId}</label>
                  <input value={clientId} onChange={(e) => setClientId(e.target.value)} placeholder={settings?.hasClientCredentials ? t.eInvoicing.leaveBlankToKeep : ""} />
                </div>
                <div className="form-field">
                  <label>{t.eInvoicing.clientSecret}</label>
                  <input type="password" value={clientSecret} onChange={(e) => setClientSecret(e.target.value)} placeholder={settings?.hasClientCredentials ? t.eInvoicing.leaveBlankToKeep : ""} />
                </div>
              </div>
              <div className="text-muted" style={{ fontSize: 13, marginTop: 4 }}>
                {t.eInvoicing.hasClientCredentials}: <strong className={settings?.hasClientCredentials ? "text-success" : "text-danger"}>{settings?.hasClientCredentials ? t.eInvoicing.stored : t.eInvoicing.notStored}</strong>
              </div>

              {provider === EInvoicingProvider.Zatca && (
                <>
                  <div className="form-grid" style={{ marginTop: 14 }}>
                    <div className="form-field">
                      <label>{t.eInvoicing.certificate}</label>
                      <textarea value={certificate} onChange={(e) => setCertificate(e.target.value)} rows={4} placeholder={settings?.hasCertificate ? t.eInvoicing.leaveBlankToKeep : ""} />
                    </div>
                    <div className="form-field">
                      <label>{t.eInvoicing.privateKey}</label>
                      <textarea value={privateKey} onChange={(e) => setPrivateKey(e.target.value)} rows={4} placeholder={settings?.hasCertificate ? t.eInvoicing.leaveBlankToKeep : ""} />
                    </div>
                  </div>
                  <div className="text-muted" style={{ fontSize: 13, marginTop: 4 }}>
                    {t.eInvoicing.hasCertificate}: <strong className={settings?.hasCertificate ? "text-success" : "text-danger"}>{settings?.hasCertificate ? t.eInvoicing.stored : t.eInvoicing.notStored}</strong>
                  </div>
                </>
              )}
            </>
          )}

          <button className="btn" type="submit" style={{ marginTop: 14 }}>
            {t.eInvoicing.saveSettings}
          </button>
        </form>
      </div>
    </div>
  );
}
