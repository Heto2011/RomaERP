import { useRef, useState } from "react";
import { Link } from "react-router-dom";
import { apiClient, getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";

const TEXT = {
  ar: {
    title: "رفع شجرة الحسابات من ملف", back: "رجوع لشجرة الحسابات",
    intro: "ارفع ملف Excel أو CSV فيه شجرة حساباتك. هتشوفها أول حاجة في جدول تقدر تعدّل فيه كل خانة، ومفيش حاجة بتتغيّر في النظام غير لما تعتمد. بعد الاعتماد، الشجرة اللي رفعتها هي الشجرة الشغّالة.",
    safe: "الحسابات القديمة اللي عليها قيود مش بتتمسح، بتفضل موجودة ومتوقفة عشان التقارير القديمة تفضل صح. والحسابات اللي النظام نفسه بيسجّل عليها (الصندوق والعملاء والمبيعات والمرتبات...) بتفضل زي ما هي.",
    template: "تحميل نموذج Excel", choose: "اختار الملف", reading: "جاري القراءة...", recheck: "تحقق من التعديلات", approve: "اعتماد الرفع واستبدال الشجرة",
    code: "الكود", nameAr: "الاسم عربي", nameEn: "الاسم إنجليزي", type: "النوع", nature: "الطبيعة", parent: "الحساب الأب", status: "ملاحظات",
    types: { 1: "أصول", 2: "خصوم", 3: "حقوق ملكية", 4: "إيرادات", 5: "مصروفات" } as Record<number, string>,
    natures: { 1: "مدين", 2: "دائن" } as Record<number, string>,
    remove: "حذف السطر", addRow: "إضافة سطر",
    summary: "اللي هيحصل لما تعتمد", newA: "حسابات جديدة", updated: "حسابات هتتحدّث", removed: "حسابات قديمة هتتمسح (مفيش حركات عليها)", deact: "حسابات قديمة هتتوقف (عليها قيود)", kept: "حسابات النظام اللي هتفضل زي ما هي",
    errors: "أخطاء لازم تتصلّح", warnings: "تنبيهات", confirm: "هتستبدل شجرة الحسابات الحالية بالشجرة دي. تأكيد؟", done: "اتعمل. الشجرة الجديدة شغّالة.", goTree: "افتح شجرة الحسابات",
    pdf: "ملف PDF لسه مش مدعوم: حوّله لـ Excel أو CSV.",
  },
  en: {
    title: "Upload a chart of accounts", back: "Back to the chart of accounts",
    intro: "Upload an Excel or CSV file with your chart. You first see it in a table where every cell can be edited, and nothing changes in the system until you approve. After approval, the uploaded chart is the working chart.",
    safe: "Old accounts that have entries are never deleted: they stay, switched off, so past reports remain correct. Accounts the system itself posts to (cash, customers, sales, payroll...) stay as they are.",
    template: "Download the Excel template", choose: "Choose file", reading: "Reading...", recheck: "Check my edits", approve: "Approve and replace the chart",
    code: "Code", nameAr: "Name (Arabic)", nameEn: "Name (English)", type: "Type", nature: "Nature", parent: "Parent", status: "Notes",
    types: { 1: "Asset", 2: "Liability", 3: "Equity", 4: "Revenue", 5: "Expense" } as Record<number, string>,
    natures: { 1: "Debit", 2: "Credit" } as Record<number, string>,
    remove: "Remove line", addRow: "Add line",
    summary: "What approving will do", newA: "new accounts", updated: "accounts updated", removed: "old accounts removed (no movements)", deact: "old accounts switched off (they have entries)", kept: "system accounts kept as they are",
    errors: "Errors to fix", warnings: "Warnings", confirm: "This replaces the current chart of accounts with this one. Confirm?", done: "Done. The new chart is in use.", goTree: "Open the chart of accounts",
    pdf: "PDF is not supported yet: convert it to Excel or CSV.",
  },
};

type Row = { code: string; nameAr: string; nameEn: string; accountType: number; nature: number; parentCode: string | null; isControlAccount: boolean; errors: string[]; warnings: string[] };
type Summary = { newAccounts: number; updatedAccounts: number; removedAccounts: number; deactivatedAccounts: number; keptForSystem: string[]; notes: string[] };
type Preview = { rows: Row[]; summary: Summary; canApply: boolean; errorCount: number; warningCount: number; fileNotes: string[] };

export default function ChartImport() {
  const { lang } = useLanguage();
  const x = TEXT[lang === "ar" ? "ar" : "en"];
  const fileRef = useRef<HTMLInputElement>(null);
  const [preview, setPreview] = useState<Preview | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [done, setDone] = useState<Summary | null>(null);

  const payload = (rows: Row[]) => ({ rows: rows.map((r) => ({ ...r, parentCode: r.parentCode || null })) });

  async function upload(file: File) {
    setError(null); setDone(null); setBusy(true);
    try {
      if (file.name.toLowerCase().endsWith(".pdf")) { setError(x.pdf); return; }
      const form = new FormData();
      form.append("file", file);
      setPreview((await apiClient.post<Preview>("/accounts/import/preview", form)).data);
    } catch (e) { setError(getErrorMessage(e)); }
    finally { setBusy(false); if (fileRef.current) fileRef.current.value = ""; }
  }

  async function recheck() {
    if (!preview) return;
    setError(null); setBusy(true);
    try { setPreview((await apiClient.post<Preview>("/accounts/import/validate", payload(preview.rows))).data); }
    catch (e) { setError(getErrorMessage(e)); }
    finally { setBusy(false); }
  }

  async function approve() {
    if (!preview || !preview.canApply || !window.confirm(x.confirm)) return;
    setError(null); setBusy(true);
    try {
      const res = (await apiClient.post<{ summary: Summary }>("/accounts/import/apply", payload(preview.rows))).data;
      setDone(res.summary); setPreview(null);
    } catch (e) { setError(getErrorMessage(e)); }
    finally { setBusy(false); }
  }

  async function downloadTemplate() {
    try {
      const res = await apiClient.get<Blob>("/accounts/import/template", { responseType: "blob" });
      const a = document.createElement("a");
      a.href = URL.createObjectURL(res.data); a.download = "chart-of-accounts-template.xlsx"; a.click(); URL.revokeObjectURL(a.href);
    } catch (e) { setError(getErrorMessage(e)); }
  }

  const edit = (i: number, patch: Partial<Row>) => preview && setPreview({ ...preview, canApply: false, rows: preview.rows.map((r, k) => (k === i ? { ...r, ...patch } : r)) });
  const removeRow = (i: number) => preview && setPreview({ ...preview, canApply: false, rows: preview.rows.filter((_, k) => k !== i) });
  const addRow = () => preview && setPreview({ ...preview, canApply: false, rows: [...preview.rows, { code: "", nameAr: "", nameEn: "", accountType: 1, nature: 1, parentCode: null, isControlAccount: false, errors: [], warnings: [] }] });

  return (
    <div>
      <div className="page-header"><h1>{x.title}</h1><Link className="btn btn-secondary" to="/accounting/chart-of-accounts">{x.back}</Link></div>
      <p className="text-muted">{x.intro}</p>
      <p className="text-muted">{x.safe}</p>
      <div className="card" style={{ display: "flex", gap: 8, flexWrap: "wrap", alignItems: "center" }}>
        <button className="btn btn-secondary" onClick={downloadTemplate}>{x.template}</button>
        <input ref={fileRef} type="file" accept=".xlsx,.xlsm,.csv,.txt,.tsv,.pdf" disabled={busy} onChange={(e) => { const f = e.target.files?.[0]; if (f) void upload(f); }} />
        {busy && <span className="text-muted">{x.reading}</span>}
      </div>
      {error && <div className="alert-error">{error}</div>}
      {done && (
        <div className="card">
          <strong>{x.done}</strong>
          <div>{done.newAccounts} {x.newA} · {done.updatedAccounts} {x.updated} · {done.removedAccounts} {x.removed} · {done.deactivatedAccounts} {x.deact}</div>
          <Link className="btn" style={{ marginTop: 10, display: "inline-block" }} to="/accounting/chart-of-accounts">{x.goTree}</Link>
          <Link className="btn btn-secondary" style={{ marginTop: 10, marginInlineStart: 8, display: "inline-block" }} to="/accounting/transfer-balance">{lang === "ar" ? "نقل أرصدة الحسابات القديمة" : "Move old account balances"}</Link>
        </div>
      )}

      {preview && (
        <>
          {preview.fileNotes.length > 0 && <div className="card text-muted">{preview.fileNotes.map((n, i) => <div key={i}>{n}</div>)}</div>}
          <div className="card">
            <h3 style={{ marginTop: 0 }}>{x.summary}</h3>
            <div>{preview.summary.newAccounts} {x.newA} · {preview.summary.updatedAccounts} {x.updated} · {preview.summary.removedAccounts} {x.removed} · {preview.summary.deactivatedAccounts} {x.deact}</div>
            {preview.summary.keptForSystem.length > 0 && (
              <details style={{ marginTop: 6 }}><summary>{preview.summary.keptForSystem.length} {x.kept}</summary>{preview.summary.keptForSystem.map((k) => <div key={k} className="text-muted">{k}</div>)}</details>
            )}
            {preview.summary.notes.map((n, i) => <div key={i} className="text-muted">{n}</div>)}
            {preview.errorCount > 0 && <div style={{ color: "#b42318", fontWeight: 700, marginTop: 6 }}>{preview.errorCount} {x.errors}</div>}
            {preview.warningCount > 0 && <div style={{ color: "#b54708", marginTop: 4 }}>{preview.warningCount} {x.warnings}</div>}
          </div>

          <div className="card" style={{ overflowX: "auto" }}>
            <table>
              <thead><tr><th>{x.code}</th><th>{x.nameAr}</th><th>{x.nameEn}</th><th>{x.type}</th><th>{x.nature}</th><th>{x.parent}</th><th>{x.status}</th><th /></tr></thead>
              <tbody>
                {preview.rows.map((r, i) => (
                  <tr key={i} style={r.errors.length ? { background: "rgba(180,35,24,0.10)" } : undefined}>
                    <td><input style={{ width: 90 }} value={r.code} onChange={(e) => edit(i, { code: e.target.value })} /></td>
                    <td><input value={r.nameAr} onChange={(e) => edit(i, { nameAr: e.target.value })} /></td>
                    <td><input value={r.nameEn} onChange={(e) => edit(i, { nameEn: e.target.value })} /></td>
                    <td><select value={r.accountType} onChange={(e) => edit(i, { accountType: Number(e.target.value) })}>{[1, 2, 3, 4, 5].map((n) => <option key={n} value={n}>{x.types[n]}</option>)}</select></td>
                    <td><select value={r.nature} onChange={(e) => edit(i, { nature: Number(e.target.value) })}>{[1, 2].map((n) => <option key={n} value={n}>{x.natures[n]}</option>)}</select></td>
                    <td><input style={{ width: 90 }} value={r.parentCode ?? ""} onChange={(e) => edit(i, { parentCode: e.target.value })} /></td>
                    <td style={{ fontSize: 12 }}>
                      {r.errors.map((m, k) => <div key={k} style={{ color: "#b42318" }}>⚠ {m}</div>)}
                      {r.warnings.map((m, k) => <div key={k} style={{ color: "#b54708" }}>{m}</div>)}
                    </td>
                    <td><button className="btn btn-secondary btn-sm" onClick={() => removeRow(i)} title={x.remove}>✕</button></td>
                  </tr>
                ))}
              </tbody>
            </table>
            <div style={{ display: "flex", gap: 8, marginTop: 12, flexWrap: "wrap" }}>
              <button className="btn btn-secondary" onClick={addRow}>{x.addRow}</button>
              <button className="btn btn-secondary" onClick={recheck} disabled={busy}>{x.recheck}</button>
              <button className="btn" onClick={approve} disabled={busy || !preview.canApply}>{x.approve}</button>
            </div>
          </div>
        </>
      )}
    </div>
  );
}
