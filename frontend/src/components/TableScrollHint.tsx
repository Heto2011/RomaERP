import { useEffect, useState } from "react";
import { useLocation } from "react-router-dom";
import { useLanguage } from "../i18n/LanguageContext";

/** On a phone, a wide table scrolls sideways but nothing says so. When a page opens with a table wider than the screen, a small note shows for a few seconds (and goes away as soon as the person scrolls sideways). */
export default function TableScrollHint() {
  const { pathname } = useLocation();
  const { lang } = useLanguage();
  const [show, setShow] = useState(false);

  useEffect(() => {
    setShow(false);
    if (typeof window === "undefined" || window.innerWidth > 820) return;
    let hideTimer = 0;
    const check = window.setTimeout(() => {
      const wide = Array.from(document.querySelectorAll<HTMLElement>(".card")).some((c) => c.querySelector("table") && c.scrollWidth > c.clientWidth + 8);
      if (!wide) return;
      setShow(true);
      hideTimer = window.setTimeout(() => setShow(false), 6000);
    }, 900);
    const onScroll = (e: Event) => {
      const t = e.target as HTMLElement | null;
      if (t && t.scrollLeft !== undefined && t.scrollLeft !== 0) setShow(false);
    };
    document.addEventListener("scroll", onScroll, true);
    return () => { window.clearTimeout(check); window.clearTimeout(hideTimer); document.removeEventListener("scroll", onScroll, true); };
  }, [pathname]);

  if (!show) return null;
  return (
    <div role="status" style={{ position: "fixed", left: "50%", transform: "translateX(-50%)", bottom: 16, zIndex: 60, background: "#000c", color: "#fff", padding: "8px 14px", borderRadius: 20, fontSize: 13, pointerEvents: "none", whiteSpace: "nowrap" }}>
      {lang === "ar" ? "↔ اسحب الجدول يمين وشمال لتشوف الباقي" : "↔ Swipe the table sideways to see the rest"}
    </div>
  );
}
