import { useEffect, useState } from "react";
import QRCode from "qrcode";
import { useLanguage } from "../i18n/LanguageContext";
import type { RestaurantTable } from "../api/types";
import { QrOrdersApi } from "../api/services";

const TEXT = {
  ar: { title: "أكواد QR للمنيو", intro: "اطبع الورقة دي وحط كل كود على الطاولة بتاعته. العميل يصوّره بموبايله ويشوف المنيو بالأسعار.", menuOnly: "كود المنيو العام", table: "طاولة", scan: "صوّر الكود لتشوف المنيو", print: "طباعة", close: "إغلاق" },
  en: { title: "Menu QR codes", intro: "Print this sheet and put each code on its table. The guest scans it with their phone and sees the menu with prices.", menuOnly: "General menu code", table: "Table", scan: "Scan to see the menu", print: "Print", close: "Close" },
};

/** One printable sheet: a QR code per table (and one general code) that opens the public menu page. */
export default function TableQrSheet({ tables, onClose }: { tables: RestaurantTable[]; onClose: () => void }) {
  const { lang } = useLanguage();
  const x = TEXT[lang === "ar" ? "ar" : "en"];
  const companyCode = localStorage.getItem("companyCode") ?? "";
  const base = `${window.location.origin}/m/${encodeURIComponent(companyCode)}`;
  const [images, setImages] = useState<Record<string, string>>({});
  // Each table's secret code, so the QR opens the menu with ordering switched on for that table only.
  const [codes, setCodes] = useState<Record<string, string> | null>(null);

  useEffect(() => {
    QrOrdersApi.codes().then((r) => setCodes(Object.fromEntries(r.data.map((c) => [c.tableId, c.code])))).catch(() => setCodes({}));
  }, []);

  useEffect(() => {
    if (codes === null) return;
    const targets = [{ key: "_", url: base }, ...tables.map((t) => ({ key: t.id, url: `${base}?table=${encodeURIComponent(t.number)}${codes[t.id] ? `&k=${encodeURIComponent(codes[t.id])}` : ""}` }))];
    Promise.all(targets.map(async (t) => [t.key, await QRCode.toDataURL(t.url, { margin: 1, width: 240 })] as const))
      .then((pairs) => setImages(Object.fromEntries(pairs))).catch(() => setImages({}));
  }, [base, tables, codes]);

  const cards = [{ key: "_", label: x.menuOnly }, ...tables.map((t) => ({ key: t.id, label: `${x.table} ${t.number}` }))];
  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="card" style={{ maxWidth: 760, margin: "4% auto", maxHeight: "86vh", overflowY: "auto" }} onClick={(e) => e.stopPropagation()}>
        <style>{`@media print { body * { visibility: hidden; } .qr-print, .qr-print * { visibility: visible; } .qr-print { position: absolute; inset: 0; } .no-print { display: none !important; } }`}</style>
        <div className="no-print" style={{ display: "flex", justifyContent: "space-between", gap: 8 }}>
          <h3 style={{ margin: 0 }}>{x.title}</h3>
          <div style={{ display: "flex", gap: 8 }}>
            <button className="btn" onClick={() => window.print()}>{x.print}</button>
            <button className="btn btn-secondary" onClick={onClose}>{x.close}</button>
          </div>
        </div>
        <p className="text-muted no-print">{x.intro}</p>
        <div className="qr-print" style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(200px, 1fr))", gap: 16 }}>
          {cards.map((c) => (
            <div key={c.key} style={{ border: "1px solid #999", borderRadius: 8, padding: 12, textAlign: "center", breakInside: "avoid", background: "#fff", color: "#000" }}>
              {images[c.key] ? <img src={images[c.key]} alt={c.label} width={170} height={170} /> : <div style={{ height: 170 }} />}
              <div style={{ fontWeight: 700, fontSize: 18 }}>{c.label}</div>
              <div style={{ fontSize: 12 }}>{x.scan}</div>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
