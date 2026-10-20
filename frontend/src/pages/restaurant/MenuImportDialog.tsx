import { useState } from "react";
import { RestaurantApi } from "../../api/services";
import type { MenuImportResult, MenuImportRow } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";

const ARABIC_DIGITS = "٠١٢٣٤٥٦٧٨٩";

function toNumber(raw: string): number {
  const western = raw.replace(/[٠-٩]/g, (d) => String(ARABIC_DIGITS.indexOf(d))).replace("٫", ".").replace(/[^0-9.\-]/g, "");
  const n = parseFloat(western);
  return Number.isFinite(n) ? n : -1;
}

/** One dish per line: "category, dish, price". Commas (English or Arabic), semicolons or tabs all work; the last field is the price. */
export function parseMenuText(text: string): MenuImportRow[] {
  const rows: MenuImportRow[] = [];
  const lines = text.replace(/^﻿/, "").split(/\r?\n/).map((l) => l.trim()).filter((l) => l.length > 0);
  lines.forEach((line, index) => {
    const delimiter = line.includes("\t") ? /\t/ : line.includes(";") || line.includes("؛") ? /[;؛]/ : /[,،]/;
    const cells = line.split(delimiter).map((c) => c.trim().replace(/^"|"$/g, ""));
    if (cells.length < 3) {
      rows.push({ category: cells[0] ?? "", nameAr: cells[1] ?? "", price: -1 });
      return;
    }
    const price = toNumber(cells[cells.length - 1]);
    // A header row like "القسم, الصنف, السعر" is skipped.
    if (index === 0 && price === -1 && !/\d/.test(cells[cells.length - 1])) return;
    rows.push({ category: cells[0], nameAr: cells.slice(1, -1).join("، "), price });
  });
  return rows;
}

