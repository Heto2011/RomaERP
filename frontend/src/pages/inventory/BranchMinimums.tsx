import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { ItemsApi, StockTransfersApi, WarehousesApi } from "../../api/services";
import type { Item, Warehouse } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";

const TEXT = {
  ar: { title: "الحد الأدنى للمخزون لكل فرع", intro: "حدد لكل فرع أقل كمية المفروض تفضل عنده من كل صنف. لما الفرع ينزل عنها بيظهر تنبيه، ومعاه اقتراح بالتحويل من فرع عنده زيادة. التنبيه بس، مفيش حاجة بتتنفذ لوحدها. الصنف من غير حد أدنى مفيهوش تنبيه.",
    alerts: "فروع تحت الحد الأدنى", none: "مفيش فرع تحت الحد الأدنى.", branch: "الفرع", item: "الصنف", have: "الموجود", min: "الحد الأدنى", coming: "في الطريق", short: "الناقص",
    cover: "التغطية", fromBranch: "حوّل من", buy: "اشتري", transferPage: "افتح صفحة التحويل", setTitle: "تحديد حد أدنى", choose: "اختار", save: "حفظ", saved: "اتحفظ.", remove: "إزالة",
    list: "الحدود الحالية", empty: "مفيش حدود دنيا محددة لسه.", hint: "0 = إزالة الحد الأدنى" },
  en: { title: "Minimum stock per branch", intro: "Set, for each branch, the least it should keep of an item. When the branch falls below it you get an alert with a suggested transfer from a branch holding more. Alert only; nothing happens by itself. An item with no minimum raises no alert.",
    alerts: "Branches below their minimum", none: "No branch is below its minimum.", branch: "Branch", item: "Item", have: "On hand", min: "Minimum", coming: "On its way", short: "Short by",
    cover: "Cover", fromBranch: "Transfer from", buy: "Buy", transferPage: "Open transfers", setTitle: "Set a minimum", choose: "Choose", save: "Save", saved: "Saved.", remove: "Remove",
    list: "Current minimums", empty: "No minimums set yet.", hint: "0 = remove the minimum" },
};
type Alert = { warehouseId: string; warehouseName: string; itemId: string; itemCode: string; itemName: string; unit: string; minimumQuantity: number; quantity: number; incoming: number; shortage: number; suggestions: { fromWarehouseId: string; fromWarehouseName: string; quantity: number }[]; toBuy: number };
type Min = { warehouseId: string; warehouseName: string; itemId: string; itemCode: string; itemName: string; unit: string; minimumQuantity: number; quantity: number };
const q = (n: number) => n.toLocaleString(undefined, { maximumFractionDigits: 2 });

