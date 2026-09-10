import { useEffect, useState } from "react";
import { AlertsApi } from "../api/services";
import { AlertSeverity, type AlertsReport } from "../api/types";
import { getErrorMessage } from "../api/client";
import { useLanguage } from "../i18n/LanguageContext";

export default function AlertsPage() {
  const { t } = useLanguage();
  const [report, setReport] = useState<AlertsReport | null>(null);
  const [error, setError] = useState<string | null>(null);

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

  useEffect(() => {
    load();
  }, []);

  return (
    <div>
      <div className="page-header">
        <h1>{t.alerts.title}</h1>
        <button className="btn btn-secondary btn-sm" onClick={load}>{t.common.viewReport}</button>
      </div>
      <p className="text-muted">{t.alerts.intro}</p>

      {error && <div className="alert-error">{error}</div>}

      {report && report.alerts.length === 0 && <div className="card text-muted">{t.alerts.noAlerts}</div>}

      {report && report.alerts.length > 0 && (
        <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
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
    </div>
  );
}
