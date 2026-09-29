import { useEffect, useState } from "react";
import { LoginHistoryApi } from "../api/services";
import type { LoginHistoryEntry } from "../api/types";
import { getErrorMessage } from "../api/client";
import { useLanguage } from "../i18n/LanguageContext";

export default function LoginHistoryPage() {
  const { t, lang } = useLanguage();
  const [logs, setLogs] = useState<LoginHistoryEntry[] | null>(null);
  const [fromDate, setFromDate] = useState("");
  const [toDate, setToDate] = useState("");
  const [error, setError] = useState<string | null>(null);

  const methodLabel: Record<LoginHistoryEntry["method"], string> = {
    Password: t.loginHistory.methodPassword,
    PosPin: t.loginHistory.methodPosPin,
  };

  async function load() {
    setError(null);
    try {
      const res = await LoginHistoryApi.getAll({
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
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return (
    <div>
      <div className="page-header">
        <h1>{t.loginHistory.title}</h1>
      </div>
      <p className="text-muted">{t.loginHistory.intro}</p>

      <div className="card" style={{ marginBottom: 16 }}>
        <div className="form-grid">
          <div className="form-field">
            <label>{t.loginHistory.from}</label>
            <input type="date" value={fromDate} onChange={(e) => setFromDate(e.target.value)} />
          </div>
          <div className="form-field">
            <label>{t.loginHistory.to}</label>
            <input type="date" value={toDate} onChange={(e) => setToDate(e.target.value)} />
          </div>
          <div className="form-field" style={{ justifyContent: "flex-end" }}>
            <button className="btn btn-secondary btn-sm" onClick={load}>{t.loginHistory.apply}</button>
          </div>
        </div>
      </div>

      {error && <div className="alert-error">{error}</div>}

      {logs && logs.length === 0 && <div className="card text-muted">{t.loginHistory.noLogs}</div>}

      {logs && logs.length > 0 && (
        <table>
          <thead>
            <tr>
              <th>{t.loginHistory.occurredAt}</th>
              <th>{t.loginHistory.user}</th>
              <th>{t.loginHistory.ipAddress}</th>
              <th>{t.loginHistory.method}</th>
              <th>{t.loginHistory.result}</th>
            </tr>
          </thead>
          <tbody>
            {logs.map((log) => (
              <tr key={log.id}>
                <td>{new Date(log.occurredAtUtc).toLocaleString(lang === "ar" ? "ar-EG" : "en-US")}</td>
                <td>{log.userName}</td>
                <td>{log.ipAddress}</td>
                <td>{methodLabel[log.method]}</td>
                <td>
                  <span className="badge" style={{ color: log.success ? "var(--color-primary)" : "var(--color-danger)" }}>
                    {log.success ? t.loginHistory.resultSuccess : t.loginHistory.resultFailed}
                  </span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
