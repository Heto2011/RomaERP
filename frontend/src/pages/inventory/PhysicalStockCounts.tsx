import { useEffect, useState } from "react";
import { PhysicalStockCountsApi, ItemsApi, WarehousesApi } from "../../api/services";
import type { CountVarianceRow, Item, PhysicalStockCountEntry, Warehouse } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";
import InfoTooltip from "../../components/InfoTooltip";
import { bilingualName } from "../../i18n/bilingual";

const TEXT = {
  ar: { branch: "الفرع", anyBranch: "بدون فرع (إجمالي الشركة)", apply: "اعتمد الفرق (تسوية)", applied: "اتسوّى", applyConfirm: "تعمل تسوية مخزون بالفرق ده؟ هيتعمل قيد محاسبي.",
    report: "فروقات الجرد حسب الفرع والصنف", reportIntro: "مجموع النقص والزيادة وقيمتهم. الأكبر خسارة فوق.", short: "نقص", over: "زيادة", net: "الصافي", netValue: "قيمة الصافي", times: "عدد الجرد", noReport: "مفيش فروقات مسجلة بعد." },
  en: { branch: "Branch", anyBranch: "No branch (company total)", apply: "Apply difference (adjustment)", applied: "Adjusted", applyConfirm: "Make a stock adjustment for this difference? A journal entry will be posted.",
    report: "Count differences by branch and item", reportIntro: "Total shortage and surplus with their value. Biggest losses first.", short: "Short", over: "Over", net: "Net", netValue: "Net value", times: "Counts", noReport: "No differences recorded yet." },
};

export default function PhysicalStockCountsPage() {
  const { t, lang } = useLanguage();
  const x = TEXT[lang === "ar" ? "ar" : "en"];
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [warehouseId, setWarehouseId] = useState("");
  const [report, setReport] = useState<CountVarianceRow[]>([]);
  const [counts, setCounts] = useState<PhysicalStockCountEntry[]>([]);
  const [items, setItems] = useState<Item[]>([]);
  const [showForm, setShowForm] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [countDate, setCountDate] = useState(new Date().toISOString().slice(0, 10));
  const [itemId, setItemId] = useState("");
  const [countedQuantity, setCountedQuantity] = useState("");
  const [notes, setNotes] = useState("");

  async function load() {
    const [countsRes, itemsRes, whRes, repRes] = await Promise.all([PhysicalStockCountsApi.getAll(), ItemsApi.getAll(), WarehousesApi.getAll(), PhysicalStockCountsApi.varianceReport({})]);
    setCounts(countsRes.data);
    setItems(itemsRes.data);
    setWarehouses(whRes.data.filter((w) => w.isActive));
    setReport(repRes.data);
  }

  useEffect(() => {
    load();
  }, []);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    try {
      await PhysicalStockCountsApi.create({
        itemId,
        countDate,
        countedQuantity: Number(countedQuantity) || 0,
        notes: notes || null,
        warehouseId: warehouseId || null,
      });
      setShowForm(false);
      setItemId("");
      setCountedQuantity("");
      setNotes("");
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  async function handleDelete(id: string) {
    if (!window.confirm(t.inventory.confirmDeleteCount)) return;
    setError(null);
    try {
      await PhysicalStockCountsApi.remove(id);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  async function handleApply(id: string) {
    if (!window.confirm(x.applyConfirm)) return;
    setError(null);
    try {
      await PhysicalStockCountsApi.apply(id);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  const selectedItem = items.find((i) => i.id === itemId);

  return (
    <div>
      <div className="page-header">
        <h1>{t.inventory.physicalStockCountsTitle}<InfoTooltip text={t.inventory.physicalStockCountsIntro} /></h1>
        <button className="btn" onClick={() => setShowForm((v) => !v)}>
          {showForm ? t.common.cancel : t.inventory.newCount}
        </button>
      </div>
      <p className="text-muted">{t.inventory.physicalStockCountsIntro}</p>

      {error && <div className="alert-error">{error}</div>}

      {showForm && (
        <div className="card">
          <form onSubmit={handleSubmit}>
            <div className="form-grid">
              <div className="form-field">
                <label>{t.common.date}</label>
                <input type="date" value={countDate} onChange={(e) => setCountDate(e.target.value)} required />
              </div>
              <div className="form-field">
                <label>{x.branch}</label>
                <select value={warehouseId} onChange={(e) => setWarehouseId(e.target.value)}>
                  <option value="">{x.anyBranch}</option>
                  {warehouses.map((w) => <option key={w.id} value={w.id}>{lang === "ar" ? w.nameAr : w.nameEn || w.nameAr}</option>)}
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
                <label>{t.inventory.countedQuantity} {selectedItem ? `(${t.inventory.systemQuantity}: ${selectedItem.quantityOnHand})` : ""}</label>
                <input type="number" step="0.01" value={countedQuantity} onChange={(e) => setCountedQuantity(e.target.value)} required />
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
              <th>{t.inventory.countDate}</th>
              <th>{x.branch}</th>
              <th>{t.sales.item}</th>
              <th>{t.inventory.systemQuantity}</th>
              <th>{t.inventory.countedQuantity}</th>
              <th>{t.inventory.variance}</th>
              <th>{t.inventory.varianceValue}</th>
              <th>{t.common.actions}</th>
            </tr>
          </thead>
          <tbody>
            {counts.map((c) => (
              <tr key={c.id}>
                <td>{new Date(c.countDate).toLocaleDateString()}</td>
                <td>{c.warehouseName ?? "—"}</td>
                <td>{c.itemCode} - {c.itemName}</td>
                <td>{c.systemQuantity.toLocaleString()}</td>
                <td>{c.countedQuantity.toLocaleString()}</td>
                <td className={c.variance === 0 ? "" : c.variance > 0 ? "text-success" : "text-danger"}>{c.variance.toLocaleString()}</td>
                <td className={c.varianceValue === 0 ? "" : c.varianceValue > 0 ? "text-success" : "text-danger"}>{c.varianceValue.toLocaleString()}</td>
                <td>
                  {c.stockAdjustmentId ? <span className="badge">{x.applied}</span>
                    : c.warehouseId && c.variance !== 0 && <button className="btn btn-sm" onClick={() => handleApply(c.id)}>{x.apply}</button>}{" "}
                  <button className="btn btn-danger btn-sm" onClick={() => handleDelete(c.id)}>
                    {t.common.delete}
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <h2 style={{ marginTop: 24 }}>{x.report}</h2>
      <p className="text-muted">{x.reportIntro}</p>
      <div className="card" style={{ overflowX: "auto" }}>
        {report.length === 0 ? <span className="text-muted">{x.noReport}</span> : (
          <table>
            <thead><tr><th>{x.branch}</th><th>{t.sales.item}</th><th>{x.times}</th><th>{x.short}</th><th>{x.over}</th><th>{x.net}</th><th>{x.netValue}</th></tr></thead>
            <tbody>
              {report.map((r) => (
                <tr key={`${r.warehouseId}|${r.itemId}`}>
                  <td>{r.warehouseName || "—"}</td><td>{r.itemCode} - {r.itemName}</td><td>{r.counts}</td>
                  <td>{r.shortQuantity.toLocaleString()}</td><td>{r.overQuantity.toLocaleString()}</td>
                  <td>{r.netQuantity.toLocaleString()}</td>
                  <td className={r.netValue < 0 ? "text-danger" : r.netValue > 0 ? "text-success" : ""}>{r.netValue.toLocaleString()}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}
