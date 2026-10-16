import { useEffect, useState } from "react";
import { SurveysApi } from "../../api/services";
import { SurveyQuestionType, type Survey } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";

export default function MySurveys() {
  const { t } = useLanguage();
  const [items, setItems] = useState<Survey[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [open, setOpen] = useState<Survey | null>(null);
  const [numbers, setNumbers] = useState<Record<string, number>>({});
  const [texts, setTexts] = useState<Record<string, string>>({});
  const [thanks, setThanks] = useState(false);

  async function load() { setItems((await SurveysApi.open()).data); }
  useEffect(() => { load().catch((e) => setError(getErrorMessage(e))); }, []);

  async function send() {
    if (!open) return;
    setError(null);
    try {
      await SurveysApi.respond(open.id, open.questions.map((q) => ({ questionId: q.id, numberValue: numbers[q.id] ?? null, textValue: texts[q.id] ?? null })));
      setOpen(null); setNumbers({}); setTexts({}); setThanks(true);
      await load();
    } catch (err) { setError(getErrorMessage(err)); }
  }

  const ready = open?.questions.every((q) => q.type === SurveyQuestionType.Text || numbers[q.id] !== undefined);

  return (
    <div>
      <div className="page-header"><h1>{t.hr.mySurveys}</h1></div>
      {error && <div className="alert-error">{error}</div>}
      {thanks && <div className="alert-success">{t.hr.surveyThanks}</div>}
      {items && items.length === 0 && <div className="card text-muted">{t.hr.mySurveysEmpty}</div>}

      {!open && items?.map((s) => (
        <div key={s.id} className="card" style={{ marginBottom: 10, display: "flex", alignItems: "center", gap: 12 }}>
          <div style={{ flex: 1 }}><strong>{s.title}</strong>{s.description && <div className="text-muted">{s.description}</div>}{s.isAnonymous && <div className="text-muted">{t.hr.surveyAnonymous}</div>}</div>
          {s.answered ? <span className="text-success">{t.hr.surveyAnsweredLabel}</span> : <button className="btn btn-sm" onClick={() => { setOpen(s); setThanks(false); }}>{t.hr.surveySend}</button>}
        </div>
      ))}

      {open && (
        <div className="card">
          <h3 style={{ marginTop: 0 }}>{open.title}</h3>
          {open.questions.map((q) => (
            <div key={q.id} style={{ padding: "8px 0", borderBottom: "1px solid var(--border-color, #eee)" }}>
              <div style={{ marginBottom: 6 }}>{q.text}</div>
              {q.type === SurveyQuestionType.Rating && [1, 2, 3, 4, 5].map((n) => (
                <button key={n} type="button" className={"btn btn-sm " + (numbers[q.id] === n ? "" : "btn-secondary")} style={{ marginInlineEnd: 6 }} onClick={() => setNumbers((m) => ({ ...m, [q.id]: n }))}>{n}</button>
              ))}
              {q.type === SurveyQuestionType.YesNo && [1, 0].map((n) => (
                <button key={n} type="button" className={"btn btn-sm " + (numbers[q.id] === n ? "" : "btn-secondary")} style={{ marginInlineEnd: 6 }} onClick={() => setNumbers((m) => ({ ...m, [q.id]: n }))}>{n === 1 ? t.hr.surveyYes : t.hr.surveyNo}</button>
              ))}
              {q.type === SurveyQuestionType.Text && <textarea rows={2} style={{ width: "100%" }} value={texts[q.id] ?? ""} onChange={(e) => setTexts((m) => ({ ...m, [q.id]: e.target.value }))} />}
            </div>
          ))}
          <div style={{ display: "flex", gap: 10, marginTop: 12 }}>
            <button className="btn" onClick={send} disabled={!ready}>{t.hr.surveySend}</button>
            <button className="btn btn-secondary" onClick={() => setOpen(null)}>{t.common.cancel}</button>
          </div>
        </div>
      )}
    </div>
  );
}
