import { useCallback, useEffect, useState } from "react";
import { QrOrdersApi, type QrOrderRequest } from "../api/services";
import { getErrorMessage } from "../api/client";
import { useLanguage } from "../i18n/LanguageContext";

const TEXT = {
  ar: { title: "طلبات من موبايل العملاء", table: "طاولة", approve: "قبول وإرسال للمطبخ", reject: "رفض", from: "بعيد عن المطعم", m: "متر", note: "ملاحظة" },
  en: { title: "Guest phone orders", table: "Table", approve: "Accept and send to kitchen", reject: "Reject", from: "from the restaurant", m: "m", note: "Note" },
};

/** Orders guests sent from their phones: nothing reaches the kitchen until the cashier accepts it. Hidden when there is none. */
export default function QrOrdersPanel({ onApproved }: { onApproved: () => void }) {
  const { lang } = useLanguage();
  const x = TEXT[lang === "ar" ? "ar" : "en"];
  const [items, setItems] = useState<QrOrderRequest[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState<string | null>(null);

  const load = useCallback(async () => {
    try { setItems((await QrOrdersApi.pending()).data); } catch { /* the panel is only a convenience; the POS keeps working */ }
  }, []);

  useEffect(() => {
    load();
    const h = window.setInterval(load, 8000);
    return () => window.clearInterval(h);
  }, [load]);

  async function decide(id: string, approve: boolean) {
    setBusy(id); setError(null);
    try {
      if (approve) { await QrOrdersApi.approve(id); onApproved(); } else await QrOrdersApi.reject(id);
      await load();
    } catch (e) { setError(getErrorMessage(e)); } finally { setBusy(null); }
  }

  if (items.length === 0 && !error) return null;
  return (
    <div className="card" style={{ marginBottom: 12, borderInlineStart: "4px solid #d97706" }}>
      <strong>{x.title} ({items.length})</strong>
      {error && <div className="alert-error" style={{ marginTop: 8 }}>{error}</div>}
      {items.map((r) => (
        <div key={r.id} style={{ borderTop: "1px solid rgba(128,128,128,.3)", marginTop: 10, paddingTop: 10 }}>
          <div style={{ fontWeight: 700 }}>{x.table} {r.tableNumber} <span className="text-muted" style={{ fontWeight: 400 }}>· {r.distanceMeters} {x.m}</span></div>
          <ul style={{ margin: "6px 0", paddingInlineStart: 20 }}>
            {r.lines.map((l, i) => <li key={i}>{l.quantity} × {l.itemName}{l.notes ? ` (${l.notes})` : ""}</li>)}
          </ul>
          {r.note && <div className="text-muted">{x.note}: {r.note}</div>}
          <div style={{ display: "flex", gap: 8, marginTop: 8 }}>
            <button className="btn btn-sm" disabled={busy === r.id} onClick={() => decide(r.id, true)}>{x.approve}</button>
            <button className="btn btn-secondary btn-sm" disabled={busy === r.id} onClick={() => decide(r.id, false)}>{x.reject}</button>
          </div>
        </div>
      ))}
    </div>
  );
}
