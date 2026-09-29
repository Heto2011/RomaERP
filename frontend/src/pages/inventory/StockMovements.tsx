import { useEffect, useState } from "react";
import { InventoryApi, ItemsApi, LookupsApi, WarehousesApi } from "../../api/services";
import { StockMovementType, type CostCenterLookup, type FiscalPeriod, type Item, type StockMovement, type Warehouse } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";
import { bilingualName } from "../../i18n/bilingual";

export default function StockMovements() {
  const { t, lang } = useLanguage();
  const typeLabel: Record<StockMovementType, { text: string; cls: string }> = {
    [StockMovementType.Receipt]: { text: t.inventory.receipt, cls: "badge-posted" },
    [StockMovementType.Issue]: { text: t.inventory.issue, cls: "badge-draft" },
  };
  const [movements, setMovements] = useState<StockMovement[]>([]);
  const [items, setItems] = useState<Item[]>([]);
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [periods, setPeriods] = useState<FiscalPeriod[]>([]);
  const [costCenters, setCostCenters] = useState<CostCenterLookup[]>([]);
  const [mode, setMode] = useState<"none" | "receive" | "issue">("none");
  const [error, setError] = useState<string | null>(null);

  const [movementDate, setMovementDate] = useState(new Date().toISOString().slice(0, 10));
  const [fiscalPeriodId, setFiscalPeriodId] = useState("");
  const [itemId, setItemId] = useState("");
  const [warehouseId, setWarehouseId] = useState("");
  const [costCenterId, setCostCenterId] = useState("");
  const [quantity, setQuantity] = useState("");
  const [unitCost, setUnitCost] = useState("");
  const [reference, setReference] = useState("");

  async function load() {
    const [movementsRes, itemsRes, warehousesRes, periodsRes, costCentersRes] = await Promise.all([
      InventoryApi.getMovements(),
      ItemsApi.getAll(),
      WarehousesApi.getAll(),
      LookupsApi.fiscalPeriods(),
      LookupsApi.costCenters(),
    ]);
    setMovements(movementsRes.data);
    setItems(itemsRes.data);
    setWarehouses(warehousesRes.data);
    setPeriods(periodsRes.data);
    setCostCenters(costCentersRes.data);
  }

  useEffect(() => {
    load();
  }, []);

  function resetForm() {
    setItemId("");
    setWarehouseId("");
    setCostCenterId("");
    setQuantity("");
    setUnitCost("");
    setReference("");
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    try {
      if (mode === "receive") {
        await InventoryApi.receive({
          movementDate,
          fiscalPeriodId,
          itemId,
          warehouseId,
          quantity: Number(quantity) || 0,
          unitCost: Number(unitCost) || 0,
          reference: reference || null,
        });
      } else if (mode === "issue") {
        await InventoryApi.issue({
          movementDate,
          fiscalPeriodId,
          itemId,
          warehouseId,
          costCenterId: costCenterId || null,
          quantity: Number(quantity) || 0,
          reference: reference || null,
        });
      }
      setMode("none");
      resetForm();
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  const selectedItem = items.find((i) => i.id === itemId);

  return (
    <div>
      <div className="page-header">
        <h1>{t.inventory.stockMovementsTitle}</h1>
        <div style={{ display: "flex", gap: 10 }}>
          <button className="btn btn-secondary" onClick={() => setMode(mode === "receive" ? "none" : "receive")}>
            {mode === "receive" ? t.common.cancel : t.inventory.newReceipt}
          </button>
          <button className="btn" onClick={() => setMode(mode === "issue" ? "none" : "issue")}>
            {mode === "issue" ? t.common.cancel : t.inventory.newIssue}
          </button>
        </div>
      </div>

      {error && <div className="alert-error">{error}</div>}

      {mode !== "none" && (
        <div className="card">
          <h3 style={{ marginTop: 0 }}>{mode === "receive" ? t.inventory.receiptTitle : t.inventory.issueTitle}</h3>
          <form onSubmit={handleSubmit}>
            <div className="form-grid">
              <div className="form-field">
                <label>{t.common.date}</label>
                <input type="date" value={movementDate} onChange={(e) => setMovementDate(e.target.value)} required />
              </div>
              <div className="form-field">
                <label>{t.accounting.fiscalPeriod}</label>
                <select value={fiscalPeriodId} onChange={(e) => setFiscalPeriodId(e.target.value)} required>
                  <option value="">{t.accounting.selectPeriod}</option>
                  {periods.map((p) => (
                    <option key={p.id} value={p.id} disabled={p.isClosed}>
                      {p.name}
                    </option>
                  ))}
                </select>
              </div>
              <div className="form-field">
                <label>{t.sales.item}</label>
                <select value={itemId} onChange={(e) => setItemId(e.target.value)} required>
                  <option value="">{t.inventory.selectItem}</option>
                  {items.map((i) => (
                    <option key={i.id} value={i.id}>
                      {i.code} - {bilingualName(i.nameAr, i.nameEn, lang)}
                    </option>
                  ))}
                </select>
              </div>
              <div className="form-field">
                <label>{t.sales.warehouse}</label>
                <select value={warehouseId} onChange={(e) => setWarehouseId(e.target.value)} required>
                  <option value="">{t.inventory.selectWarehouse}</option>
                  {warehouses.map((w) => (
                    <option key={w.id} value={w.id}>
                      {w.code} - {bilingualName(w.nameAr, w.nameEn, lang)}
                    </option>
                  ))}
                </select>
              </div>
              <div className="form-field">
                <label>{t.common.quantity} {selectedItem ? `(${t.inventory.available}: ${selectedItem.quantityOnHand})` : ""}</label>
                <input type="number" step="0.01" value={quantity} onChange={(e) => setQuantity(e.target.value)} required />
              </div>
              {mode === "receive" && (
                <div className="form-field">
                  <label>{t.inventory.unitCost}</label>
                  <input type="number" step="0.01" value={unitCost} onChange={(e) => setUnitCost(e.target.value)} required />
                </div>
              )}
              {mode === "issue" && (
                <div className="form-field">
                  <label>{t.inventory.costCenterOptional}</label>
                  <select value={costCenterId} onChange={(e) => setCostCenterId(e.target.value)}>
                    <option value="">{t.common.none}</option>
                    {costCenters.map((c) => (
                      <option key={c.id} value={c.id}>
                        {c.code} - {c.nameAr}
                      </option>
                    ))}
                  </select>
                </div>
              )}
              <div className="form-field">
                <label>{t.inventory.referenceOptional}</label>
                <input value={reference} onChange={(e) => setReference(e.target.value)} />
              </div>
            </div>
            <button className="btn" type="submit" style={{ marginTop: 14 }}>
              {t.common.save}
            </button>
          </form>
        </div>
      )}

      <div className="card">
        <table>
          <thead>
            <tr>
              <th>{t.inventory.movementNumber}</th>
              <th>{t.common.date}</th>
              <th>{t.common.type}</th>
              <th>{t.sales.item}</th>
              <th>{t.sales.warehouse}</th>
              <th>{t.common.quantity}</th>
              <th>{t.inventory.unitCost}</th>
              <th>{t.common.total}</th>
            </tr>
          </thead>
          <tbody>
            {movements.map((m) => (
              <tr key={m.id}>
                <td>{m.movementNumber}</td>
                <td>{new Date(m.movementDate).toLocaleDateString()}</td>
                <td>
                  <span className={`badge ${typeLabel[m.movementType].cls}`}>{typeLabel[m.movementType].text}</span>
                </td>
                <td>{m.itemCode} - {m.itemName}</td>
                <td>{m.warehouseName}</td>
                <td>{m.quantity.toLocaleString()}</td>
                <td>{m.unitCost.toLocaleString()}</td>
                <td>{m.totalCost.toLocaleString()}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
