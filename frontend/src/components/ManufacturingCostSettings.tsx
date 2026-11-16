import { useEffect, useState } from "react";
import { WarehousesApi } from "../api/services";
import { apiClient, getErrorMessage } from "../api/client";
import type { Warehouse } from "../api/types";
import { useLanguage } from "../i18n/LanguageContext";

const TEXT = {
  ar: { title: "تكاليف التصنيع الثابتة (حسب معيار المخزون IAS 2)", intro: "المصروفات الثابتة للتصنيع (إيجار المطبخ المركزي، إهلاك المعدات، المشرفين) بتتحمّل على تكلفة المنتج بحسب الطاقة العادية للموقع: المعدل = المصروفات الشهرية ÷ عدد الباتشات في الشهر العادي. اللي ما اتحمّلش لأن الموقع اشتغل أقل من الطاقة العادية بيفضل مصروف على الفترة، مش جزء من تكلفة المخزون. وفي الشهور اللي الإنتاج فيها أعلى من العادي، التحميل مبيزيدش عن المصروف الفعلي.",
    site: "موقع التصنيع", monthly: "المصروفات الثابتة الشهرية", normal: "الطاقة العادية (باتشات/شهر)", rate: "المعدل للباتش", save: "حفظ", saved: "اتحفظ.", choose: "اختار موقع", active: "مفعّل",
    report: "تقرير تحميل المصروفات الثابتة", month: "الشهر", year: "السنة", show: "عرض", actual: "الباتشات الفعلية", used: "نسبة استغلال الطاقة", absorbed: "اتحمّل على المخزون", unabsorbed: "فضل مصروف على الفترة", none: "مفيش مواقع تصنيع معرّفة." },
  en: { title: "Fixed production overhead (IAS 2 inventory standard)", intro: "Fixed production costs (central kitchen rent, equipment depreciation, supervisors) are put on the cost of what is produced on the site's NORMAL capacity: rate = monthly overhead ÷ normal batches per month. What is not absorbed because the site worked below normal stays an expense of the period, not part of stock cost. In months above normal, absorption never exceeds the real overhead.",
    site: "Production site", monthly: "Monthly fixed overhead", normal: "Normal capacity (batches/month)", rate: "Rate per batch", save: "Save", saved: "Saved.", choose: "Choose a site", active: "Active",
    report: "Fixed overhead absorption report", month: "Month", year: "Year", show: "Show", actual: "Actual batches", used: "Capacity used", absorbed: "Absorbed into stock", unabsorbed: "Left as period expense", none: "No production sites defined." },
};
type Site = { warehouseId: string; warehouseName: string; monthlyFixedOverhead: number; normalMonthlyBatches: number; isActive: boolean; ratePerBatch: number };
type Row = { warehouseId: string; warehouseName: string; monthlyFixedOverhead: number; normalMonthlyBatches: number; actualBatches: number; capacityUsedPercent: number; absorbed: number; unabsorbed: number };
const money = (n: number) => n.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });

