import { Fragment, useEffect, useState } from "react";
import { AuditLogApi } from "../api/services";
import type { AuditLogEntry } from "../api/types";
import { getErrorMessage } from "../api/client";
import { useLanguage } from "../i18n/LanguageContext";

export default function AuditLogPage() {
  const { t, lang } = useLanguage();
  const [logs, setLogs] = useState<AuditLogEntry[] | null>(null);
  const [entityNames, setEntityNames] = useState<string[]>([]);
  const [entityName, setEntityName] = useState("");
  const [fromDate, setFromDate] = useState("");
  const [toDate, setToDate] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [expandedId, setExpandedId] = useState<string | null>(null);

  const actionLabel: Record<AuditLogEntry["action"], string> = {
    Created: t.auditLog.actionCreated,
    Updated: t.auditLog.actionUpdated,
    Deleted: t.auditLog.actionDeleted,
  };
  const actionClass: Record<AuditLogEntry["action"], string> = {
    Created: "badge",
    Updated: "badge badge-reversed",
    Deleted: "badge",
  };
  const actionColor: Record<AuditLogEntry["action"], string> = {
    Created: "var(--color-primary)",
    Updated: "var(--color-muted)",
    Deleted: "var(--color-danger)",
  };

  async function load() {
    setError(null);
    try {
      const res = await AuditLogApi.getAll({
        entityName: entityName || undefined,
        fromUtc: fromDate ? new Date(fromDate).toISOString() : undefined,
        toUtc: toDate ? new Date(toDate + "T23:59:59").toISOString() : undefined,
      });
      setLogs(res.data);
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  useEffect(() => {
    load();
    AuditLogApi.getEntityNames().then((r) => setEntityNames(r.data)).catch(() => {});
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  function formatChanges(raw: string) {
    try {
      return JSON.stringify(JSON.parse(raw), null, 2);
    } catch {
      return raw;
    }
  }

  return (
    <div>
      <div className="page-header">
        <h1>{t.auditLog.title}</h1>
      </div>
      <p className="text-muted">{t.auditLog.intro}</p>

      <div className="card" style={{ marginBottom: 16 }}>
        <div className="form-grid">
          <div className="form-field">
            <label>{t.auditLog.entity}</label>
            <select value={entityName} onChange={(e) => setEntityName(e.target.value)}>
              <option value="">{t.auditLog.allEntities}</option>
              {entityNames.map((n) => (
                <option key={n} value={n}>{n}</option>
              ))}
            </select>
          </div>
          <div className="form-field">
            <label>{t.auditLog.from}</label>
            <input type="date" value={fromDate} onChange={(e) => setFromDate(e.target.value)} />
          </div>
          <div className="form-field">
            <label>{t.auditLog.to}</label>
            <input type="date" value={toDate} onChange={(e) => setToDate(e.target.value)} />
          </div>
          <div className="form-field" style={{ justifyContent: "flex-end" }}>
            <button className="btn btn-secondary btn-sm" onClick={load}>{t.auditLog.apply}</button>
          </div>
        </div>
      </div>

      {error && <div className="alert-error">{error}</div>}

      {logs && logs.length === 0 && <div className="card text-muted">{t.auditLog.noLogs}</div>}

      {logs && logs.length > 0 && (
        <table>
          <thead>
            <tr>
              <th>{t.auditLog.occurredAt}</th>
              <th>{t.auditLog.entity}</th>
              <th>{t.auditLog.action}</th>
              <th>{t.auditLog.user}</th>
              <th>{t.auditLog.details}</th>
            </tr>
          </thead>
          <tbody>
            {logs.map((log) => (
              <Fragment key={log.id}>
                <tr onClick={() => setExpandedId(expandedId === log.id ? null : log.id)} style={{ cursor: "pointer" }}>
                  <td>{new Date(log.occurredAtUtc).toLocaleString(lang === "ar" ? "ar-EG" : "en-US")}</td>
                  <td>{log.entityName}</td>
                  <td>
                    <span className={actionClass[log.action]} style={{ color: actionColor[log.action] }}>
                      {actionLabel[log.action]}
                    </span>
                  </td>
                  <td>{log.userName ?? "—"}</td>
                  <td className="text-muted">{expandedId === log.id ? "▲" : "▼"}</td>
                </tr>
                {expandedId === log.id && (
                  <tr>
                    <td colSpan={5}>
                      <pre style={{ margin: 0, fontSize: 12.5, whiteSpace: "pre-wrap", wordBreak: "break-all" }}>
                        {formatChanges(log.changes)}
                      </pre>
                    </td>
                  </tr>
                )}
              </Fragment>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
