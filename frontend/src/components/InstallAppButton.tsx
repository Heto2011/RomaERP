import { useEffect, useState } from "react";
import { useLanguage } from "../i18n/LanguageContext";

interface InstallEvent extends Event { prompt: () => Promise<void> }

/** Lets a person put the system on their phone's home screen like an app. Android/desktop Chrome gets the real install prompt; iPhone gets the two-step hint, since Safari has no prompt. */
export default function InstallAppButton() {
  const { lang } = useLanguage();
  const [evt, setEvt] = useState<InstallEvent | null>(null);
  const [hint, setHint] = useState(false);
  const standalone = typeof window !== "undefined" && (window.matchMedia("(display-mode: standalone)").matches || (navigator as unknown as { standalone?: boolean }).standalone === true);
  const ios = typeof navigator !== "undefined" && /iphone|ipad|ipod/i.test(navigator.userAgent);

  useEffect(() => {
    const on = (e: Event) => { e.preventDefault(); setEvt(e as InstallEvent); };
    window.addEventListener("beforeinstallprompt", on);
    return () => window.removeEventListener("beforeinstallprompt", on);
  }, []);

  if (standalone || (!evt && !ios)) return null;
  const ar = lang === "ar";
  return (
    <span style={{ position: "relative" }}>
      <button type="button" className="btn btn-secondary btn-sm" onClick={() => (evt ? evt.prompt() : setHint((h) => !h))}>{ar ? "ثبّت التطبيق" : "Install app"}</button>
      {hint && (
        <div className="card" style={{ position: "absolute", insetInlineEnd: 0, top: "110%", width: 240, zIndex: 50 }}>
          {ar ? "من زر المشاركة في سفاري اختار \"إضافة إلى الشاشة الرئيسية\"." : "In Safari tap the Share button, then \"Add to Home Screen\"."}
        </div>
      )}
    </span>
  );
}