export default function ManufacturingCostSettings() {
  const { lang } = useLanguage();
  const x = TEXT[lang === "ar" ? "ar" : "en"];
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [sites, setSites] = useState<Site[]>([]);
  const [warehouseId, setWarehouseId] = useState("");
  const [monthly, setMonthly] = useState("");
  const [normal, setNormal] = useState("");
  const [isActive, setIsActive] = useState(true);
  const [year, setYear] = useState(new Date().getFullYear());
  const [month, setMonth] = useState(new Date().getMonth() + 1);
  const [rows, setRows] = useState<Row[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  async function loadSites() { try { setSites((await apiClient.get<Site[]>("/manufacturing/sites")).data); } catch (e) { setError(getErrorMessage(e)); } }
  async function loadReport() { try { setRows((await apiClient.get<Row[]>("/manufacturing/overhead-report", { params: { year, month } })).data); } catch (e) { setError(getErrorMessage(e)); } }
  useEffect(() => { WarehousesApi.getAll().then((r) => setWarehouses(r.data.filter((w) => w.isActive))).catch(() => undefined); loadSites(); loadReport(); /* eslint-disable-next-line react-hooks/exhaustive-deps */ }, []);

  function pick(id: string) {
    setWarehouseId(id);
    const s = sites.find((a) => a.warehouseId === id);
    setMonthly(s ? String(s.monthlyFixedOverhead) : ""); setNormal(s ? String(s.normalMonthlyBatches) : ""); setIsActive(s?.isActive ?? true);
  }
  async function save(e: React.FormEvent) {
    e.preventDefault(); setError(null); setMessage(null);
    try {
      await apiClient.put(`/manufacturing/sites/${warehouseId}`, { monthlyFixedOverhead: Number(monthly) || 0, normalMonthlyBatches: Number(normal) || 0, isActive });
      setMessage(x.saved); await loadSites(); await loadReport();
    } catch (err) { setError(getErrorMessage(err)); }
  }

  return (
    <div className="card" style={{ marginTop: 24 }}>
      <h3 style={{ marginTop: 0 }}>{x.title}</h3>
      <p className="text-muted" style={{ fontSize: 13 }}>{x.intro}</p>
      {error && <div className="alert-error">{error}</div>}
      {message && <div className="text-success">{message}</div>}
      <form onSubmit={save}>
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", gap: 12 }}>
          <div className="form-field"><label>{x.site}</label>
            <select value={warehouseId} onChange={(e) => pick(e.target.value)} required><option value="">{x.choose}</option>{warehouses.map((w) => <option key={w.id} value={w.id}>{lang === "ar" ? w.nameAr : w.nameEn || w.nameAr}</option>)}</select></div>
          <div className="form-field"><label>{x.monthly}</label><input type="number" min="0" step="0.01" value={monthly} onChange={(e) => setMonthly(e.target.value)} required /></div>
          <div className="form-field"><label>{x.normal}</label><input type="number" min="0" step="any" value={normal} onChange={(e) => setNormal(e.target.value)} required /></div>
          <div className="form-field"><label>{x.active}</label><input type="checkbox" checked={isActive} onChange={(e) => setIsActive(e.target.checked)} /></div>
        </div>
        <button className="btn" type="submit" disabled={!warehouseId}>{x.save}</button>
      </form>
      {sites.length > 0 && (
        <table style={{ marginTop: 12 }}>
          <thead><tr><th>{x.site}</th><th>{x.monthly}</th><th>{x.normal}</th><th>{x.rate}</th></tr></thead>
          <tbody>{sites.map((s) => <tr key={s.warehouseId}><td>{s.warehouseName}</td><td>{money(s.monthlyFixedOverhead)}</td><td>{s.normalMonthlyBatches.toLocaleString()}</td><td>{money(s.ratePerBatch)}</td></tr>)}</tbody>
        </table>
      )}

      <h4 style={{ marginTop: 20 }}>{x.report}</h4>
      <div className="toolbar" style={{ display: "flex", gap: 8, alignItems: "end" }}>
        <div className="form-field"><label>{x.year}</label><input type="number" value={year} onChange={(e) => setYear(Number(e.target.value))} style={{ width: 90 }} /></div>
        <div className="form-field"><label>{x.month}</label><input type="number" min={1} max={12} value={month} onChange={(e) => setMonth(Number(e.target.value))} style={{ width: 70 }} /></div>
        <button className="btn btn-secondary" type="button" onClick={loadReport}>{x.show}</button>
      </div>
      {rows.length === 0 ? <p className="text-muted">{x.none}</p> : (
        <table style={{ marginTop: 8 }}>
          <thead><tr><th>{x.site}</th><th>{x.monthly}</th><th>{x.actual}</th><th>{x.used}</th><th>{x.absorbed}</th><th>{x.unabsorbed}</th></tr></thead>
          <tbody>{rows.map((r) => (
            <tr key={r.warehouseId}><td>{r.warehouseName}</td><td>{money(r.monthlyFixedOverhead)}</td><td>{r.actualBatches.toLocaleString()} / {r.normalMonthlyBatches.toLocaleString()}</td>
              <td>{r.capacityUsedPercent}%</td><td>{money(r.absorbed)}</td><td className={r.unabsorbed > 0 ? "text-danger" : ""}>{money(r.unabsorbed)}</td></tr>
          ))}</tbody>
        </table>
      )}
    </div>
  );
}
