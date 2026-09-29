import { useEffect, useRef, useState } from "react";

/// Small "?" badge next to a report title — click it to see a plain-language explanation of what
/// the report means and how it's calculated. Meant for reports a customer wouldn't recognize by
/// name alone (most of the custom ones, as opposed to standard reports like the Income Statement).
export default function InfoTooltip({ text }: { text: string }) {
  const [open, setOpen] = useState(false);
  const containerRef = useRef<HTMLSpanElement>(null);

  useEffect(() => {
    function onOutside(e: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) setOpen(false);
    }
    function onEscape(e: KeyboardEvent) {
      if (e.key === "Escape") setOpen(false);
    }
    document.addEventListener("mousedown", onOutside);
    document.addEventListener("keydown", onEscape);
    return () => {
      document.removeEventListener("mousedown", onOutside);
      document.removeEventListener("keydown", onEscape);
    };
  }, []);

  return (
    <span className="info-tooltip" ref={containerRef}>
      <button type="button" className="info-tooltip-icon" onClick={() => setOpen((o) => !o)} aria-label="؟">
        ؟
      </button>
      {open && <div className="info-tooltip-popover">{text}</div>}
    </span>
  );
}
