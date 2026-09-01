import { useEffect, useState } from "react";
import { FinancialReportsApi } from "../../api/services";
import type { ForecastReport } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";
import InfoTooltip from "../../components/InfoTooltip";

export default function ForecastPage() {
  const { t } = useLanguage();
  const [report, setReport] = useState<ForecastReport | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const today = new Date().toISOString().slice(0, 10);
    FinancialReportsApi.forecast(today, 6, 3)
      .then((res) => setReport(res.data))
      .catch((err) => setError(getErrorMessage(err)));
  }, []);

  return (
    <div>
      <div className="page-header">
        <h1>{t.accounting.forecastTitle}<InfoTooltip text={t.accounting.forecastIntro} /></h1>
      </div>
      <p className="text-muted">{t.accounting.forecastIntro}</p>

      {error && <div className="alert-error">{error}</div>}

      {report && report.historicalMonthsUsed === 0 && <div className="card text-muted">{t.common.noData}</div>}

      {report && report.historicalMonthsUsed > 0 && (
        <>
          <div className="card text-muted" style={{ fontSize: 13 }}>
            {t.accounting.historicalMonths}: {report.historicalMonthsUsed}
            {report.isLowConfidence && <div style={{ marginTop: 6 }}>⚠️ {t.accounting.forecastLowConfidenceNote}</div>}
          </div>

          <div className="card">
            <h3 style={{ marginTop: 0 }}>{t.accounting.revenue}</h3>
            <table>
              <thead>
                <tr>
                  <th>{t.accounting.month}</th>
                  <th>{t.accounting.worst}</th>
                  <th>{t.accounting.expected}</th>
                  <th>{t.accounting.best}</th>
                </tr>
              </thead>
              <tbody>
                {report.forecastMonths.map((m) => (
                  <tr key={m.monthLabel}>
                    <td>{m.monthLabel}</td>
                    <td>{m.worstRevenue.toLocaleString()}</td>
                    <td><strong>{m.expectedRevenue.toLocaleString()}</strong></td>
                    <td>{m.bestRevenue.toLocaleString()}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <div className="card">
            <h3 style={{ marginTop: 0 }}>{t.dashboard.netProfit}</h3>
            <table>
              <thead>
                <tr>
                  <th>{t.accounting.month}</th>
                  <th>{t.accounting.worst}</th>
                  <th>{t.accounting.expected}</th>
                  <th>{t.accounting.best}</th>
                </tr>
              </thead>
              <tbody>
                {report.forecastMonths.map((m) => (
                  <tr key={m.monthLabel}>
                    <td>{m.monthLabel}</td>
                    <td className={m.worstProfit >= 0 ? "text-success" : "text-danger"}>{m.worstProfit.toLocaleString()}</td>
                    <td className={m.expectedProfit >= 0 ? "text-success" : "text-danger"}><strong>{m.expectedProfit.toLocaleString()}</strong></td>
                    <td className={m.bestProfit >= 0 ? "text-success" : "text-danger"}>{m.bestProfit.toLocaleString()}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <div className="card">
            <h3 style={{ marginTop: 0 }}>{t.accounting.historicalMonths}</h3>
            <table>
              <thead>
                <tr>
                  <th>{t.accounting.month}</th>
                  <th>{t.accounting.revenue}</th>
                  <th>{t.accounting.types.expense}</th>
                  <th>{t.dashboard.netProfit}</th>
                </tr>
              </thead>
              <tbody>
                {report.historicalMonths.map((m) => (
                  <tr key={m.monthLabel}>
                    <td>{m.monthLabel}</td>
                    <td>{m.revenue.toLocaleString()}</td>
                    <td>{m.expense.toLocaleString()}</td>
                    <td className={m.netIncome >= 0 ? "text-success" : "text-danger"}>{m.netIncome.toLocaleString()}</td>
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
