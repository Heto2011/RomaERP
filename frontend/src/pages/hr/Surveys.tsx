import { useEffect, useState } from "react";
import { SurveysApi } from "../../api/services";
import { SurveyQuestionType, SurveyStatus, type Survey, type SurveyResults } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";

interface DraftQuestion { text: string; type: SurveyQuestionType }

export default function Surveys() {
  const { t } = useLanguage();
  const [surveys, setSurveys] = useState<Survey[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [showForm, setShowForm] = useState(false);
  const [editing, setEditing] = useState<Survey | null>(null);
  const [title, setTitle] = useState("");
  const [description, setDescription] = useState("");
  const [anonymous, setAnonymous] = useState(true);
  const [questions, setQuestions] = useState<DraftQuestion[]>([{ text: "", type: SurveyQuestionType.Rating }]);
  const [results, setResults] = useState<SurveyResults | null>(null);

  async function load() { setSurveys((await SurveysApi.getAll()).data); }
  useEffect(() => { load().catch((e) => setError(getErrorMessage(e))); }, []);

  function openForm(s: Survey | null) {
    setEditing(s);
    setTitle(s?.title ?? "");
    setDescription(s?.description ?? "");
    setAnonymous(s?.isAnonymous ?? true);
    setQuestions(s ? s.questions.map((q) => ({ text: q.text, type: q.type })) : [{ text: "", type: SurveyQuestionType.Rating }]);
    setShowForm(true);
  }

  async function save(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    try {
      const payload = { title, description: description || null, isAnonymous: anonymous, questions: questions.filter((q) => q.text.trim()).map((q) => ({ id: "", text: q.text, type: q.type })) };
      if (editing) await SurveysApi.update(editing.id, payload as Partial<Survey>);
      else await SurveysApi.create(payload as Partial<Survey>);
      setShowForm(false);
      await load();
    } catch (err) { setError(getErrorMessage(err)); }
  }

  async function setStatus(s: Survey, status: SurveyStatus) {
    setError(null);
    try { await SurveysApi.setStatus(s.id, status); await load(); } catch (err) { setError(getErrorMessage(err)); }
  }
  async function remove(s: Survey) {
    setError(null);
    try { await SurveysApi.remove(s.id); await load(); } catch (err) { setError(getErrorMessage(err)); }
  }
  async function showResults(s: Survey) {
    setError(null);
    try { setResults((await SurveysApi.results(s.id)).data); } catch (err) { setError(getErrorMessage(err)); }
  }

  return (
    <div>
      <div className="page-header">
        <h1>{t.hr.surveysTitle}</h1>
        <button className="btn" onClick={() => (showForm ? setShowForm(false) : openForm(null))}>{showForm ? t.common.cancel : t.hr.surveyNew}</button>
      </div>
      <p className="text-muted">{t.hr.surveysIntro}</p>
      {error && <div className="alert-error">{error}</div>}

      {showForm && (
        <div className="card">
          <form onSubmit={save}>
            <div className="form-field"><label>{t.hr.surveyTitleLabel}</label><input value={title} onChange={(e) => setTitle(e.target.value)} required /></div>
            <div className="form-field"><label>{t.common.description}</label><input value={description} onChange={(e) => setDescription(e.target.value)} /></div>
            <label style={{ display: "flex", gap: 8, alignItems: "center", fontWeight: "normal" }}><input type="checkbox" checked={anonymous} onChange={(e) => setAnonymous(e.target.checked)} />{t.hr.surveyAnonymous}</label>
            <h4>{t.hr.surveyQuestions}</h4>
            {questions.map((q, i) => (
              <div key={i} style={{ display: "flex", gap: 8, marginBottom: 8 }}>
                <input style={{ flex: 1 }} placeholder={t.hr.surveyQuestionText} value={q.text} onChange={(e) => setQuestions((l) => l.map((x, j) => (j === i ? { ...x, text: e.target.value } : x)))} />
                <select value={q.type} onChange={(e) => setQuestions((l) => l.map((x, j) => (j === i ? { ...x, type: Number(e.target.value) as SurveyQuestionType } : x)))}>
                  {[1, 2, 3].map((v) => <option key={v} value={v}>{t.hr.surveyTypes[v]}</option>)}
                </select>
                {questions.length > 1 && <button type="button" className="btn btn-secondary btn-sm" onClick={() => setQuestions((l) => l.filter((_, j) => j !== i))}>✕</button>}
              </div>
            ))}
            <button type="button" className="btn btn-secondary btn-sm" onClick={() => setQuestions((l) => [...l, { text: "", type: SurveyQuestionType.Rating }])}>{t.hr.surveyAddQuestion}</button>
            <div style={{ marginTop: 12 }}><button className="btn" type="submit">{t.common.save}</button></div>
          </form>
        </div>
      )}

      <div className="card" style={{ overflowX: "auto" }}>
        <table>
          <tbody>
            {surveys.length === 0 && <tr><td className="text-muted">{t.common.noData}</td></tr>}
            {surveys.map((s) => (
              <tr key={s.id}>
                <td><strong>{s.title}</strong><div className="text-muted">{s.questions.length} · {s.isAnonymous ? t.hr.surveyAnonymous : ""}</div></td>
                <td>{t.hr.surveyStatuses[s.status]}</td>
                <td>{s.responseCount} {t.hr.surveyResponses}</td>
                <td style={{ whiteSpace: "nowrap" }}>
                  {s.status === SurveyStatus.Draft && <><button className="btn btn-secondary btn-sm" onClick={() => openForm(s)}>{t.common.edit}</button>{" "}<button className="btn btn-secondary btn-sm" onClick={() => setStatus(s, SurveyStatus.Open)}>{t.hr.surveyOpen}</button>{" "}<button className="btn btn-secondary btn-sm" onClick={() => remove(s)}>{t.common.delete}</button></>}
                  {s.status === SurveyStatus.Open && <button className="btn btn-secondary btn-sm" onClick={() => setStatus(s, SurveyStatus.Closed)}>{t.hr.surveyClose}</button>}{" "}
                  {s.status !== SurveyStatus.Draft && <button className="btn btn-sm" onClick={() => showResults(s)}>{t.hr.surveyResultsBtn}</button>}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {results && (
        <div className="modal-overlay" onClick={() => setResults(null)}>
          <div className="card" style={{ maxWidth: 720, margin: "4% auto", maxHeight: "88vh", overflowY: "auto" }} onClick={(e) => e.stopPropagation()}>
            <h3>{results.title}</h3>
            <p className="text-muted">{results.responseCount} {t.hr.surveyInvited} {results.invitedCount}</p>
            {results.textHidden && <div className="alert-error">{t.hr.surveyTextHidden}</div>}
            {results.questions.map((q) => (
              <div key={q.questionId} style={{ marginBottom: 14 }}>
                <strong>{q.text}</strong>
                {q.type === SurveyQuestionType.Rating && <div>Ø {q.average ?? "—"} · {[1, 2, 3, 4, 5].map((n) => `${n}: ${q.distribution[n] ?? 0}`).join("  ")}</div>}
                {q.type === SurveyQuestionType.YesNo && <div>{t.hr.surveyYes}: {q.distribution[1] ?? 0} · {t.hr.surveyNo}: {q.distribution[0] ?? 0}</div>}
                {q.type === SurveyQuestionType.Text && q.texts.map((x, i) => <div key={i} className="text-muted">— {x}</div>)}
              </div>
            ))}
            <button className="btn btn-secondary" onClick={() => setResults(null)}>{t.common.cancel}</button>
          </div>
        </div>
      )}
    </div>
  );
}
