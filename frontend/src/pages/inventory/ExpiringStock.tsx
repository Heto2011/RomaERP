import { useEffect, useState } from "react";
import { InventoryApi } from "../../api/services";
import type { ExpiringLot } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";

export default function ExpiringStock() {
  const { t } = useLanguage();
  const [withinDays, setWithinDays] = useState(7);
  const [lots, setLots] = useState<ExpiringLot[]>([]);
  const [error, setError] = useState<string | null>(null);

  async function load(days: number) {
    setError(null);
    try {
      const res = await InventoryApi.getExpiringLots(days);
      setLots(res.data);
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  useEffect(() => {
    load(withinDays);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const totalValueAtRisk = lots.reduce((sum, l) => sum + l.valueAtRisk, 0);

  return (
    <div>
      <div className="page-header">
        <h1>{t.inventory.expiringStockTitle}</h1>
      </div>
      <p className="text-muted">{t.inventory.expiringStockIntro}</p>

      {error && <div className="alert-error">{error}</div>}

      <div className="card">
        <div style={{ display: "flex", alignItems: "flex-end", gap: 10 }}>
          <div className="form-field" style={{ maxWidth: 160 }}>
            <label>{t.inventory.expiringWithinDays}</label>
            <input type="number" min={1} value={withinDays} onChange={(e) => setWithinDays(Number(e.target.value))} />
          </div>
          <button className="btn btn-secondary" onClick={() => load(withinDays)}>{t.common.refresh}</button>
        </div>

        <table style={{ marginTop: 16 }}>
          <thead>
            <tr>
              <th>{t.common.code}</th>
              <th>{t.common.nameAr}</th>
              <th>{t.inventory.warehouse}</th>
              <th>{t.inventory.lotNumber}</th>
              <th>{t.common.quantity}</th>
              <th>{t.inventory.expiryDate}</th>
              <th>{t.common.status}</th>
              <th>{t.inventory.valueAtRisk}</th>
            </tr>
          </thead>
          <tbody>
            {lots.length === 0 && (
              <tr>
                <td colSpan={8} className="text-muted" style={{ textAlign: "center", padding: 20 }}>
                  {t.common.noData}
                </td>
              </tr>
            )}
            {lots.map((l) => (
              <tr key={`${l.itemId}-${l.lotNumber}`}>
                <td>{l.itemCode}</td>
                <td>{l.itemName}</td>
                <td>{l.warehouseName}</td>
                <td>{l.lotNumber}</td>
                <td>{l.quantityOnHand.toLocaleString()}</td>
                <td>{new Date(l.expiryDate).toLocaleDateString()}</td>
                <td>
                  {l.isExpired ? (
                    <span className="text-danger">{t.inventory.expiredBadge}</span>
                  ) : (
                    <span className={l.daysUntilExpiry <= 2 ? "text-danger" : "text-muted"}>
                      {l.daysUntilExpiry} {t.inventory.daysLeft}
                    </span>
                  )}
                </td>
                <td>{l.valueAtRisk.toLocaleString()}</td>
              </tr>
            ))}
          </tbody>
          {lots.length > 0 && (
            <tfoot>
              <tr style={{ fontWeight: 700 }}>
                <td colSpan={7} style={{ textAlign: "end" }}>{t.common.total}</td>
                <td>{totalValueAtRisk.toLocaleString()}</td>
              </tr>
            </tfoot>
          )}
        </table>
      </div>
    </div>
  );
}
