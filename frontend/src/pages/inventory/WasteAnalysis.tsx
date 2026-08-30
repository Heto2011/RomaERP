import { useEffect, useState } from "react";
import { InventoryReportsApi } from "../../api/services";
import { WasteReason, type WasteAnalysisReport } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";

function firstDayOfMonth() {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-01`;
}

export default function WasteAnalysisPage() {
  const { t } = useLanguage();
  const [fromDate, setFromDate] = useState(firstDayOfMonth());
  const [toDate, setToDate] = useState(new Date().toISOString().slice(0, 10));
  const [report, setReport] = useState<WasteAnalysisReport | null>(null);
  const [error, setError] = useState<string | null>(null);

  const reasonLabel: Record<WasteReason, string> = {
    [WasteReason.Waste]: t.inventory.wasteReasonWaste,
    [WasteReason.Expired]: t.inventory.wasteReasonExpired,
    [WasteReason.Damaged]: t.inventory.wasteReasonDamaged,
    [WasteReason.ProductionWaste]: t.inventory.wasteReasonProductionWaste,
    [WasteReason.OverPortion]: t.inventory.wasteReasonOverPortion,
    [WasteReason.Unknown]: t.inventory.wasteReasonUnknown,
  };

  async function load() {
    setError(null);
    try {
      const res = await InventoryReportsApi.wasteAnalysis(fromDate, toDate);
      setReport(res.data);
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const maxWeeklyCost = report ? Math.max(1, ...report.weeklyTrend.map((p) => p.totalCost)) : 1;

  return (
    <div>
      <div className="page-header">
        <h1>{t.inventory.wasteAnalysisTitle}</h1>
      </div>
      <p className="text-muted">{t.inventory.wasteAnalysisIntro}</p>

      <div className="card toolbar">
        <div className="form-field">
          <label>{t.common.from}</label>
          <input type="date" value={fromDate} onChange={(e) => setFromDate(e.target.value)} />
        </div>
        <div className="form-field">
          <label>{t.common.to}</label>
          <input type="date" value={toDate} onChange={(e) => setToDate(e.target.value)} />
        </div>
        <button className="btn" style={{ alignSelf: "flex-end" }} onClick={load}>
          {t.common.viewReport}
        </button>
      </div>

      {error && <div className="alert-error">{error}</div>}

      {report && (
        <>
          <div className="stat-grid" style={{ marginTop: 16, marginBottom: 16 }}>
            <div className="stat-card">
              <div className="label">{t.inventory.totalWasteCost}</div>
              <div className="value" style={{ color: "var(--color-danger)" }}>{report.totalWasteCost.toLocaleString()}</div>
            </div>
            <div className="stat-card">
              <div className="label">{t.inventory.totalWasteQuantity}</div>
              <div className="value">{report.totalWasteQuantity.toLocaleString()}</div>
            </div>
            <div className="stat-card">
              <div className="label">{t.inventory.wasteCostPercentOfCogs}</div>
              <div className="value">{report.wasteCostPercentOfCogs != null ? `${report.wasteCostPercentOfCogs.toFixed(1)}%` : t.common.noData}</div>
            </div>
          </div>

          {report.weeklyTrend.length > 0 && (
            <div className="card" style={{ marginBottom: 16 }}>
              <h3>{t.inventory.weeklyWasteTrend}</h3>
              <div style={{ display: "flex", flexDirection: "column", gap: 6, marginTop: 10 }}>
                {report.weeklyTrend.map((p) => (
                  <div key={p.weekStart} style={{ display: "flex", alignItems: "center", gap: 10 }}>
                    <div style={{ width: 90, fontSize: 13 }} className="text-muted">{new Date(p.weekStart).toLocaleDateString()}</div>
                    <div style={{ flex: 1, background: "var(--color-bg)", borderRadius: 4, overflow: "hidden" }}>
                      <div
                        style={{
                          width: `${(p.totalCost / maxWeeklyCost) * 100}%`,
                          background: "var(--color-danger)",
                          height: 16,
                          minWidth: p.totalCost > 0 ? 2 : 0,
                        }}
                      />
                    </div>
                    <div style={{ width: 90, textAlign: "end", fontSize: 13 }}>{p.totalCost.toLocaleString()}</div>
                  </div>
                ))}
              </div>
            </div>
          )}

          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 16 }}>
            <div className="card">
              <h3>{t.inventory.topWastedItems}</h3>
              {report.topWastedItems.length === 0 && <div className="text-muted">{t.common.noData}</div>}
              {report.topWastedItems.length > 0 && (
                <table>
                  <thead>
                    <tr>
                      <th>{t.inventory.itemCode}</th>
                      <th>{t.common.description}</th>
                      <th>{t.common.quantity}</th>
                      <th>{t.common.total}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {report.topWastedItems.map((l) => (
                      <tr key={l.itemId}>
                        <td>{l.itemCode}</td>
                        <td>{l.itemName}</td>
                        <td>{l.totalQuantity.toLocaleString()}</td>
                        <td>{l.totalCost.toLocaleString()}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </div>

            <div className="card">
              <h3>{t.inventory.wasteByReason}</h3>
              {report.byReason.length === 0 && <div className="text-muted">{t.common.noData}</div>}
              {report.byReason.length > 0 && (
                <table>
                  <thead>
                    <tr>
                      <th>{t.inventory.wasteReason}</th>
                      <th>{t.common.total}</th>
                      <th>%</th>
                    </tr>
                  </thead>
                  <tbody>
                    {report.byReason.map((l) => (
                      <tr key={l.reason}>
                        <td>{reasonLabel[l.reason]}</td>
                        <td>{l.totalCost.toLocaleString()}</td>
                        <td>{l.percentOfTotal.toFixed(1)}%</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </div>
          </div>
        </>
      )}
    </div>
  );
}
