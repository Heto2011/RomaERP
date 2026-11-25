import { useEffect, useState } from "react";
import { PhysicalStockCountsApi, ItemsApi, WarehousesApi, EmployeesApi } from "../../api/services";
import type { CountVarianceRow, Employee, Item, PhysicalStockCountEntry, Warehouse } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";
import BarcodeScanField from "../../components/BarcodeScanField";
import { findItemByCode } from "../../utils/barcodeMatch";
import InfoTooltip from "../../components/InfoTooltip";
import { bilingualName } from "../../i18n/bilingual";
import { useAuth } from "../../context/AuthContext";

const TEXT = {
  ar: { branch: "الفرع", anyBranch: "بدون فرع (إجمالي الشركة)", apply: "اعتمد الفرق (تسوية)", applied: "اتسوّى", applyConfirm: "تعمل تسوية مخزون بالفرق ده؟ هيتعمل قيد محاسبي.",
    pending: "منتظر الاعتماد", approved: "معتمد", rejected: "مرفوض", review: "مراجعة واعتماد", status: "الحالة",
    reviewTitle: "مراجعة فرق الجرد", sysQty: "رصيد النظام", countedQty: "المعدود", diffQty: "الفرق", diffValue: "قيمة الفرق",
    whoBears: "مين يتحمّل النقص؟", rBranch: "الفرع (تكلفة بضاعة على الفرع)", rCompany: "الشركة (تكلفة بضاعة على الإدارة)", rEmployee: "موظف (مديونية عليه)", rManager: "مدير الفرع (مديونية عليه)",
    pickEmployee: "اختار الموظف", decisionNote: "ملاحظة القرار", approveBtn: "اعتماد وعمل التسوية", rejectBtn: "رفض (من غير تسوية)", close: "إغلاق",
    gainNote: "ده فرق بالزيادة: هيتسجل زيادة جرد على الفرع.", personNote: "النقص هيتسجّل كمديونية على الشخص (حساب عهد وسلف الموظفين). الخصم من الراتب بتعمله إنت من الرواتب.",
    needApprover: "الاعتماد للمالك أو المحاسب بس.", pendingNote: "الفروقات دي منتظرة قرار المالك أو المحاسب قبل أي تسوية.", borneBy: "اتحمّله",
    report: "فروقات الجرد حسب الفرع والصنف", reportIntro: "مجموع النقص والزيادة وقيمتهم. الأكبر خسارة فوق.", short: "نقص", over: "زيادة", net: "الصافي", netValue: "قيمة الصافي", times: "عدد الجرد", noReport: "مفيش فروقات مسجلة بعد." },
  en: { branch: "Branch", anyBranch: "No branch (company total)", apply: "Apply difference (adjustment)", applied: "Adjusted", applyConfirm: "Make a stock adjustment for this difference? A journal entry will be posted.",
    pending: "Awaiting approval", approved: "Approved", rejected: "Rejected", review: "Review and decide", status: "Status",
    reviewTitle: "Review count difference", sysQty: "System balance", countedQty: "Counted", diffQty: "Difference", diffValue: "Value of difference",
    whoBears: "Who bears the shortage?", rBranch: "The branch (cost of goods on the branch)", rCompany: "The company (cost of goods on head office)", rEmployee: "An employee (a debt on them)", rManager: "Branch manager (a debt on them)",
    pickEmployee: "Choose the employee", decisionNote: "Decision note", approveBtn: "Approve and make the adjustment", rejectBtn: "Reject (no adjustment)", close: "Close",
    gainNote: "This is a surplus: it will be recorded as a count gain for the branch.", personNote: "The shortage is recorded as a debt on the person (employee custody and advances account). You deduct it from pay in payroll.",
    needApprover: "Only the owner or accountant can approve.", pendingNote: "These differences wait for the owner or accountant before any adjustment.", borneBy: "Borne by",
    report: "Count differences by branch and item", reportIntro: "Total shortage and surplus with their value. Biggest losses first.", short: "Short", over: "Over", net: "Net", netValue: "Net value", times: "Counts", noReport: "No differences recorded yet." },
};

