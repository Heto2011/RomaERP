import { useEffect, useState } from "react";
import { FinancialReportsApi } from "../../api/services";
import type { CashFlowIntelligence } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";

export default function CashFlowIntelligencePage() {
  const { t } = useLanguage();
  const [report, setReport] = useState<CashFlowIntelligence | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function load() {
    setError(null);
    try {
      const res = await FinancialReportsApi.cashFlowIntelligence(new Date().toISOString().slice(0, 10));
      setReport(res.data);
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  useEffect(() => {
    load();
  }, []);

  const maxAbsBalance = report
    ? Math.max(1, Math.abs(report.currentCashBalance), ...report.projectedWeeks.map((w) => Math.abs(w.projectedEndingBalance)))
    : 1;

  return (
    <div>
      <div className="page-header">
        <h1>{t.accounting.cashFlowIntelligenceTitle}</h1>
      </div>
      <p className="text-muted">{t.accounting.cashFlowIntelligenceIntro}</p>

      {error && <div className="alert-error">{error}</div>}

      {report && (
        <>
          {report.isLowConfidence && (
            <div className="alert-error" style={{ background: "var(--color-bg)", borderColor: "var(--color-border)" }}>
              {t.accounting.cashFlowLowConfidence}
            </div>
          )}

          {report.firstWeekBelowZero && (
            <div className="alert-error">
              {t.accounting.cashFlowNegativeAlert} — {new Date(report.firstWeekBelowZero).toLocaleDateString()}
            </div>
          )}

          <div className="stat-grid" style={{ marginTop: 16, marginBottom: 16 }}>
            <div className="stat-card">
              <div className="label">{t.accounting.currentCashBalance}</div>
              <div className="value">{report.currentCashBalance.toLocaleString()}</div>
            </div>
            <div className="stat-card">
              <div className="label">{t.accounting.averageWeeklyNetChange}</div>
              <div className="value" style={{ color: report.averageWeeklyNetChange >= 0 ? "var(--color-success)" : "var(--color-danger)" }}>
                {report.averageWeeklyNetChange.toLocaleString()}
              </div>
            </div>
            <div className="stat-card">
              <div className="label">{t.accounting.historicalWeeksUsed}</div>
              <div className="value">{report.historicalWeeksUsed}</div>
            </div>
          </div>

          <div className="card">
            <h3>{t.accounting.cashFlowProjectionTitle}</h3>
            <div style={{ display: "flex", flexDirection: "column", gap: 6, marginTop: 10 }}>
              {report.projectedWeeks.map((w) => (
                <div key={w.weekStart} style={{ display: "flex", alignItems: "center", gap: 10 }}>
                  <div style={{ width: 90, fontSize: 13 }} className="text-muted">{new Date(w.weekStart).toLocaleDateString()}</div>
                  <div style={{ flex: 1, background: "var(--color-bg)", borderRadius: 4, overflow: "hidden", position: "relative", height: 16 }}>
                    <div
                      style={{
                        width: `${(Math.abs(w.projectedEndingBalance) / maxAbsBalance) * 100}%`,
                        background: w.isBelowZero ? "var(--color-danger)" : "var(--color-success)",
                        height: 16,
                        minWidth: w.projectedEndingBalance !== 0 ? 2 : 0,
                      }}
                    />
                  </div>
                  <div style={{ width: 110, textAlign: "end", fontSize: 13 }} className={w.isBelowZero ? "text-danger" : undefined}>
                    {w.projectedEndingBalance.toLocaleString()}
                  </div>
                </div>
              ))}
            </div>
          </div>

          <div className="card" style={{ marginTop: 16 }}>
            <table>
              <thead>
                <tr>
                  <th>{t.accounting.week}</th>
                  <th>{t.accounting.projectedNetChange}</th>
                  <th>{t.accounting.projectedEndingBalance}</th>
                </tr>
              </thead>
              <tbody>
                {report.projectedWeeks.map((w) => (
                  <tr key={w.weekStart}>
                    <td>{new Date(w.weekStart).toLocaleDateString()}</td>
                    <td className={w.projectedNetChange >= 0 ? "text-success" : "text-danger"}>{w.projectedNetChange.toLocaleString()}</td>
                    <td className={w.isBelowZero ? "text-danger" : undefined}>{w.projectedEndingBalance.toLocaleString()}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      )}
    </div>
  );
}
