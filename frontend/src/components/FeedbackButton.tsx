import { useEffect, useState } from "react";
import { createPortal } from "react-dom";
import { apiClient, getErrorMessage } from "../api/client";
import { useLanguage } from "../i18n/LanguageContext";

const TEXT = {
  ar: { title: "رأيك يهمنا", intro: "قيّم تجربتك هذا الشهر. دقيقة واحدة، وبتساعدنا نتحسّن.", rating: "تقييمك", comment: "إيه رأيك؟ إيه اللي عجبك وإيه اللي نحسّنه؟", name: "الاسم اللي يظهر لو وافقت على النشر",
    publish: "أوافق إن رأيي يتنشر على موقع روما.", showCompany: "اظهر اسم شركتي جنبه.", send: "إرسال", later: "بعدين", thanks: "شكرًا على رأيك! وصلنا.", tooltip: "قيّم تجربتك" },
  en: { title: "We value your opinion", intro: "Rate your experience this month. It takes a minute and helps us improve.", rating: "Your rating", comment: "What do you think? What did you like, and what should we improve?", name: "Name to show if you agree to publish",
    publish: "I agree that my review may be published on the Roma website.", showCompany: "Show my company name next to it.", send: "Send", later: "Later", thanks: "Thank you for your feedback! We got it.", tooltip: "Rate your experience" },
};
type Status = { inWindow: boolean; periodKey: string; answered: boolean };
const dismissedKey = (p: string) => `roma.feedback.dismissed.${p}`;

/** A star in the top bar. At the end of the month a dot appears on it and the question opens once; the person can always open it themselves. */
export default function FeedbackButton() {
  const { lang } = useLanguage();
  const x = TEXT[lang === "ar" ? "ar" : "en"];
  const [status, setStatus] = useState<Status | null>(null);
  const [open, setOpen] = useState(false);
  const [rating, setRating] = useState(0);
  const [comment, setComment] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [allowPublish, setAllowPublish] = useState(false);
  const [showCompany, setShowCompany] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [done, setDone] = useState(false);

  useEffect(() => {
    apiClient.get<Status>("/feedback/status").then((r) => {
      setStatus(r.data);
      let dismissed = false;
      try { dismissed = localStorage.getItem(dismissedKey(r.data.periodKey)) === "1"; } catch { /* ignore */ }
      if (r.data.inWindow && !r.data.answered && !dismissed) setOpen(true);
    }).catch(() => undefined);
  }, []);

  function close() {
    setOpen(false);
    if (status) { try { localStorage.setItem(dismissedKey(status.periodKey), "1"); } catch { /* ignore */ } }
  }
  async function send(e: React.FormEvent) {
    e.preventDefault(); setError(null);
    try {
      await apiClient.post("/feedback", { rating, comment: comment || undefined, displayName: displayName || undefined, allowPublish, showCompany });
      setDone(true); setStatus(status ? { ...status, answered: true } : status);
      window.setTimeout(() => { setOpen(false); setDone(false); }, 1800);
    } catch (err) { setError(getErrorMessage(err)); }
  }
  const askNow = !!status && status.inWindow && !status.answered;

  return (
    <>
      <button className="sidebar-toggle" onClick={() => setOpen(true)} title={x.tooltip} aria-label={x.tooltip} style={{ position: "relative" }}>
        <span aria-hidden style={{ fontSize: 16, lineHeight: 1 }}>★</span>
        {askNow && <span style={{ position: "absolute", top: 2, insetInlineEnd: 2, width: 8, height: 8, borderRadius: 8, background: "#f5a623" }} />}
      </button>
      {open && createPortal(
        <div className="modal-overlay" onClick={close}>
          <form className="card" style={{ maxWidth: 460, margin: "8% auto", color: "var(--color-text)" }} onClick={(e) => e.stopPropagation()} onSubmit={send}>
            {done ? <p style={{ fontWeight: 700 }}>{x.thanks}</p> : (
              <>
                <h3 style={{ marginTop: 0 }}>{x.title}</h3>
                <p className="text-muted">{x.intro}</p>
                <div className="form-field"><label>{x.rating}</label>
                  <div style={{ fontSize: 30, cursor: "pointer", letterSpacing: 4 }}>
                    {[1, 2, 3, 4, 5].map((n) => <span key={n} onClick={() => setRating(n)} style={{ color: n <= rating ? "#f5a623" : "var(--color-border)" }} role="button" aria-label={`${n}`}>★</span>)}
                  </div></div>
                <div className="form-field"><label>{x.comment}</label><textarea rows={4} value={comment} onChange={(e) => setComment(e.target.value)} maxLength={1000} /></div>
                <label style={{ display: "flex", gap: 8, alignItems: "center", fontSize: 13 }}><input type="checkbox" checked={allowPublish} onChange={(e) => setAllowPublish(e.target.checked)} /> {x.publish}</label>
                {allowPublish && (
                  <>
                    <div className="form-field" style={{ marginTop: 8 }}><label>{x.name}</label><input value={displayName} onChange={(e) => setDisplayName(e.target.value)} maxLength={80} /></div>
                    <label style={{ display: "flex", gap: 8, alignItems: "center", fontSize: 13 }}><input type="checkbox" checked={showCompany} onChange={(e) => setShowCompany(e.target.checked)} /> {x.showCompany}</label>
                  </>
                )}
                {error && <div className="alert-error" style={{ marginTop: 8 }}>{error}</div>}
                <div style={{ display: "flex", gap: 8, marginTop: 12 }}>
                  <button className="btn" type="submit" disabled={rating === 0}>{x.send}</button>
                  <button className="btn btn-secondary" type="button" onClick={close}>{x.later}</button>
                </div>
              </>
            )}
          </form>
        </div>,
        document.body
      )}
    </>
  );
}
