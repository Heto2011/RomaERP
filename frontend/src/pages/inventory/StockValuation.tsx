import { useEffect, useState } from "react";
import { InventoryReportsApi } from "../../api/services";
import type { StockValuationReport } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";
import InfoTooltip from "../../components/InfoTooltip";

export default function StockValuationPage() {
  const { t } = useLanguage();
  const [report, setReport] = useState<StockValuationReport | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    InventoryReportsApi.stockValuation()
      .then((res) => setReport(res.data))
      .catch((err) => setError(getErrorMessage(err)));
  }, []);

  return (
    <div>
      <div className="page-header">
        <h1>{t.inventory.stockValuationTitle}<InfoTooltip text={t.inventory.stockValuationIntro} /></h1>
      </div>
      <p className="text-muted">{t.inventory.stockValuationIntro}</p>

      {error && <div className="alert-error">{error}</div>}

      {report && report.items.length === 0 && <div className="card text-muted">{t.common.noData}</div>}

      {report && report.items.length > 0 && (
        <div className="card">
          <table>
            <thead>
              <tr>
                <th>{t.inventory.itemCode}</th>
                <th>{t.common.description}</th>
                <th>{t.inventory.category}</th>
                <th>{t.inventory.quantityOnHand}</th>
                <th>{t.inventory.averageCost}</th>
                <th>{t.inventory.value}</th>
              </tr>
            </thead>
            <tbody>
              {report.items.map((l) => (
                <tr key={l.itemId}>
                  <td>{l.itemCode}</td>
                  <td>{l.itemName}</td>
                  <td>{l.categoryName}</td>
                  <td>{l.quantityOnHand.toLocaleString()}</td>
                  <td>{l.averageCost.toLocaleString()}</td>
                  <td>{l.value.toLocaleString()}</td>
                </tr>
              ))}
            </tbody>
            <tfoot>
              <tr>
                <td colSpan={5}><strong>{t.inventory.totalValue}</strong></td>
                <td><strong>{report.totalValue.toLocaleString()}</strong></td>
              </tr>
            </tfoot>
          </table>
        </div>
      )}
    </div>
  );
}
