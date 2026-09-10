import { useEffect, useState } from "react";
import { useLocation } from "react-router-dom";
import { InventoryReportsApi } from "../../api/services";
import type { InventoryMovementLine, InventoryMovementReport } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";
import InfoTooltip from "../../components/InfoTooltip";

function firstDayOfMonth() {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-01`;
}

type Filter = "all" | "atRisk" | "dead" | "excess" | "fast" | "slow";

const hashToFilter: Record<string, Filter> = {
  "#fast": "fast",
  "#slow": "slow",
  "#dead": "dead",
  "#atrisk": "atRisk",
  "#excess": "excess",
};

export default function InventoryMovementPage() {
  const { t } = useLanguage();
  const location = useLocation();
  const [fromDate, setFromDate] = useState(firstDayOfMonth());
  const [toDate, setToDate] = useState(new Date().toISOString().slice(0, 10));
  const [report, setReport] = useState<InventoryMovementReport | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [filter, setFilter] = useState<Filter>(hashToFilter[location.hash] ?? "all");

  async function load() {
    setError(null);
    try {
      const res = await InventoryReportsApi.movementAnalysis(fromDate, toDate);
      setReport(res.data);
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const fastMovers = report ? [...report.items].filter((l) => l.quantityIssuedInPeriod > 0).sort((a, b) => b.quantityIssuedInPeriod - a.quantityIssuedInPeriod).slice(0, 10) : [];
  const slowMovers = report ? [...report.items].filter((l) => l.quantityIssuedInPeriod > 0 && !l.isDeadStock).sort((a, b) => a.quantityIssuedInPeriod - b.quantityIssuedInPeriod).slice(0, 10) : [];

  const filtered: InventoryMovementLine[] = !report
    ? []
    : filter === "atRisk" ? report.items.filter((l) => l.isAtRiskOfStockout)
    : filter === "dead" ? report.items.filter((l) => l.isDeadStock)
    : filter === "excess" ? report.items.filter((l) => l.isExcessStock)
    : filter === "fast" ? fastMovers
    : filter === "slow" ? slowMovers
    : report.items;

  const filterButtons: { key: Filter; label: string }[] = [
    { key: "all", label: t.inventory.filterAll },
    { key: "fast", label: t.inventory.filterFastMoving },
    { key: "slow", label: t.inventory.filterSlowMoving },
    { key: "dead", label: t.inventory.filterDeadStock },
    { key: "atRisk", label: t.inventory.filterAtRisk },
    { key: "excess", label: t.inventory.filterExcessStock },
  ];

  return (
    <div>
      <div className="page-header">
        <h1>{t.inventory.movementAnalysisTitle}<InfoTooltip text={t.inventory.movementAnalysisIntro} /></h1>
      </div>
      <p className="text-muted">{t.inventory.movementAnalysisIntro}</p>

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
          <div style={{ display: "flex", gap: 6, flexWrap: "wrap", marginBottom: 12 }}>
            {filterButtons.map((b) => (
              <button
                key={b.key}
                className={filter === b.key ? "btn btn-sm" : "btn btn-secondary btn-sm"}
                onClick={() => setFilter(b.key)}
              >
                {b.label}
              </button>
            ))}
          </div>

          {filtered.length === 0 && <div className="card text-muted">{t.common.noData}</div>}

          {filtered.length > 0 && (
            <div className="card">
              <table>
                <thead>
                  <tr>
                    <th>{t.inventory.itemCode}</th>
                    <th>{t.common.description}</th>
                    <th>{t.inventory.quantityOnHand}</th>
                    <th>{t.inventory.reorderLevel}</th>
                    <th>{t.inventory.quantityIssued}</th>
                    <th>{t.inventory.cogsInPeriod}</th>
                    <th>{t.inventory.daysOfStock}</th>
                    <th>{t.inventory.turnoverRate}</th>
                  </tr>
                </thead>
                <tbody>
                  {filtered.map((l) => (
                    <tr key={l.itemId}>
                      <td>{l.itemCode}</td>
                      <td>{l.itemName}</td>
                      <td>{l.quantityOnHand.toLocaleString()}</td>
                      <td>{l.reorderLevel.toLocaleString()}</td>
                      <td>{l.quantityIssuedInPeriod.toLocaleString()}</td>
                      <td>{l.cogsInPeriod.toLocaleString()}</td>
                      <td>{l.daysOfStockRemaining ?? t.common.noData}</td>
                      <td>{l.turnoverRate.toFixed(2)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </>
      )}
    </div>
  );
}
