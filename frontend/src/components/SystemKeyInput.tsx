import { useState } from "react";

const STORE = "roma.system.key";

/** The console key, remembered while this browser tab stays open (never saved to disk), so it is typed once for all console pages. */
export function useSystemKey(): [string, (v: string) => void] {
  const [key, setKey] = useState(() => { try { return sessionStorage.getItem(STORE) ?? ""; } catch { return ""; } });
  const set = (v: string) => { setKey(v); try { sessionStorage.setItem(STORE, v); } catch { /* ignore */ } };
  return [key, set];
}

/** The key box for console pages: a show/hide eye and a copy icon beside it. */
export default function SystemKeyInput({ value, onChange }: { value: string; onChange: (v: string) => void }) {
  const [show, setShow] = useState(false);
  const [copied, setCopied] = useState(false);
  async function copy() {
    try { await navigator.clipboard.writeText(value.trim()); setCopied(true); window.setTimeout(() => setCopied(false), 1500); } catch { /* ignore */ }
  }
  return (
    <span style={{ display: "inline-flex", gap: 6, alignItems: "center" }}>
      <input type={show ? "text" : "password"} value={value} onChange={(e) => onChange(e.target.value)} placeholder="X-System-Key" autoComplete="off" />
      <button type="button" className="btn btn-secondary btn-sm" onClick={() => setShow((s) => !s)} aria-label="show" title="show / hide">{show ? "🙈" : "👁"}</button>
      <button type="button" className="btn btn-secondary btn-sm" disabled={!value} onClick={copy} aria-label="copy" title="copy">{copied ? "✓" : "📋"}</button>
    </span>
  );
}
