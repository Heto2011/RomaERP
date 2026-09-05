import { useEffect, useState } from "react";
import { ItemsApi, ManufacturingApi, WarehousesApi } from "../../api/services";
import type { Item, ManufacturingBom, ManufacturingOrder, Warehouse } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";
import { bilingualName } from "../../i18n/bilingual";

interface BomLineDraft {
  rawMaterialItemId: string;
  quantityPerBatch: number;
}

export default function Manufacturing() {
  const { t, lang } = useLanguage();
  const [items, setItems] = useState<Item[]>([]);
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [boms, setBoms] = useState<ManufacturingBom[]>([]);
  const [orders, setOrders] = useState<ManufacturingOrder[]>([]);
  const [error, setError] = useState<string | null>(null);

  const [showBomForm, setShowBomForm] = useState(false);
  const [editingOutputItemId, setEditingOutputItemId] = useState<string | null>(null);
  const [newOutputItemId, setNewOutputItemId] = useState("");
  const [outputQuantity, setOutputQuantity] = useState(1);
  const [bomLines, setBomLines] = useState<BomLineDraft[]>([]);
  const [newRawMaterialId, setNewRawMaterialId] = useState("");
  const [newQuantity, setNewQuantity] = useState(1);
  const [savingBom, setSavingBom] = useState(false);

  const [showOrderForm, setShowOrderForm] = useState(false);
  const [orderBomOutputItemId, setOrderBomOutputItemId] = useState("");
  const [orderWarehouseId, setOrderWarehouseId] = useState("");
  const [orderDate, setOrderDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [orderProducedQuantity, setOrderProducedQuantity] = useState(1);
  const [orderNotes, setOrderNotes] = useState("");
  const [savingOrder, setSavingOrder] = useState(false);

  async function load() {
    const [itemsRes, warehousesRes, bomsRes, ordersRes] = await Promise.all([
      ItemsApi.getAll(),
      WarehousesApi.getAll(),
      ManufacturingApi.getBoms(),
      ManufacturingApi.getOrders(),
    ]);
    setItems(itemsRes.data);
    setWarehouses(warehousesRes.data);
    setBoms(bomsRes.data);
    setOrders(ordersRes.data);
  }

  useEffect(() => {
    load();
  }, []);

  function itemLabel(id: string) {
    const item = items.find((i) => i.id === id);
    return item ? `${item.code} - ${bilingualName(item.nameAr, item.nameEn, lang)}` : id;
  }

  function openNewBom() {
    setEditingOutputItemId(null);
    setNewOutputItemId("");
    setOutputQuantity(1);
    setBomLines([]);
    setNewRawMaterialId("");
    setNewQuantity(1);
    setError(null);
    setShowBomForm(true);
  }

  function openEditBom(bom: ManufacturingBom) {
    setEditingOutputItemId(bom.outputItemId);
    setNewOutputItemId(bom.outputItemId);
    setOutputQuantity(bom.outputQuantity);
    setBomLines(bom.lines.map((l) => ({ rawMaterialItemId: l.rawMaterialItemId, quantityPerBatch: l.quantityPerBatch })));
    setNewRawMaterialId("");
    setNewQuantity(1);
    setError(null);
    setShowBomForm(true);
  }

  function closeBomForm() {
    setEditingOutputItemId(null);
    setNewOutputItemId("");
    setShowBomForm(false);
  }

  function addBomLine() {
    if (!newRawMaterialId) return;
    if (bomLines.some((l) => l.rawMaterialItemId === newRawMaterialId)) return;
    setBomLines((prev) => [...prev, { rawMaterialItemId: newRawMaterialId, quantityPerBatch: newQuantity }]);
    setNewRawMaterialId("");
    setNewQuantity(1);
  }

  function removeBomLine(rawMaterialItemId: string) {
    setBomLines((prev) => prev.filter((l) => l.rawMaterialItemId !== rawMaterialItemId));
  }

  async function handleSaveBom() {
    const outputItemId = editingOutputItemId ?? newOutputItemId;
    if (!outputItemId) {
      setError(t.inventory.manufacturingSelectOutputItem);
      return;
    }
    setError(null);
    setSavingBom(true);
    try {
      await ManufacturingApi.setBom(outputItemId, { outputQuantity, lines: bomLines });
      closeBomForm();
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setSavingBom(false);
    }
  }

  async function handleDeleteBom(outputItemId: string) {
    setError(null);
    try {
      await ManufacturingApi.removeBom(outputItemId);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  function openOrderForm() {
    setShowOrderForm(true);
    setOrderBomOutputItemId(boms[0]?.outputItemId ?? "");
    setOrderWarehouseId(warehouses[0]?.id ?? "");
    setOrderDate(new Date().toISOString().slice(0, 10));
    setOrderProducedQuantity(boms[0]?.outputQuantity ?? 1);
    setOrderNotes("");
    setError(null);
  }

  async function handleCreateOrder(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    setSavingOrder(true);
    try {
      await ManufacturingApi.createOrder({
        outputItemId: orderBomOutputItemId,
        warehouseId: orderWarehouseId,
        productionDate: orderDate,
        producedQuantity: orderProducedQuantity,
        notes: orderNotes || null,
      });
      setShowOrderForm(false);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setSavingOrder(false);
    }
  }

  return (
    <div>
      <div className="page-header">
        <h1>{t.inventory.manufacturingTitle}</h1>
      </div>
      <p className="text-muted">{t.inventory.manufacturingIntro}</p>

      {error && <div className="alert-error">{error}</div>}

      <div className="card">
        <div className="page-header" style={{ marginBottom: 10 }}>
          <h3 style={{ margin: 0 }}>{t.inventory.manufacturingBomsTitle}</h3>
          <button className="btn" onClick={openNewBom}>{t.inventory.manufacturingNewBom}</button>
        </div>

        <table>
          <thead>
            <tr>
              <th>{t.inventory.manufacturingOutputItem}</th>
              <th>{t.inventory.manufacturingBatchYield}</th>
              <th>{t.inventory.manufacturingIngredients}</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {boms.length === 0 && (
              <tr>
                <td colSpan={4} className="text-muted" style={{ textAlign: "center", padding: 20 }}>
                  {t.common.noData}
                </td>
              </tr>
            )}
            {boms.map((bom) => (
              <tr key={bom.outputItemId}>
                <td>{bom.outputItemCode} - {bom.outputItemName}</td>
                <td>{bom.outputQuantity.toLocaleString()}</td>
                <td className="text-muted">{bom.lines.map((l) => `${l.rawMaterialItemName} (${l.quantityPerBatch})`).join("، ")}</td>
                <td style={{ display: "flex", gap: 6 }}>
                  <button className="btn btn-secondary btn-sm" onClick={() => openEditBom(bom)}>{t.common.edit}</button>
                  <button className="btn btn-secondary btn-sm" onClick={() => handleDeleteBom(bom.outputItemId)}>{t.common.delete}</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>

        {showBomForm && (
          <div className="modal-overlay" onClick={closeBomForm}>
            <div className="card" style={{ maxWidth: 560, margin: "5% auto" }} onClick={(e) => e.stopPropagation()}>
              <h3>{editingOutputItemId ? itemLabel(editingOutputItemId) : t.inventory.manufacturingNewBom}</h3>

              {!editingOutputItemId && (
                <div className="form-field" style={{ marginTop: 10 }}>
                  <label>{t.inventory.manufacturingOutputItem}</label>
                  <select value={newOutputItemId} onChange={(e) => setNewOutputItemId(e.target.value)}>
                    <option value="">-</option>
                    {items.map((i) => (
                      <option key={i.id} value={i.id}>{i.code} - {bilingualName(i.nameAr, i.nameEn, lang)}</option>
                    ))}
                  </select>
                </div>
              )}

              <div className="form-field" style={{ marginTop: 10 }}>
                <label>{t.inventory.manufacturingBatchYield}</label>
                <input type="number" min={0.0001} step="0.01" value={outputQuantity} onChange={(e) => setOutputQuantity(Number(e.target.value))} />
              </div>

              <h4 style={{ marginTop: 16 }}>{t.inventory.manufacturingIngredients}</h4>
              <table>
                <thead>
                  <tr>
                    <th>{t.restaurant.rawMaterial}</th>
                    <th>{t.inventory.manufacturingQuantityPerBatch}</th>
                    <th></th>
                  </tr>
                </thead>
                <tbody>
                  {bomLines.map((line) => (
                    <tr key={line.rawMaterialItemId}>
                      <td>{itemLabel(line.rawMaterialItemId)}</td>
                      <td>{line.quantityPerBatch}</td>
                      <td>
                        <button type="button" className="btn btn-secondary btn-sm" onClick={() => removeBomLine(line.rawMaterialItemId)}>
                          {t.common.delete}
                        </button>
                      </td>
                    </tr>
                  ))}
                  <tr>
                    <td>
                      <select value={newRawMaterialId} onChange={(e) => setNewRawMaterialId(e.target.value)}>
                        <option value="">-</option>
                        {items.filter((i) => i.id !== (editingOutputItemId ?? newOutputItemId)).map((i) => (
                          <option key={i.id} value={i.id}>{i.code} - {bilingualName(i.nameAr, i.nameEn, lang)}</option>
                        ))}
                      </select>
                    </td>
                    <td>
                      <input type="number" min={0.0001} step="0.01" value={newQuantity} onChange={(e) => setNewQuantity(Number(e.target.value))} style={{ width: 90 }} />
                    </td>
                    <td>
                      <button type="button" className="btn btn-secondary btn-sm" onClick={addBomLine} disabled={!newRawMaterialId}>
                        {t.sales.addLine}
                      </button>
                    </td>
                  </tr>
                </tbody>
              </table>

              <div style={{ display: "flex", gap: 10, marginTop: 16 }}>
                <button className="btn" onClick={handleSaveBom} disabled={savingBom}>{t.common.save}</button>
                <button className="btn btn-secondary" onClick={closeBomForm}>{t.common.cancel}</button>
              </div>
            </div>
          </div>
        )}
      </div>

      <div className="card" style={{ marginTop: 16 }}>
        <div className="page-header" style={{ marginBottom: 10 }}>
          <h3 style={{ margin: 0 }}>{t.inventory.manufacturingOrdersTitle}</h3>
          <button className="btn" onClick={openOrderForm} disabled={boms.length === 0}>{t.inventory.manufacturingNewOrder}</button>
        </div>

        {boms.length === 0 && <p className="text-muted">{t.inventory.manufacturingNoOrdersHint}</p>}

        <table>
          <thead>
            <tr>
              <th>{t.common.reference}</th>
              <th>{t.inventory.manufacturingOutputItem}</th>
              <th>{t.common.quantity}</th>
              <th>{t.common.date}</th>
              <th>{t.common.total}</th>
              <th>{t.common.notes}</th>
            </tr>
          </thead>
          <tbody>
            {orders.length === 0 && (
              <tr>
                <td colSpan={6} className="text-muted" style={{ textAlign: "center", padding: 20 }}>
                  {t.common.noData}
                </td>
              </tr>
            )}
            {orders.map((o) => (
              <tr key={o.id}>
                <td>{o.orderNumber}</td>
                <td>{o.outputItemCode} - {o.outputItemName}</td>
                <td>{o.producedQuantity.toLocaleString()}</td>
                <td>{new Date(o.productionDate).toLocaleDateString()}</td>
                <td>{o.totalCost.toLocaleString()}</td>
                <td className="text-muted">{o.notes ?? "—"}</td>
              </tr>
            ))}
          </tbody>
        </table>

        {showOrderForm && (
          <div className="modal-overlay" onClick={() => setShowOrderForm(false)}>
            <div className="card" style={{ maxWidth: 480, margin: "5% auto" }} onClick={(e) => e.stopPropagation()}>
              <h3>{t.inventory.manufacturingNewOrder}</h3>
              <form onSubmit={handleCreateOrder}>
                <div className="form-field" style={{ marginTop: 10 }}>
                  <label>{t.inventory.manufacturingOutputItem}</label>
                  <select value={orderBomOutputItemId} onChange={(e) => setOrderBomOutputItemId(e.target.value)} required>
                    {boms.map((b) => (
                      <option key={b.outputItemId} value={b.outputItemId}>
                        {b.outputItemCode} - {b.outputItemName} ({t.inventory.manufacturingBatchYield}: {b.outputQuantity})
                      </option>
                    ))}
                  </select>
                </div>
                <div className="form-field">
                  <label>{t.inventory.warehouse}</label>
                  <select value={orderWarehouseId} onChange={(e) => setOrderWarehouseId(e.target.value)} required>
                    <option value="" disabled>-</option>
                    {warehouses.map((w) => (
                      <option key={w.id} value={w.id}>{w.code} - {bilingualName(w.nameAr, w.nameEn, lang)}</option>
                    ))}
                  </select>
                </div>
                <div className="form-field">
                  <label>{t.common.date}</label>
                  <input type="date" value={orderDate} onChange={(e) => setOrderDate(e.target.value)} required />
                </div>
                <div className="form-field">
                  <label>{t.inventory.manufacturingProducedQuantity}</label>
                  <input type="number" min={0.0001} step="0.01" value={orderProducedQuantity} onChange={(e) => setOrderProducedQuantity(Number(e.target.value))} required />
                </div>
                <div className="form-field">
                  <label>{t.common.notes}</label>
                  <input value={orderNotes} onChange={(e) => setOrderNotes(e.target.value)} />
                </div>
                <div style={{ display: "flex", gap: 10, marginTop: 16 }}>
                  <button className="btn" type="submit" disabled={savingOrder}>{t.common.save}</button>
                  <button className="btn btn-secondary" type="button" onClick={() => setShowOrderForm(false)}>{t.common.cancel}</button>
                </div>
              </form>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
