import { useEffect, useState } from "react";
import { InventoryReportsApi } from "../../api/services";
import type { RecipeCostReport } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";

export default function RecipeCostPage() {
  const { t } = useLanguage();
  const [report, setReport] = useState<RecipeCostReport | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    InventoryReportsApi.recipeCost()
      .then((res) => setReport(res.data))
      .catch((err) => setError(getErrorMessage(err)));
  }, []);

  return (
    <div>
      <div className="page-header">
        <h1>{t.inventory.recipeCostTitle}</h1>
      </div>
      <p className="text-muted">{t.inventory.recipeCostIntro}</p>

      {error && <div className="alert-error">{error}</div>}

      {report && report.items.length === 0 && <div className="card text-muted">{t.common.noData}</div>}

      {report && report.items.length > 0 && (
        <div className="card">
          <table>
            <thead>
              <tr>
                <th>{t.inventory.itemCode}</th>
                <th>{t.common.description}</th>
                <th>{t.inventory.hasRecipe}</th>
                <th>{t.accounting.cost}</th>
                <th>{t.inventory.sellingPrice}</th>
                <th>{t.accounting.grossProfit}</th>
                <th>{t.accounting.margin}</th>
              </tr>
            </thead>
            <tbody>
              {report.items.map((l) => (
                <tr key={l.itemId}>
                  <td>{l.itemCode}</td>
                  <td>{l.itemName}</td>
                  <td>{l.hasRecipe ? "✓" : "—"}</td>
                  <td>{l.recipeCost.toLocaleString()}</td>
                  <td>{l.menuPrice.toLocaleString()}</td>
                  <td className={l.grossProfit >= 0 ? "text-success" : "text-danger"}>{l.grossProfit.toLocaleString()}</td>
                  <td>{l.marginPercent.toFixed(1)}%</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
