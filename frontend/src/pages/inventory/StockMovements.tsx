import { useEffect, useState } from "react";
import { InventoryApi, ItemsApi, LookupsApi, WarehousesApi } from "../../api/services";
import { StockMovementType, type CostCenterLookup, type FiscalPeriod, type Item, type StockMovement, type Warehouse } from "../../api/types";
import { getErrorMessage } from "../../api/client";

const typeLabel: Record<StockMovementType, { text: string; cls: string }> = {
  [StockMovementType.Receipt]: { text: "استلام", cls: "badge-posted" },
  [StockMovementType.Issue]: { text: "صرف", cls: "badge-draft" },
};

export default function StockMovements() {
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
        <h1>حركات المخزون</h1>
        <div style={{ display: "flex", gap: 10 }}>
          <button className="btn btn-secondary" onClick={() => setMode(mode === "receive" ? "none" : "receive")}>
            {mode === "receive" ? "إلغاء" : "+ استلام مخزون"}
          </button>
          <button className="btn" onClick={() => setMode(mode === "issue" ? "none" : "issue")}>
            {mode === "issue" ? "إلغاء" : "+ صرف مخزون"}
          </button>
        </div>
      </div>

      {error && <div className="alert-error">{error}</div>}

      {mode !== "none" && (
        <div className="card">
          <h3 style={{ marginTop: 0 }}>{mode === "receive" ? "استلام مخزون" : "صرف مخزون"}</h3>
          <form onSubmit={handleSubmit}>
            <div className="form-grid">
              <div className="form-field">
                <label>التاريخ</label>
                <input type="date" value={movementDate} onChange={(e) => setMovementDate(e.target.value)} required />
              </div>
              <div className="form-field">
                <label>الفترة المالية</label>
                <select value={fiscalPeriodId} onChange={(e) => setFiscalPeriodId(e.target.value)} required>
                  <option value="">اختر الفترة</option>
                  {periods.map((p) => (
                    <option key={p.id} value={p.id} disabled={p.isClosed}>
                      {p.name}
                    </option>
                  ))}
                </select>
              </div>
              <div className="form-field">
                <label>الصنف</label>
                <select value={itemId} onChange={(e) => setItemId(e.target.value)} required>
                  <option value="">اختر الصنف</option>
                  {items.map((i) => (
                    <option key={i.id} value={i.id}>
                      {i.code} - {i.nameAr}
                    </option>
                  ))}
                </select>
              </div>
              <div className="form-field">
                <label>المخزن</label>
                <select value={warehouseId} onChange={(e) => setWarehouseId(e.target.value)} required>
                  <option value="">اختر المخزن</option>
                  {warehouses.map((w) => (
                    <option key={w.id} value={w.id}>
                      {w.code} - {w.nameAr}
                    </option>
                  ))}
                </select>
              </div>
              <div className="form-field">
                <label>الكمية {selectedItem ? `(المتاح: ${selectedItem.quantityOnHand})` : ""}</label>
                <input type="number" step="0.01" value={quantity} onChange={(e) => setQuantity(e.target.value)} required />
              </div>
              {mode === "receive" && (
                <div className="form-field">
                  <label>تكلفة الوحدة</label>
                  <input type="number" step="0.01" value={unitCost} onChange={(e) => setUnitCost(e.target.value)} required />
                </div>
              )}
              {mode === "issue" && (
                <div className="form-field">
                  <label>مركز التكلفة (اختياري)</label>
                  <select value={costCenterId} onChange={(e) => setCostCenterId(e.target.value)}>
                    <option value="">بدون</option>
                    {costCenters.map((c) => (
                      <option key={c.id} value={c.id}>
                        {c.code} - {c.nameAr}
                      </option>
                    ))}
                  </select>
                </div>
              )}
              <div className="form-field">
                <label>مرجع (اختياري)</label>
                <input value={reference} onChange={(e) => setReference(e.target.value)} />
              </div>
            </div>
            <button className="btn" type="submit" style={{ marginTop: 14 }}>
              حفظ
            </button>
          </form>
        </div>
      )}

      <div className="card">
        <table>
          <thead>
            <tr>
              <th>رقم الحركة</th>
              <th>التاريخ</th>
              <th>النوع</th>
              <th>الصنف</th>
              <th>المخزن</th>
              <th>الكمية</th>
              <th>تكلفة الوحدة</th>
              <th>الإجمالي</th>
            </tr>
          </thead>
          <tbody>
            {movements.map((m) => (
              <tr key={m.id}>
                <td>{m.movementNumber}</td>
                <td>{new Date(m.movementDate).toLocaleDateString("ar-EG")}</td>
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