export default function BranchMinimums() {
  const { lang } = useLanguage();
  const x = TEXT[lang === "ar" ? "ar" : "en"];
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [items, setItems] = useState<Item[]>([]);
  const [alerts, setAlerts] = useState<Alert[]>([]);
  const [mins, setMins] = useState<Min[]>([]);
  const [warehouseId, setWarehouseId] = useState("");
  const [itemId, setItemId] = useState("");
  const [value, setValue] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  async function load() {
    try {
      const [a, m] = await Promise.all([StockTransfersApi.shortageAlerts(), StockTransfersApi.branchMinimums()]);
      setAlerts(a.data as Alert[]); setMins(m.data as Min[]);
    } catch (e) { setError(getErrorMessage(e)); }
  }
  useEffect(() => {
    Promise.all([WarehousesApi.getAll(), ItemsApi.getAll()]).then(([w, i]) => { setWarehouses(w.data.filter((a) => a.isActive)); setItems(i.data); }).catch((e) => setError(getErrorMessage(e)));
    load();
  }, []);

  async function save(e: React.FormEvent, remove = false) {
    e.preventDefault(); setError(null); setMessage(null);
    try {
      await StockTransfersApi.setBranchMinimum({ warehouseId, itemId, minimumQuantity: remove ? 0 : Number(value) || 0 });
      setMessage(x.saved); setValue(""); await load();
    } catch (err) { setError(getErrorMessage(err)); }
  }
  const wName = (w: Warehouse) => (lang === "ar" ? w.nameAr : w.nameEn || w.nameAr);

  return (
    <div>
      <div className="page-header"><h1>{x.title}</h1></div>
      <p className="text-muted">{x.intro}</p>
      {error && <div className="alert-error">{error}</div>}
      {message && <div className="card" style={{ marginBottom: 12 }}>{message}</div>}

      <h3>{x.alerts}</h3>
      {alerts.length === 0 ? <div className="card text-muted">{x.none}</div> : (
        <div className="card" style={{ overflowX: "auto", borderColor: "#c8862e" }}>
          <table>
            <thead><tr><th>{x.branch}</th><th>{x.item}</th><th>{x.have}</th><th>{x.min}</th><th>{x.coming}</th><th>{x.short}</th><th>{x.cover}</th></tr></thead>
            <tbody>
              {alerts.map((a) => (
                <tr key={`${a.warehouseId}|${a.itemId}`}>
                  <td>{a.warehouseName}</td><td>{a.itemCode} - {a.itemName}</td><td className="text-danger">{q(a.quantity)} {a.unit}</td><td>{q(a.minimumQuantity)}</td>
                  <td>{a.incoming ? q(a.incoming) : "—"}</td><td><strong>{q(a.shortage)}</strong></td>
                  <td>
                    {a.suggestions.map((s) => <div key={s.fromWarehouseId}>{x.fromBranch} {s.fromWarehouseName}: <strong>{q(s.quantity)}</strong></div>)}
                    {a.toBuy > 0 && <div className="text-danger">{x.buy}: <strong>{q(a.toBuy)}</strong></div>}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          <p style={{ marginTop: 8 }}><Link to="/inventory/transfers">{x.transferPage}</Link></p>
        </div>
      )}

      <h3 style={{ marginTop: 24 }}>{x.setTitle}</h3>
      <form className="card" onSubmit={(e) => save(e)}>
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", gap: 12 }}>
          <div className="form-field"><label>{x.branch}</label>
            <select value={warehouseId} onChange={(e) => setWarehouseId(e.target.value)} required><option value="">{x.choose}</option>{warehouses.map((w) => <option key={w.id} value={w.id}>{wName(w)}</option>)}</select></div>
          <div className="form-field"><label>{x.item}</label>
            <select value={itemId} onChange={(e) => setItemId(e.target.value)} required><option value="">{x.choose}</option>{items.map((i) => <option key={i.id} value={i.id}>{i.code} — {lang === "ar" ? i.nameAr : i.nameEn || i.nameAr}</option>)}</select></div>
          <div className="form-field"><label>{x.min}</label><input type="number" min="0" step="any" value={value} onChange={(e) => setValue(e.target.value)} placeholder={x.hint} required /></div>
        </div>
        <button className="btn" type="submit">{x.save}</button>
      </form>

      <h3 style={{ marginTop: 24 }}>{x.list}</h3>
      {mins.length === 0 ? <div className="card text-muted">{x.empty}</div> : (
        <div className="card" style={{ overflowX: "auto" }}>
          <table>
            <thead><tr><th>{x.branch}</th><th>{x.item}</th><th>{x.have}</th><th>{x.min}</th><th></th></tr></thead>
            <tbody>
              {mins.map((m) => (
                <tr key={`${m.warehouseId}|${m.itemId}`}>
                  <td>{m.warehouseName}</td><td>{m.itemCode} - {m.itemName}</td><td className={m.quantity < m.minimumQuantity ? "text-danger" : ""}>{q(m.quantity)} {m.unit}</td><td>{q(m.minimumQuantity)}</td>
                  <td><button className="btn btn-secondary btn-sm" onClick={(e) => { setWarehouseId(m.warehouseId); setItemId(m.itemId); save(e, true); }}>{x.remove}</button></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
