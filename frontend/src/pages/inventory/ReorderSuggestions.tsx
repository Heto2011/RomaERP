import { useEffect, useState } from "react";
import { InventoryReportsApi } from "../../api/services";
import type { ReorderSuggestionReport } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";
import InfoTooltip from "../../components/InfoTooltip";

export default function ReorderSuggestionsPage() {
  const { t } = useLanguage();
  const [lookback, setLookback] = useState(30);
  const [report, setReport] = useState<ReorderSuggestionReport | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function load() {
    setError(null);
    try {
      const res = await InventoryReportsApi.reorderSuggestions(lookback);
      setReport(res.data);
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
        <h1>{t.inventory.reorderSuggestionsTitle}<InfoTooltip text={t.inventory.reorderSuggestionsIntro} /></h1>
      </div>
      <p className="text-muted">{t.inventory.reorderSuggestionsIntro}</p>

      <div className="card toolbar">
        <div className="form-field">
          <label>{t.inventory.reorderLookback}</label>
          <input type="number" min={7} max={180} value={lookback} onChange={(e) => setLookback(Number(e.target.value) || 30)} />
        </div>
        <button className="btn" style={{ alignSelf: "flex-end" }} onClick={load}>
          {t.common.viewReport}
        </button>
      </div>

      {error && <div className="alert-error">{error}</div>}

      {report && report.items.length === 0 && <div className="card text-muted">{t.inventory.reorderNone}</div>}

      {report && report.items.length > 0 && (
        <div className="card" style={{ overflowX: "auto" }}>
          <table>
            <thead>
              <tr>
                <th>{t.inventory.itemCode}</th>
                <th>{t.common.description}</th>
                <th>{t.inventory.quantityOnHand}</th>
                <th>{t.inventory.dailyConsumption}</th>
                <th>{t.inventory.stockDaysCover}</th>
                <th>{t.inventory.leadTimeDays}</th>
                <th>{t.inventory.reorderPoint}</th>
                <th>{t.inventory.suggestedQty}</th>
                <th>{t.inventory.estimatedCost}</th>
                <th>{t.inventory.lastVendor}</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {report.items.map((l) => (
                <tr key={l.itemId}>
                  <td>{l.itemCode}</td>
                  <td>{l.itemName}</td>
                  <td className={l.urgency === "Critical" ? "text-danger" : undefined}>
                    {l.quantityOnHand.toLocaleString()} {l.unitOfMeasure}
                  </td>
                  <td>{l.dailyConsumption > 0 ? l.dailyConsumption.toLocaleString() : "—"}</td>
                  <td>{l.daysOfStockRemaining != null ? l.daysOfStockRemaining.toLocaleString() : "—"}</td>
                  <td>{l.leadTimeDays}</td>
                  <td>{l.reorderPoint.toLocaleString()}</td>
                  <td><strong>{l.suggestedOrderQuantity.toLocaleString()}</strong> {l.unitOfMeasure}</td>
                  <td>{l.estimatedCost.toLocaleString()}</td>
                  <td>{l.lastVendorName ?? "—"}</td>
                  <td>
                    <span className={l.urgency === "Critical" ? "text-danger" : "text-muted"}>
                      {l.urgency === "Critical" ? t.inventory.urgencyCritical : t.inventory.urgencyWarning}
                    </span>
                    <div className="text-muted" style={{ fontSize: 12 }}>
                      {l.drivenByConsumption ? t.inventory.reorderBasisConsumption : t.inventory.reorderBasisFixed}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
            <tfoot>
              <tr>
                <td colSpan={8}><strong>{t.inventory.reorderTotal}</strong></td>
                <td colSpan={3}><strong>{report.totalEstimatedCost.toLocaleString()}</strong></td>
              </tr>
            </tfoot>
          </table>
        </div>
      )}
    </div>
  );
}
