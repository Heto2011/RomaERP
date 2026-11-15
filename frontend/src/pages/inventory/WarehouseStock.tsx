import { useEffect, useMemo, useState } from "react";
import { StockTransfersApi } from "../../api/services";
import type { WarehouseStockRow } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";

const TEXT = {
  ar: { title: "رصيد الفروع", intro: "كمية كل صنف في كل فرع (من حركات المخزون). «قادم» يعني بضاعة اتبعتت لفرع وبتستنى استلامه.", item: "الصنف", incoming: "قادم", empty: "مفيش حركات مخزون لسه.", search: "ابحث بالكود أو الاسم" },
  en: { title: "Branch stock", intro: "Quantity of each item in each branch (from stock movements). “Incoming” is stock sent to a branch and waiting to be received.", item: "Item", incoming: "incoming", empty: "No stock movements yet.", search: "Search by code or name" },
};
const q = (n: number) => n.toLocaleString(undefined, { maximumFractionDigits: 4 });

export default function WarehouseStock() {
  const { lang } = useLanguage();
  const x = TEXT[lang === "ar" ? "ar" : "en"];
  const [rows, setRows] = useState<WarehouseStockRow[]>([]);
  const [search, setSearch] = useState("");
  const [error, setError] = useState<string | null>(null);

  useEffect(() => { StockTransfersApi.warehouseStock().then((r) => setRows(r.data)).catch((e) => setError(getErrorMessage(e))); }, []);

  const warehouses = useMemo(() => [...new Map(rows.map((r) => [r.warehouseId, r.warehouseName])).entries()], [rows]);
  const items = useMemo(() => {
    const map = new Map<string, { code: string; name: string; unit: string; cells: Map<string, WarehouseStockRow> }>();
    for (const r of rows) {
      if (!map.has(r.itemId)) map.set(r.itemId, { code: r.itemCode, name: r.itemName, unit: r.unitOfMeasure, cells: new Map() });
      map.get(r.itemId)!.cells.set(r.warehouseId, r);
    }
    const needle = search.trim().toLowerCase();
    return [...map.values()].filter((i) => !needle || i.code.toLowerCase().includes(needle) || i.name.toLowerCase().includes(needle));
  }, [rows, search]);

  return (
    <div>
      <div className="page-header"><h1>{x.title}</h1></div>
      <p className="text-muted">{x.intro}</p>
      {error && <div className="alert-error">{error}</div>}
      <input placeholder={x.search} value={search} onChange={(e) => setSearch(e.target.value)} style={{ maxWidth: 320, marginBottom: 12 }} />
      {items.length === 0 ? <div className="card text-muted">{x.empty}</div> : (
        <div className="card" style={{ overflowX: "auto" }}>
          <table>
            <thead><tr><th>{x.item}</th>{warehouses.map(([id, name]) => <th key={id}>{name}</th>)}</tr></thead>
            <tbody>
              {items.map((i) => (
                <tr key={i.code}>
                  <td><strong>{i.code}</strong> — {i.name} <span className="text-muted">({i.unit})</span></td>
                  {warehouses.map(([id]) => {
                    const c = i.cells.get(id);
                    return (
                      <td key={id} style={c && c.quantity < 0 ? { color: "#c0392b" } : undefined}>
                        {c ? q(c.quantity) : "—"}
                        {c && c.incomingQuantity > 0 && <span className="text-muted" style={{ fontSize: 12 }}> (+{q(c.incomingQuantity)} {x.incoming})</span>}
                      </td>
                    );
                  })}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
