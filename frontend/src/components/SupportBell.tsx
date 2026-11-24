import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { SupportApi } from "../api/services";
import { useLanguage } from "../i18n/LanguageContext";

const KEY = "roma.support.seen";
const read = (): Record<string, number> => { try { return JSON.parse(localStorage.getItem(KEY) ?? "{}"); } catch { return {}; } };
const stamp = (t: { updatedAtUtc: string | null; createdAtUtc: string }) => t.updatedAtUtc ?? t.createdAtUtc;

/** Remember that the person has now seen this ticket's latest reply. */
export function markSupportSeen(id: string) {
  try { localStorage.setItem(KEY, JSON.stringify({ ...read(), [id]: Date.now() })); window.dispatchEvent(new Event("roma-support-seen")); } catch { /* ignore */ }
}

/** A chat icon in the top bar: a dot appears when the support team (or the assistant) answered a ticket and the person has not opened it yet. The reply itself is read inside the system. */
export default function SupportBell() {
  const { lang } = useLanguage();
  const nav = useNavigate();
  const [count, setCount] = useState(0);

  useEffect(() => {
    let alive = true;
    const check = () => SupportApi.getMyTickets().then((r) => {
      if (!alive) return;
      const seen = read();
      setCount(r.data.filter((t) => t.status === "AwaitingCustomer" && Date.parse(stamp(t)) > (seen[t.id] ?? 0)).length);
    }).catch(() => undefined);
    check();
    const timer = window.setInterval(check, 120000);
    window.addEventListener("roma-support-seen", check);
    return () => { alive = false; window.clearInterval(timer); window.removeEventListener("roma-support-seen", check); };
  }, []);

  const title = lang === "ar" ? (count ? "في رد على تذكرتك" : "الدعم") : (count ? "You have a reply" : "Support");
  return (
    <button className="sidebar-toggle" onClick={() => nav("/support")} title={title} aria-label={title} style={{ position: "relative" }}>
      <span aria-hidden style={{ fontSize: 15, lineHeight: 1 }}>💬</span>
      {count > 0 && <span style={{ position: "absolute", top: 2, insetInlineEnd: 2, minWidth: 14, height: 14, borderRadius: 7, background: "#e5484d", color: "#fff", fontSize: 10, lineHeight: "14px", textAlign: "center" }}>{count}</span>}
    </button>
  );
}
