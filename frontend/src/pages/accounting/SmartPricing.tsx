import { useEffect, useState } from "react";
import { InventoryReportsApi } from "../../api/services";
import type { RecipeCostReport } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";
import InfoTooltip from "../../components/InfoTooltip";

export default function SmartPricingPage() {
  const { t } = useLanguage();
  const [report, setReport] = useState<RecipeCostReport | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [targetMargin, setTargetMargin] = useState(30);

  useEffect(() => {
    InventoryReportsApi.recipeCost()
      .then((res) => setReport(res.data))
      .catch((err) => setError(getErrorMessage(err)));
  }, []);

  function suggestedPrice(cost: number): number {
    if (targetMargin >= 100) return cost;
    return cost / (1 - targetMargin / 100);
  }

  return (
    <div>
      <div className="page-header">
        <h1>{t.accounting.smartPricingTitle}<InfoTooltip text={t.accounting.smartPricingIntro} /></h1>
      </div>
      <p className="text-muted">{t.accounting.smartPricingIntro}</p>

      <div className="card toolbar">
        <div className="form-field">
          <label>{t.accounting.targetMarginPercent}</label>
          <input
            type="number"
            min={0}
            max={99}
            step="1"
            value={targetMargin}
            onChange={(e) => setTargetMargin(Number(e.target.value))}
            style={{ width: 100 }}
          />
        </div>
      </div>

      {error && <div className="alert-error">{error}</div>}

      {report && report.items.length === 0 && <div className="card text-muted">{t.common.noData}</div>}

      {report && report.items.length > 0 && (
        <div className="card">
          <table>
            <thead>
              <tr>
                <th>{t.inventory.itemCode}</th>
                <th>{t.common.description}</th>
                <th>{t.accounting.cost}</th>
                <th>{t.inventory.sellingPrice}</th>
                <th>{t.accounting.margin}</th>
                <th>{t.accounting.suggestedPrice}</th>
                <th>{t.accounting.priceGap}</th>
              </tr>
            </thead>
            <tbody>
              {report.items.map((l) => {
                const suggested = suggestedPrice(l.recipeCost);
                const gap = suggested - l.menuPrice;
                return (
                  <tr key={l.itemId}>
                    <td>{l.itemCode}</td>
                    <td>{l.itemName}</td>
                    <td>{l.recipeCost.toLocaleString()}</td>
                    <td>{l.menuPrice.toLocaleString()}</td>
                    <td className={l.marginPercent >= targetMargin ? "text-success" : "text-danger"}>{l.marginPercent.toFixed(1)}%</td>
                    <td>{suggested.toLocaleString(undefined, { maximumFractionDigits: 2 })}</td>
                    <td className={gap > 0 ? "text-danger" : "text-success"}>
                      {gap > 0 ? "+" : ""}{gap.toLocaleString(undefined, { maximumFractionDigits: 2 })}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
