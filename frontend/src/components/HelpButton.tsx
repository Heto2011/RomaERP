import { useEffect, useState } from "react";
import { useLocation } from "react-router-dom";
import { useLanguage } from "../i18n/LanguageContext";
import { HELP_TOPICS, searchTopics, topicForPath, type HelpTopic } from "../help/helpContent";

const TEXT = {
  ar: { tooltip: "مساعدة", title: "مركز المساعدة", search: "اكتب سؤالك أو كلمة (مثلاً: فاتورة، شجرة الحسابات، رواتب)", none: "ما لقيتش شرح لده. جرّب كلمة تانية، أو كلمنا على support@romagroup.app", back: "رجوع للقايمة", close: "إغلاق", tips: "نصايح", forPage: "شرح الصفحة دي" },
  en: { tooltip: "Help", title: "Help center", search: "Type your question or a word (for example: invoice, chart of accounts, payroll)", none: "No guide found for that. Try another word, or write to support@romagroup.app", back: "Back to the list", close: "Close", tips: "Tips", forPage: "Guide for this page" },
};

/** A "?" in the top bar. It opens the help as an overlay, so it works the same in every screen layout. */
export default function HelpButton() {
  const { lang } = useLanguage();
  const l = lang === "ar" ? "ar" : "en";
  const x = TEXT[l];
  const { pathname } = useLocation();
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState("");
  const [topic, setTopic] = useState<HelpTopic | null>(null);

  useEffect(() => { if (open) { setQuery(""); setTopic(topicForPath(pathname)); } }, [open, pathname]);
  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => { if (e.key === "Escape") setOpen(false); };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [open]);

  const results = searchTopics(query, l);
  const sections = Array.from(new Set(results.map((t) => t.section[l])));

  return (
    <>
      <button className="sidebar-toggle" type="button" onClick={() => setOpen(true)} title={x.tooltip} aria-label={x.tooltip} style={{ fontWeight: 800, fontSize: 16 }}>?</button>
      {open && (
        <div className="modal-overlay" onClick={() => setOpen(false)}>
          <div className="card" role="dialog" aria-label={x.title} style={{ maxWidth: 640, margin: "6% auto", maxHeight: "80vh", overflowY: "auto" }} onClick={(e) => e.stopPropagation()}>
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", gap: 8 }}>
              <h3 style={{ margin: 0 }}>{x.title}</h3>
              <button className="btn btn-secondary btn-sm" type="button" onClick={() => setOpen(false)}>{x.close}</button>
            </div>
            <input style={{ width: "100%", margin: "12px 0" }} value={query} placeholder={x.search} autoFocus
              onChange={(e) => { setQuery(e.target.value); setTopic(null); }} />
            {topic ? (
              <div>
                <button className="btn btn-secondary btn-sm" type="button" onClick={() => setTopic(null)}>{x.back}</button>
                <h4 style={{ marginBottom: 4 }}>{topic.title[l]}</h4>
                <div className="text-muted" style={{ fontSize: 12, marginBottom: 8 }}>{topic.section[l]}</div>
                <ol style={{ lineHeight: 1.9, paddingInlineStart: 22 }}>{topic.steps[l].map((s, i) => <li key={i}>{s}</li>)}</ol>
                {topic.tips && (
                  <div className="text-muted" style={{ marginTop: 8 }}>
                    <strong>{x.tips}</strong>
                    <ul style={{ lineHeight: 1.8, paddingInlineStart: 22 }}>{topic.tips[l].map((s, i) => <li key={i}>{s}</li>)}</ul>
                  </div>
                )}
              </div>
            ) : results.length === 0 ? (
              <div className="text-muted">{x.none}</div>
            ) : (
              sections.map((sec) => (
                <div key={sec} style={{ marginBottom: 10 }}>
                  <div className="text-muted" style={{ fontSize: 12, fontWeight: 700 }}>{sec}</div>
                  {results.filter((t) => t.section[l] === sec).map((t) => (
                    <button key={t.id} type="button" className="btn btn-secondary btn-sm" style={{ display: "block", width: "100%", textAlign: "start", marginTop: 4 }} onClick={() => setTopic(t)}>
                      {t.title[l]}{t.id === topicForPath(pathname).id && t.id !== HELP_TOPICS[0].id ? ` · ${x.forPage}` : ""}
                    </button>
                  ))}
                </div>
              ))
            )}
          </div>
        </div>
      )}
    </>
  );
}
