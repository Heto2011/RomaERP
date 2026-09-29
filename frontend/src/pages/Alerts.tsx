import { useEffect, useState } from "react";
import { AlertsApi, WhatsAppApi } from "../api/services";
import { AlertSeverity, type AlertsReport, type WhatsAppStatus } from "../api/types";
import { getErrorMessage } from "../api/client";
import { useLanguage } from "../i18n/LanguageContext";
import { useAuth } from "../context/AuthContext";
import PasswordInput from "../components/PasswordInput";

export default function AlertsPage() {
  const { t } = useLanguage();
  const { user } = useAuth();
  const isAdmin = user?.roles.includes("Admin") ?? false;
  const [report, setReport] = useState<AlertsReport | null>(null);
  const [error, setError] = useState<string | null>(null);

  const [status, setStatus] = useState<WhatsAppStatus | null>(null);
  const [phoneNumberId, setPhoneNumberId] = useState("");
  const [accessToken, setAccessToken] = useState("");
  const [recipientPhoneNumber, setRecipientPhoneNumber] = useState("");
  const [templateName, setTemplateName] = useState("romaerp_alert");
  const [templateLanguageCode, setTemplateLanguageCode] = useState("ar");
  const [isEnabled, setIsEnabled] = useState(false);
  const [whatsAppMessage, setWhatsAppMessage] = useState<string | null>(null);
  const [whatsAppError, setWhatsAppError] = useState<string | null>(null);
  const [sending, setSending] = useState(false);

  const severityLabel: Record<AlertSeverity, string> = {
    [AlertSeverity.Info]: t.alerts.severityInfo,
    [AlertSeverity.Warning]: t.alerts.severityWarning,
    [AlertSeverity.Critical]: t.alerts.severityCritical,
  };

  const severityClass: Record<AlertSeverity, string> = {
    [AlertSeverity.Info]: "badge",
    [AlertSeverity.Warning]: "badge badge-reversed",
    [AlertSeverity.Critical]: "badge",
  };

  const severityColor: Record<AlertSeverity, string> = {
    [AlertSeverity.Info]: "var(--color-muted)",
    [AlertSeverity.Warning]: "var(--color-primary)",
    [AlertSeverity.Critical]: "var(--color-danger)",
  };

  async function load() {
    setError(null);
    try {
      const res = await AlertsApi.getAll();
      setReport(res.data);
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  async function loadWhatsAppStatus() {
    try {
      const res = await WhatsAppApi.getStatus();
      setStatus(res.data);
      setPhoneNumberId(res.data.phoneNumberId ?? "");
      setRecipientPhoneNumber(res.data.recipientPhoneNumber ?? "");
      setTemplateName(res.data.templateName);
      setTemplateLanguageCode(res.data.templateLanguageCode);
      setIsEnabled(res.data.isEnabled);
    } catch {
      // WhatsApp settings are optional — a load failure here shouldn't block the alerts list itself.
    }
  }

  useEffect(() => {
    load();
    if (isAdmin) loadWhatsAppStatus();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  async function handleSaveWhatsApp(e: React.FormEvent) {
    e.preventDefault();
    setWhatsAppError(null);
    setWhatsAppMessage(null);
    try {
      const res = await WhatsAppApi.saveCredential({
        phoneNumberId,
        accessToken: accessToken || undefined,
        recipientPhoneNumber,
        templateName,
        templateLanguageCode,
        isEnabled,
      });
      setStatus(res.data);
      setAccessToken("");
      setWhatsAppMessage(t.alerts.whatsAppSaved);
    } catch (err) {
      setWhatsAppError(getErrorMessage(err));
    }
  }

  async function handleSendTest() {
    setWhatsAppError(null);
    setWhatsAppMessage(null);
    setSending(true);
    try {
      const res = await WhatsAppApi.sendTest();
      if (res.data.success) setWhatsAppMessage(t.alerts.whatsAppSentSuccess);
      else setWhatsAppError(res.data.failureReason ?? "");
    } catch (err) {
      setWhatsAppError(getErrorMessage(err));
    } finally {
      setSending(false);
    }
  }

  async function handleSendAlertsNow() {
    setWhatsAppError(null);
    setWhatsAppMessage(null);
    setSending(true);
    try {
      const res = await WhatsAppApi.sendAlertsNow();
      if (res.data.success) setWhatsAppMessage(t.alerts.whatsAppSentSuccess);
      else setWhatsAppError(res.data.failureReason ?? "");
    } catch (err) {
      setWhatsAppError(getErrorMessage(err));
    } finally {
      setSending(false);
    }
  }

  return (
    <div>
      <div className="page-header">
        <h1>{t.alerts.title}</h1>
        <div style={{ display: "flex", gap: 8 }}>
          {isAdmin && status?.isConfigured && (
            <button className="btn btn-secondary btn-sm" onClick={handleSendAlertsNow} disabled={sending}>
              {t.alerts.sendViaWhatsApp}
            </button>
          )}
          <button className="btn btn-secondary btn-sm" onClick={load}>{t.common.viewReport}</button>
        </div>
      </div>
      <p className="text-muted">{t.alerts.intro}</p>

      {error && <div className="alert-error">{error}</div>}

      {report && report.alerts.length === 0 && <div className="card text-muted">{t.alerts.noAlerts}</div>}

      {report && report.alerts.length > 0 && (
        <div style={{ display: "flex", flexDirection: "column", gap: 10, marginBottom: 20 }}>
          {report.alerts.map((a, idx) => (
            <div className="card" key={idx} style={{ borderInlineStart: `4px solid ${severityColor[a.severity]}` }}>
              <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", gap: 10 }}>
                <strong>{a.title}</strong>
                <span className={severityClass[a.severity]} style={{ color: severityColor[a.severity] }}>
                  {severityLabel[a.severity]}
                </span>
              </div>
              <div className="text-muted" style={{ fontSize: 13, marginTop: 6 }}>{a.category}</div>
              {a.detail && <div style={{ marginTop: 6 }}>{a.detail}</div>}
            </div>
          ))}
        </div>
      )}

      {isAdmin && (
      <div className="card">
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
          <h3 style={{ marginTop: 0 }}>{t.alerts.whatsAppSectionTitle}</h3>
          <span className={`badge ${status?.isConfigured ? "badge-posted" : "badge-draft"}`}>
            {status?.isConfigured ? t.alerts.whatsAppConfigured : t.alerts.whatsAppNotConfigured}
          </span>
        </div>
        <p className="text-muted" style={{ marginTop: 0 }}>{t.alerts.whatsAppIntro}</p>

        {whatsAppError && <div className="alert-error">{whatsAppError}</div>}
        {whatsAppMessage && <div className="alert-success">{whatsAppMessage}</div>}

        <form onSubmit={handleSaveWhatsApp} className="form-grid">
          <div className="form-field">
            <label>{t.alerts.whatsAppPhoneNumberId}</label>
            <input value={phoneNumberId} onChange={(e) => setPhoneNumberId(e.target.value)} required />
          </div>
          <div className="form-field">
            <label>{t.alerts.whatsAppAccessToken}</label>
            <PasswordInput value={accessToken} onChange={setAccessToken} />
            <span className="text-muted" style={{ fontSize: 12 }}>{t.alerts.whatsAppAccessTokenHint}</span>
          </div>
          <div className="form-field">
            <label>{t.alerts.whatsAppRecipient}</label>
            <input value={recipientPhoneNumber} onChange={(e) => setRecipientPhoneNumber(e.target.value)} required />
          </div>
          <div className="form-field">
            <label>{t.alerts.whatsAppTemplateName}</label>
            <input value={templateName} onChange={(e) => setTemplateName(e.target.value)} required />
          </div>
          <div className="form-field">
            <label>{t.alerts.whatsAppTemplateLanguage}</label>
            <input value={templateLanguageCode} onChange={(e) => setTemplateLanguageCode(e.target.value)} required />
          </div>
          <div className="form-field" style={{ justifyContent: "flex-end" }}>
            <label style={{ display: "flex", alignItems: "center", gap: 8 }}>
              <input type="checkbox" checked={isEnabled} onChange={(e) => setIsEnabled(e.target.checked)} />
              {t.alerts.whatsAppEnable}
            </label>
          </div>
          <div style={{ display: "flex", gap: 8, alignSelf: "flex-end" }}>
            <button className="btn" type="submit">{t.alerts.whatsAppSave}</button>
            {status?.isConfigured && (
              <button className="btn btn-secondary" type="button" onClick={handleSendTest} disabled={sending}>
                {t.alerts.whatsAppSendTest}
              </button>
            )}
          </div>
        </form>
      </div>
      )}
    </div>
  );
}
