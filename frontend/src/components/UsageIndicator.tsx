import { useEffect, useRef, useState } from "react";
import { UsageApi } from "../api/services";
import { useAuth } from "../context/AuthContext";
import { useLanguage } from "../i18n/LanguageContext";
import type { Usage } from "../api/types";

export default function UsageIndicator() {
  const { user } = useAuth();
  const { t, lang } = useLanguage();
  const [open, setOpen] = useState(false);
  const [usage, setUsage] = useState<Usage | null>(null);
  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    function onOutside(e: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) setOpen(false);
    }
    document.addEventListener("mousedown", onOutside);
    return () => document.removeEventListener("mousedown", onOutside);
  }, []);

  if (!user?.roles.includes("Admin")) return null;

  function toggle() {
    if (!open) UsageApi.get().then((r) => setUsage(r.data));
    setOpen(!open);
  }

  return (
    <div className="global-search" ref={containerRef} style={{ width: "auto", marginInlineStart: "auto" }}>
      <button type="button" className="btn btn-secondary btn-sm" onClick={toggle} title={t.usage.title}>
        📊 {t.usage.title}
      </button>
      {open && (
        <div className="global-search-dropdown" style={{ width: 240, insetInlineEnd: 0, insetInlineStart: "auto" }}>
          {!usage && <div className="global-search-empty">…</div>}
          {usage && (
            <div style={{ padding: "10px 12px", display: "flex", flexDirection: "column", gap: 8 }}>
              <div style={{ display: "flex", justifyContent: "space-between" }}>
                <span className="text-muted">{t.usage.activeUsers}</span>
                <strong>{usage.activeUsers}</strong>
              </div>
              <div style={{ display: "flex", justifyContent: "space-between" }}>
                <span className="text-muted">{t.usage.activeBranches}</span>
                <strong>{usage.activeBranches}</strong>
              </div>
              <div className="text-muted" style={{ fontSize: 12, borderTop: "1px solid var(--color-border)", paddingTop: 8 }}>
                {t.usage.updatedAt}: {new Date(usage.generatedAtUtc).toLocaleString(lang === "ar" ? "ar-EG" : "en-US")}
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
