import { useEffect, useState } from "react";
import { WasteEntriesApi, ItemsApi, WarehousesApi, LookupsApi } from "../../api/services";
import { WasteReason, type FiscalPeriod, type Item, type Warehouse, type WasteEntryRecord } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";
import InfoTooltip from "../../components/InfoTooltip";
import { bilingualName } from "../../i18n/bilingual";

export default function WasteEntriesPage() {
  const { t, lang } = useLanguage();
  const reasonLabel: Record<WasteReason, string> = {
    [WasteReason.Waste]: t.inventory.wasteReasonWaste,
    [WasteReason.Expired]: t.inventory.wasteReasonExpired,
    [WasteReason.Damaged]: t.inventory.wasteReasonDamaged,
    [WasteReason.ProductionWaste]: t.inventory.wasteReasonProductionWaste,
    [WasteReason.OverPortion]: t.inventory.wasteReasonOverPortion,
    [WasteReason.Unknown]: t.inventory.wasteReasonUnknown,
  };

  const [entries, setEntries] = useState<WasteEntryRecord[]>([]);
  const [items, setItems] = useState<Item[]>([]);
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [periods, setPeriods] = useState<FiscalPeriod[]>([]);
  const [showForm, setShowForm] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [wasteDate, setWasteDate] = useState(new Date().toISOString().slice(0, 10));
  const [fiscalPeriodId, setFiscalPeriodId] = useState("");
  const [itemId, setItemId] = useState("");
  const [warehouseId, setWarehouseId] = useState("");
  const [quantity, setQuantity] = useState("");
  const [reason, setReason] = useState<WasteReason>(WasteReason.Waste);
  const [notes, setNotes] = useState("");

  async function load() {
    const [entriesRes, itemsRes, warehousesRes, periodsRes] = await Promise.all([
      WasteEntriesApi.getAll(),
      ItemsApi.getAll(),
      WarehousesApi.getAll(),
      LookupsApi.fiscalPeriods(),
    ]);
    setEntries(entriesRes.data);
    setItems(itemsRes.data);
    setWarehouses(warehousesRes.data);
    setPeriods(periodsRes.data);
  }

  useEffect(() => {
    load();
  }, []);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    try {
      await WasteEntriesApi.create({
        itemId,
        warehouseId,
        fiscalPeriodId,
        wasteDate,
        quantity: Number(quantity) || 0,
        reason,
        notes: notes || null,
      });
      setShowForm(false);
      setItemId("");
      setWarehouseId("");
      setQuantity("");
      setNotes("");
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  const selectedItem = items.find((i) => i.id === itemId);

  return (
    <div>
      <div className="page-header">
        <h1>{t.inventory.wasteEntriesTitle}<InfoTooltip text={t.inventory.wasteEntriesIntro} /></h1>
        <button className="btn" onClick={() => setShowForm((v) => !v)}>
          {showForm ? t.common.cancel : t.inventory.newWasteEntry}
        </button>
      </div>
      <p className="text-muted">{t.inventory.wasteEntriesIntro}</p>

      {error && <div className="alert-error">{error}</div>}

      {showForm && (
        <div className="card">
          <form onSubmit={handleSubmit}>
            <div className="form-grid">
              <div className="form-field">
                <label>{t.common.date}</label>
                <input type="date" value={wasteDate} onChange={(e) => setWasteDate(e.target.value)} required />
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
              <div className="form-field">
                <label>{t.inventory.wasteReason}</label>
                <select value={reason} onChange={(e) => setReason(Number(e.target.value) as WasteReason)}>
                  {Object.entries(reasonLabel).map(([value, label]) => (
                    <option key={value} value={value}>{label}</option>
                  ))}
                </select>
              </div>
              <div className="form-field" style={{ gridColumn: "1 / -1" }}>
                <label>{t.inventory.notesOptional}</label>
                <input value={notes} onChange={(e) => setNotes(e.target.value)} />
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
              <th>{t.inventory.wasteDate}</th>
              <th>{t.sales.item}</th>
              <th>{t.common.quantity}</th>
              <th>{t.inventory.unitCost}</th>
              <th>{t.common.total}</th>
              <th>{t.inventory.wasteReason}</th>
            </tr>
          </thead>
          <tbody>
            {entries.map((w) => (
              <tr key={w.id}>
                <td>{new Date(w.wasteDate).toLocaleDateString()}</td>
                <td>{w.itemCode} - {w.itemName}</td>
                <td>{w.quantity.toLocaleString()}</td>
                <td>{w.unitCost.toLocaleString()}</td>
                <td className="text-danger">{w.totalCost.toLocaleString()}</td>
                <td>{reasonLabel[w.reason]}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