export default function MenuImportDialog({ onClose, onDone }: { onClose: () => void; onDone: () => void }) {
  const { t } = useLanguage();
  const [text, setText] = useState("");
  const [updatePrices, setUpdatePrices] = useState(false);
  const [preview, setPreview] = useState<MenuImportResult | null>(null);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [scanning, setScanning] = useState(false);
  const [info, setInfo] = useState<string | null>(null);

  async function run(apply: boolean) {
    setError(null);
    const rows = parseMenuText(text);
    if (rows.length === 0) {
      setError(t.restaurant.importEmpty);
      return;
    }
    setBusy(true);
    try {
      const res = await RestaurantApi.importMenu(rows, apply, updatePrices);
      setPreview(res.data);
      if (apply) {
        setSaved(true);
        onDone();
      }
    } catch (e) {
      setError(getErrorMessage(e));
    } finally {
      setBusy(false);
    }
  }

  async function onScan(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    e.target.value = "";
    if (!file) return;
    setError(null);
    setInfo(null);
    setPreview(null);
    setScanning(true);
    try {
      const res = await RestaurantApi.scanMenu(file);
      setText(res.data.rows.map((r) => `${r.category}، ${r.nameAr}، ${r.price}`).join("\n"));
      setInfo(t.restaurant.importScanDone + (res.data.skippedWithoutPrice > 0 ? ` ${t.restaurant.importScanSkipped} ${res.data.skippedWithoutPrice}` : ""));
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setScanning(false);
    }
  }

  async function onFile(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    if (!file) return;
    setText(await file.text());
    setPreview(null);
    e.target.value = "";
  }

  const actionLabel = (a: string) =>
    ({ create: t.restaurant.importRowAction_create, update: t.restaurant.importRowAction_update, same: t.restaurant.importRowAction_same, error: t.restaurant.importRowAction_error } as Record<string, string>)[a] ?? a;
  const actionClass: Record<string, string> = { create: "badge badge-posted", update: "badge badge-draft", same: "badge", error: "badge badge-reversed" };

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="card" style={{ maxWidth: 760, margin: "4% auto", maxHeight: "88vh", overflowY: "auto" }} onClick={(e) => e.stopPropagation()}>
        <h3 style={{ marginTop: 0 }}>{t.restaurant.importMenuTitle}</h3>
        <p className="text-muted">{t.restaurant.importMenuIntro}</p>
        <p className="text-muted">{t.restaurant.importNoStockNote}</p>

        {error && <div className="alert-error">{error}</div>}
        {info && !saved && <div className="alert-success">{info}</div>}
        {scanning && <div className="text-muted">{t.restaurant.importScanning}</div>}
        {saved && <div className="alert-success">{t.restaurant.importDone}</div>}

        {!saved && (
          <>
            <textarea value={text} rows={8} dir="auto" style={{ width: "100%", fontFamily: "inherit" }}
              placeholder={t.restaurant.importMenuExample}
              onChange={(e) => { setText(e.target.value); setPreview(null); }} />
            <div style={{ display: "flex", gap: 14, alignItems: "center", flexWrap: "wrap", marginTop: 8 }}>
              <label className="btn btn-sm" style={{ cursor: scanning ? "wait" : "pointer" }}>
                {t.restaurant.importScanFile}
                <input type="file" accept="application/pdf,image/jpeg,image/png,image/webp" onChange={onScan} disabled={scanning} style={{ display: "none" }} />
              </label>
              <label className="btn btn-secondary btn-sm" style={{ cursor: "pointer" }}>
                {t.restaurant.importUploadFile}
                <input type="file" accept=".csv,.txt,text/csv,text/plain" onChange={onFile} style={{ display: "none" }} />
              </label>
              <label>
                <input type="checkbox" checked={updatePrices} onChange={(e) => { setUpdatePrices(e.target.checked); setPreview(null); }} style={{ marginInlineEnd: 6 }} />
                {t.restaurant.importUpdatePrices}
              </label>
            </div>
          </>
        )}

        {preview && (
          <div style={{ marginTop: 16 }}>
            <div style={{ display: "flex", gap: 22, flexWrap: "wrap", marginBottom: 10 }}>
              <div><div className="text-muted">{t.restaurant.importCreated}</div><strong>{preview.created}</strong></div>
              <div><div className="text-muted">{t.restaurant.importNewCategories}</div><strong>{preview.newCategories}</strong></div>
              <div><div className="text-muted">{t.restaurant.importUpdated}</div><strong>{preview.updated}</strong></div>
              <div><div className="text-muted">{t.restaurant.importUnchanged}</div><strong>{preview.unchanged}</strong></div>
              <div><div className="text-muted">{t.restaurant.importErrors}</div><strong className={preview.errors > 0 ? "text-danger" : ""}>{preview.errors}</strong></div>
            </div>
            <div style={{ maxHeight: 280, overflowY: "auto" }}>
              <table>
                <tbody>
                  {preview.lines.map((l) => (
                    <tr key={l.row}>
                      <td className="text-muted">{l.row}</td>
                      <td>{l.category}</td>
                      <td>{l.nameAr}</td>
                      <td>{l.price >= 0 ? l.price.toLocaleString() : "—"}</td>
                      <td><span className={actionClass[l.action]}>{actionLabel(l.action)}</span>{l.message && <div className="text-muted">{l.message}</div>}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            {preview.errors > 0 && !saved && <div className="alert-error" style={{ marginTop: 10 }}>{t.restaurant.importFixErrors}</div>}
          </div>
        )}

        <div style={{ display: "flex", gap: 10, marginTop: 16 }}>
          {!saved && <button className="btn btn-secondary" onClick={() => run(false)} disabled={busy || scanning || !text.trim()}>{t.restaurant.importPreview}</button>}
          {!saved && preview && preview.errors === 0 && preview.created + preview.updated > 0 && (
            <button className="btn" onClick={() => run(true)} disabled={busy}>{t.restaurant.importConfirm}</button>
          )}
          <button className="btn btn-secondary" onClick={onClose}>{saved ? t.common.close : t.common.cancel}</button>
        </div>
      </div>
    </div>
  );
}
