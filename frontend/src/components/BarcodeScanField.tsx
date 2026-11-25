import { useEffect, useRef, useState } from "react";
import { useLanguage } from "../i18n/LanguageContext";

type Detector = { detect: (v: HTMLVideoElement) => Promise<{ rawValue: string }[]> };
type DetectorCtor = new (o?: { formats?: string[] }) => Detector;

/** A box for scanned codes. A USB/Bluetooth scanner types the code and presses Enter, so it works as it is; where the phone browser supports it, a camera button reads the code from the product. */
export default function BarcodeScanField({ onScan, autoFocus }: { onScan: (code: string) => void | Promise<void>; autoFocus?: boolean }) {
  const { lang } = useLanguage();
  const ar = lang === "ar";
  const [value, setValue] = useState("");
  const [busy, setBusy] = useState(false);
  const [camera, setCamera] = useState(false);
  const [camError, setCamError] = useState<string | null>(null);
  const video = useRef<HTMLVideoElement>(null);
  const Ctor = (typeof window !== "undefined" ? (window as unknown as { BarcodeDetector?: DetectorCtor }).BarcodeDetector : undefined);

  async function submit(code: string) {
    const c = code.trim();
    if (!c || busy) return;
    setBusy(true);
    try { await onScan(c); } finally { setBusy(false); setValue(""); }
  }

  useEffect(() => {
    if (!camera || !Ctor) return;
    let stream: MediaStream | null = null; let stop = false;
    (async () => {
      try {
        stream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: "environment" } });
        if (video.current) { video.current.srcObject = stream; await video.current.play(); }
        const det = new Ctor({ formats: ["ean_13", "ean_8", "upc_a", "upc_e", "code_128", "code_39", "qr_code"] });
        while (!stop) {
          if (video.current) {
            const found = await det.detect(video.current).catch(() => []);
            if (found.length > 0) { stop = true; setCamera(false); await submit(found[0].rawValue); break; }
          }
          await new Promise((r) => setTimeout(r, 300));
        }
      } catch { setCamError(ar ? "مقدرتش أفتح الكاميرا. اسمح للمتصفح بالوصول لها." : "Could not open the camera. Allow the browser to use it."); setCamera(false); }
    })();
    return () => { stop = true; stream?.getTracks().forEach((t) => t.stop()); };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [camera]);

  return (
    <div style={{ display: "flex", gap: 6, alignItems: "center", flexWrap: "wrap" }}>
      <input
        value={value} autoFocus={autoFocus} inputMode="text" autoComplete="off" disabled={busy}
        placeholder={ar ? "امسح الباركود أو اكتبه واضغط Enter" : "Scan the barcode or type it and press Enter"}
        onChange={(e) => setValue(e.target.value)}
        onKeyDown={(e) => { if (e.key === "Enter") { e.preventDefault(); void submit(value); } }}
        style={{ flex: 1, minWidth: 200 }}
      />
      {Ctor && <button type="button" className="btn btn-secondary btn-sm" onClick={() => { setCamError(null); setCamera(true); }}>📷 {ar ? "كاميرا" : "Camera"}</button>}
      {camError && <span className="text-danger" style={{ fontSize: 13 }}>{camError}</span>}
      {camera && (
        <div className="modal-overlay" onClick={() => setCamera(false)}>
          <div className="card" style={{ maxWidth: 420, margin: "8% auto", color: "var(--color-text)" }} onClick={(e) => e.stopPropagation()}>
            <video ref={video} playsInline muted style={{ width: "100%", borderRadius: 8 }} />
            <p className="text-muted" style={{ marginBottom: 8 }}>{ar ? "وجّه الكاميرا على الباركود" : "Point the camera at the barcode"}</p>
            <button type="button" className="btn btn-secondary" onClick={() => setCamera(false)}>{ar ? "إغلاق" : "Close"}</button>
          </div>
        </div>
      )}
    </div>
  );
}
