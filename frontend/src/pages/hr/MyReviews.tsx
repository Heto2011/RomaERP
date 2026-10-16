import { useEffect, useState } from "react";
import { PerformanceApi } from "../../api/services";
import type { ReviewAssignment } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";

export default function MyReviews() {
  const { t } = useLanguage();
  const [items, setItems] = useState<ReviewAssignment[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [open, setOpen] = useState<ReviewAssignment | null>(null);
  const [ratings, setRatings] = useState<Record<string, number>>({});
  const [comments, setComments] = useState("");
  const [done, setDone] = useState(false);

  async function load() { setItems((await PerformanceApi.mine()).data); }
  useEffect(() => { load().catch((e) => setError(getErrorMessage(e))); }, []);

  async function submit() {
    if (!open) return;
    setError(null);
    try {
      await PerformanceApi.submit(open.id, ratings, comments || null);
      setOpen(null);
      setRatings({});
      setComments("");
      setDone(true);
      await load();
    } catch (err) { setError(getErrorMessage(err)); }
  }

  return (
    <div>
      <div className="page-header"><h1>{t.hr.myReviews}</h1></div>
      {error && <div className="alert-error">{error}</div>}
      {done && <div className="alert-success">{t.hr.reviewDone}</div>}
      {items && items.length === 0 && <div className="card text-muted">{t.hr.myReviewsEmpty}</div>}

      {!open && items && items.length > 0 && (
        <div className="card">
          <table>
            <tbody>
              {items.map((a) => (
                <tr key={a.id}>
                  <td><strong>{a.subjectName}</strong><div className="text-muted">{a.cycleName} · {t.hr.cycleRelationships[a.relationship]}</div></td>
                  <td>{a.isSubmitted ? `✓ ${t.hr.cycleSubmitted}` : <button className="btn btn-sm" onClick={() => { setOpen(a); setRatings({}); setDone(false); }}>{t.hr.reviewAbout}</button>}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {open && (
        <div className="card">
          <h3 style={{ marginTop: 0 }}>{t.hr.reviewAbout} {open.subjectName}</h3>
          <p className="text-muted">{t.hr.reviewScaleHint}</p>
          {open.competencies.map((c) => (
            <div key={c} style={{ display: "flex", gap: 12, alignItems: "center", padding: "6px 0", borderBottom: "1px solid var(--border-color, #eee)" }}>
              <span style={{ flex: 1 }}>{c}</span>
              {[1, 2, 3, 4, 5].map((n) => (
                <button key={n} type="button" className={"btn btn-sm " + (ratings[c] === n ? "" : "btn-secondary")} onClick={() => setRatings((r) => ({ ...r, [c]: n }))}>{n}</button>
              ))}
            </div>
          ))}
          <div className="form-field" style={{ marginTop: 12 }}><label>{t.hr.reviewComments}</label><textarea rows={3} value={comments} onChange={(e) => setComments(e.target.value)} /></div>
          <div style={{ display: "flex", gap: 10 }}>
            <button className="btn" onClick={submit} disabled={open.competencies.some((c) => !ratings[c])}>{t.hr.reviewSubmit}</button>
            <button className="btn btn-secondary" onClick={() => setOpen(null)}>{t.common.cancel}</button>
          </div>
        </div>
      )}
    </div>
  );
}