export default function PhysicalStockCountsPage() {
  const { t, lang } = useLanguage();
  const x = TEXT[lang === "ar" ? "ar" : "en"];
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [warehouseId, setWarehouseId] = useState("");
  const [report, setReport] = useState<CountVarianceRow[]>([]);
  const { user } = useAuth();
  const canApprove = !!user && (user.roles.includes("Admin") || user.roles.includes("Accountant"));
  const [employees, setEmployees] = useState<Employee[]>([]);
  const [reviewing, setReviewing] = useState<PhysicalStockCountEntry | null>(null);
  const [responsibility, setResponsibility] = useState(1);
  const [employeeId, setEmployeeId] = useState("");
  const [decisionNote, setDecisionNote] = useState("");
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
    if (canApprove) EmployeesApi.getAll().then((r) => setEmployees(r.data)).catch(() => setEmployees([]));
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

  async function decide(approve: boolean) {
    if (!reviewing) return;
    setError(null);
    try {
      if (approve) await PhysicalStockCountsApi.apply(reviewing.id, { responsibility, employeeId: employeeId || undefined, note: decisionNote || undefined });
      else await PhysicalStockCountsApi.reject(reviewing.id, decisionNote || undefined);
      setReviewing(null);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }
  const statusLabel = (c: PhysicalStockCountEntry) => c.status === 1 ? x.pending : c.status === 3 ? x.rejected : c.status === 2 && c.stockAdjustmentId ? x.applied : c.status === 2 ? x.approved : "—";
  const pendingCount = counts.filter((c) => c.status === 1).length;

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
      {pendingCount > 0 && <div className="card" style={{ borderColor: "#c8862e", marginBottom: 12 }}>⚠ {pendingCount} — {x.pendingNote}</div>}

      {reviewing && (
        <div className="card" style={{ borderColor: "#c8862e", marginBottom: 16 }}>
          <h3 style={{ marginTop: 0 }}>{x.reviewTitle}</h3>
          <table>
            <thead><tr><th>{x.branch}</th><th>{t.sales.item}</th><th>{x.sysQty}</th><th>{x.countedQty}</th><th>{x.diffQty}</th><th>{x.diffValue}</th></tr></thead>
            <tbody><tr>
              <td>{reviewing.warehouseName}</td><td>{reviewing.itemCode} - {reviewing.itemName}</td><td>{reviewing.systemQuantity.toLocaleString()}</td><td>{reviewing.countedQuantity.toLocaleString()}</td>
              <td className={reviewing.variance < 0 ? "text-danger" : "text-success"}>{reviewing.variance.toLocaleString()}</td>
              <td className={reviewing.varianceValue < 0 ? "text-danger" : "text-success"}>{reviewing.varianceValue.toLocaleString()}</td>
            </tr></tbody>
          </table>
          {reviewing.variance < 0 ? (
            <div className="form-grid" style={{ marginTop: 12 }}>
              <div className="form-field"><label>{x.whoBears}</label>
                <select value={responsibility} onChange={(e) => setResponsibility(Number(e.target.value))}>
                  <option value={1}>{x.rBranch}</option><option value={2}>{x.rCompany}</option><option value={3}>{x.rEmployee}</option><option value={4}>{x.rManager}</option>
                </select></div>
              {responsibility >= 3 && (
                <div className="form-field"><label>{x.pickEmployee}</label>
                  <select value={employeeId} onChange={(e) => setEmployeeId(e.target.value)}>
                    <option value="">—</option>{employees.map((em) => <option key={em.id} value={em.id}>{em.fullNameAr}</option>)}
                  </select></div>
              )}
            </div>
          ) : <p className="text-muted">{x.gainNote}</p>}
          {responsibility >= 3 && reviewing.variance < 0 && <p className="text-muted" style={{ fontSize: 13 }}>{x.personNote}</p>}
          <div className="form-field"><label>{x.decisionNote}</label><input value={decisionNote} onChange={(e) => setDecisionNote(e.target.value)} maxLength={300} /></div>
          <button className="btn" onClick={() => decide(true)} disabled={reviewing.variance < 0 && responsibility >= 3 && !employeeId}>{x.approveBtn}</button>{" "}
          <button className="btn btn-danger" onClick={() => decide(false)}>{x.rejectBtn}</button>{" "}
          <button className="btn btn-secondary" onClick={() => setReviewing(null)}>{x.close}</button>
        </div>
      )}

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
                <BarcodeScanField onScan={(c) => { const it = findItemByCode(items, c); if (it) { setError(null); setItemId(it.id); } else setError(lang === "ar" ? `مفيش صنف بالباركود ده: ${c}` : `No item has this barcode: ${c}`); }} />
                <select value={itemId} onChange={(e) => setItemId(e.target.value)} required style={{ marginTop: 6 }}>
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
              <th>{x.status}</th>
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
                <td><span className="badge">{statusLabel(c)}</span>{c.responsibleEmployeeName ? ` — ${x.borneBy}: ${c.responsibleEmployeeName}` : ""}{c.decidedBy ? ` (${c.decidedBy})` : ""}</td>
                <td>
                  {c.status === 1 && canApprove && <button className="btn btn-sm" onClick={() => { setReviewing(c); setResponsibility(1); setEmployeeId(""); setDecisionNote(""); }}>{x.review}</button>}{" "}
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
