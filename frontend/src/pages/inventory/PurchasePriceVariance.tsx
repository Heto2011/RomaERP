import { useEffect, useState } from "react";
import { InventoryReportsApi } from "../../api/services";
import type { PurchasePriceVarianceReport } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";
import InfoTooltip from "../../components/InfoTooltip";

function firstDayOfMonth() {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-01`;
}

export default function PurchasePriceVariancePage() {
  const { t } = useLanguage();
  const [fromDate, setFromDate] = useState(firstDayOfMonth());
  const [toDate, setToDate] = useState(new Date().toISOString().slice(0, 10));
  const [report, setReport] = useState<PurchasePriceVarianceReport | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function load() {
    setError(null);
    try {
      const res = await InventoryReportsApi.purchasePriceVariance(fromDate, toDate);
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
        <h1>{t.inventory.purchasePriceVarianceTitle}<InfoTooltip text={t.inventory.purchasePriceVarianceIntro} /></h1>
      </div>
      <p className="text-muted">{t.inventory.purchasePriceVarianceIntro}</p>

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

      {report && report.items.length === 0 && <div className="card text-muted">{t.common.noData}</div>}

      {report && report.items.length > 0 && (
        <div className="card">
          <table>
            <thead>
              <tr>
                <th>{t.inventory.itemCode}</th>
                <th>{t.common.description}</th>
                <th>{t.inventory.previousPrice}</th>
                <th>{t.inventory.latestPrice}</th>
                <th>{t.inventory.changeAmount}</th>
                <th>{t.inventory.changePercent}</th>
              </tr>
            </thead>
            <tbody>
              {report.items.map((l) => (
                <tr key={l.itemId}>
                  <td>{l.itemCode}</td>
                  <td>{l.itemName}</td>
                  <td>{l.previousUnitCost.toLocaleString()}</td>
                  <td>{l.latestUnitCost.toLocaleString()}</td>
                  <td className={l.changeAmount > 0 ? "text-danger" : l.changeAmount < 0 ? "text-success" : ""}>
                    {l.changeAmount.toLocaleString()}
                  </td>
                  <td className={l.changePercent > 0 ? "text-danger" : l.changePercent < 0 ? "text-success" : ""}>
                    {l.changePercent.toFixed(1)}%
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
