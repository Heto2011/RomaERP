import { useEffect, useState } from "react";
import { EmployeesApi, PerformanceApi } from "../../api/services";
import { ReviewCycleStatus, ReviewRelationship, type Employee, type ReviewAssignment, type ReviewCycle, type ReviewSubjectResult } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";

const dateIn = (iso: string) => iso.slice(0, 10);

export default function Performance() {
  const { t, lang } = useLanguage();
  const [cycles, setCycles] = useState<ReviewCycle[]>([]);
  const [employees, setEmployees] = useState<Employee[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  const [showForm, setShowForm] = useState(false);
  const [editing, setEditing] = useState<ReviewCycle | null>(null);
  const [name, setName] = useState("");
  const [start, setStart] = useState("");
  const [end, setEnd] = useState("");
  const [competencies, setCompetencies] = useState("");

  const [selected, setSelected] = useState<ReviewCycle | null>(null);
  const [assignments, setAssignments] = useState<ReviewAssignment[]>([]);
  const [results, setResults] = useState<ReviewSubjectResult[]>([]);
  const [subjectId, setSubjectId] = useState("");
  const [reviewers, setReviewers] = useState<{ id: string; rel: ReviewRelationship }[]>([]);

  const empName = (id: string) => { const e = employees.find((x) => x.id === id); return e ? (lang === "ar" ? e.fullNameAr : e.fullNameEn || e.fullNameAr) : ""; };
  const active = employees.filter((e) => e.employmentStatus === 1);

  async function loadCycles() { setCycles((await PerformanceApi.getCycles()).data); }
  async function loadSelected(c: ReviewCycle) {
    setSelected(c);
    setAssignments((await PerformanceApi.getAssignments(c.id)).data);
    setResults((await PerformanceApi.getResults(c.id)).data);
  }

  useEffect(() => {
    loadCycles().catch((e) => setError(getErrorMessage(e)));
    EmployeesApi.getAll().then((r) => setEmployees(r.data)).catch(() => {});
  }, []);

  function openForm(c: ReviewCycle | null) {
    setEditing(c);
    setName(c?.name ?? "");
    setStart(c ? dateIn(c.startDate) : new Date().toISOString().slice(0, 10));
    setEnd(c ? dateIn(c.endDate) : new Date(Date.now() + 30 * 86400000).toISOString().slice(0, 10));
    setCompetencies(c ? c.competencies.join("\n") : "");
    setShowForm(true);
  }

  async function saveCycle(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    try {
      const payload = { name, startDate: start, endDate: end, competencies: competencies.split("\n").map((x) => x.trim()).filter(Boolean) };
      const res = editing ? await PerformanceApi.updateCycle(editing.id, payload) : await PerformanceApi.createCycle(payload);
      setShowForm(false);
      await loadCycles();
      await loadSelected(res.data);
    } catch (err) { setError(getErrorMessage(err)); }
  }

  async function setStatus(c: ReviewCycle, status: ReviewCycleStatus) {
    setError(null);
    try {
      const res = await PerformanceApi.setStatus(c.id, status);
      await loadCycles();
      await loadSelected(res.data);
    } catch (err) { setError(getErrorMessage(err)); }
  }

  async function addReviewers() {
    if (!selected || !subjectId || reviewers.length === 0) return;
    setError(null);
    try {
      const n = (await PerformanceApi.addAssignments(selected.id, subjectId, reviewers.map((r) => ({ reviewerEmployeeId: r.id, relationship: r.rel })))).data;
      setMessage(t.hr.cycleAdded.replace("{n}", String(n)));
      setReviewers([]);
      await loadCycles();
      await loadSelected(selected);
    } catch (err) { setError(getErrorMessage(err)); }
  }

  async function selfAll() {
    if (!selected) return;
    setError(null);
    try {
      const n = (await PerformanceApi.addSelfReviews(selected.id)).data;
      setMessage(t.hr.cycleAdded.replace("{n}", String(n)));
      await loadCycles();
      await loadSelected(selected);
    } catch (err) { setError(getErrorMessage(err)); }
  }

  async function removeAssignment(a: ReviewAssignment) {
    setError(null);
    try { await PerformanceApi.deleteAssignment(a.id); if (selected) await loadSelected(selected); await loadCycles(); } catch (err) { setError(getErrorMessage(err)); }
  }

  const canEditPeople = selected && selected.status !== ReviewCycleStatus.Closed;

  return (
    <div>
      <div className="page-header">
        <h1>{t.hr.performanceTitle}</h1>
        <button className="btn" onClick={() => (showForm ? setShowForm(false) : openForm(null))}>{showForm ? t.common.cancel : t.hr.cycleNew}</button>
      </div>
      <p className="text-muted">{t.hr.performanceIntro}</p>
      {error && <div className="alert-error">{error}</div>}
      {message && <div className="alert-success">{message}</div>}

      {showForm && (
        <div className="card">
          <form onSubmit={saveCycle}>
            <div className="form-grid">
              <div className="form-field"><label>{t.hr.cycleName}</label><input value={name} onChange={(e) => setName(e.target.value)} required /></div>
              <div className="form-field"><label>{t.hr.cycleStart}</label><input type="date" value={start} onChange={(e) => setStart(e.target.value)} required /></div>
              <div className="form-field"><label>{t.hr.cycleEnd}</label><input type="date" value={end} onChange={(e) => setEnd(e.target.value)} required /></div>
            </div>
            <div className="form-field"><label>{t.hr.cycleCompetencies}</label><textarea rows={5} value={competencies} onChange={(e) => setCompetencies(e.target.value)} /></div>
            <button className="btn" type="submit">{t.common.save}</button>
          </form>
        </div>
      )}

      <div className="card" style={{ overflowX: "auto" }}>
        <table>
          <tbody>
            {cycles.length === 0 && <tr><td className="text-muted">{t.common.noData}</td></tr>}
            {cycles.map((c) => (
              <tr key={c.id} style={{ cursor: "pointer", background: selected?.id === c.id ? "var(--row-active, rgba(0,0,0,.04))" : undefined }} onClick={() => loadSelected(c).catch((e) => setError(getErrorMessage(e)))}>
                <td><strong>{c.name}</strong><div className="text-muted">{dateIn(c.startDate)} → {dateIn(c.endDate)}</div></td>
                <td>{t.hr.cycleStatuses[c.status]}</td>
                <td>{c.submittedReviews}/{c.totalReviews} {t.hr.cycleProgress}</td>
                <td style={{ whiteSpace: "nowrap" }} onClick={(e) => e.stopPropagation()}>
                  {c.status === ReviewCycleStatus.Draft && <button className="btn btn-secondary btn-sm" onClick={() => openForm(c)}>{t.common.edit}</button>}{" "}
                  {c.status === ReviewCycleStatus.Draft && <button className="btn btn-secondary btn-sm" onClick={() => setStatus(c, ReviewCycleStatus.Active)}>{t.hr.cycleStart2}</button>}{" "}
                  {c.status === ReviewCycleStatus.Active && <button className="btn btn-secondary btn-sm" onClick={() => setStatus(c, ReviewCycleStatus.Closed)}>{t.hr.cycleClose}</button>}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {selected && (
        <>
          <h3>{selected.name}</h3>
          <p className="text-muted">{selected.competencies.join(" · ")}</p>

          {canEditPeople && (
            <div className="card">
              <h4 style={{ marginTop: 0 }}>{t.hr.cycleAddReviewers}</h4>
              <div style={{ display: "flex", gap: 8, flexWrap: "wrap", alignItems: "flex-end" }}>
                <div className="form-field">
                  <label>{t.hr.cycleSubject}</label>
                  <select value={subjectId} onChange={(e) => setSubjectId(e.target.value)}>
                    <option value="">—</option>
                    {active.map((e) => <option key={e.id} value={e.id}>{empName(e.id)}</option>)}
                  </select>
                </div>
                <button type="button" className="btn btn-secondary btn-sm" onClick={() => setReviewers((r) => [...r, { id: "", rel: ReviewRelationship.Manager }])}>+ {t.hr.cycleReviewer}</button>
                <button type="button" className="btn btn-secondary btn-sm" onClick={selfAll}>{t.hr.cycleSelfAll}</button>
              </div>
              {reviewers.map((r, i) => (
                <div key={i} style={{ display: "flex", gap: 8, marginTop: 8 }}>
                  <select value={r.id} onChange={(e) => setReviewers((list) => list.map((x, j) => (j === i ? { ...x, id: e.target.value } : x)))}>
                    <option value="">—</option>
                    {active.map((e) => <option key={e.id} value={e.id}>{empName(e.id)}</option>)}
                  </select>
                  <select value={r.rel} onChange={(e) => setReviewers((list) => list.map((x, j) => (j === i ? { ...x, rel: Number(e.target.value) as ReviewRelationship } : x)))}>
                    {[1, 2, 3, 4].map((v) => <option key={v} value={v}>{t.hr.cycleRelationships[v]}</option>)}
                  </select>
                  <button type="button" className="btn btn-secondary btn-sm" onClick={() => setReviewers((list) => list.filter((_, j) => j !== i))}>✕</button>
                </div>
              ))}
              {reviewers.length > 0 && <button className="btn" style={{ marginTop: 10 }} onClick={addReviewers} disabled={!subjectId || reviewers.some((r) => !r.id)}>{t.common.save}</button>}
            </div>
          )}

          <div className="card" style={{ overflowX: "auto" }}>
            <h4 style={{ marginTop: 0 }}>{t.hr.cycleAssignments}</h4>
            <table>
              <tbody>
                {assignments.length === 0 && <tr><td className="text-muted">{t.common.noData}</td></tr>}
                {assignments.map((a) => (
                  <tr key={a.id}>
                    <td>{a.subjectName}</td>
                    <td>{t.hr.cycleRelationships[a.relationship]}: {a.reviewerName}</td>
                    <td>{a.isSubmitted ? `✓ ${t.hr.cycleSubmitted}` : t.hr.cyclePending}</td>
                    <td>{canEditPeople && !a.isSubmitted && <button className="btn btn-secondary btn-sm" onClick={() => removeAssignment(a)}>{t.common.delete}</button>}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <div className="card">
            <h4 style={{ marginTop: 0 }}>{t.hr.cycleResults}</h4>
            {results.length === 0 && <div className="text-muted">{t.hr.cycleNoResults}</div>}
            {results.map((r) => (
              <div key={r.employeeId} style={{ marginBottom: 16, paddingBottom: 12, borderBottom: "1px solid var(--border-color, #eee)" }}>
                <strong>{r.employeeName}</strong>{" "}
                <span className="text-muted">{t.hr.cycleOthersAvg}: <b>{r.othersAverage ?? "—"}</b> · {t.hr.cycleSelfAvg}: <b>{r.selfAverage ?? "—"}</b></span>
                <table style={{ marginTop: 6 }}>
                  <thead><tr><th></th>{selected.competencies.map((c) => <th key={c}>{c}</th>)}<th>Ø</th></tr></thead>
                  <tbody>
                    {r.groups.map((g) => (
                      <tr key={g.relationship}>
                        <td>{t.hr.cycleRelationships[g.relationship]} ({g.count})</td>
                        {selected.competencies.map((c) => <td key={c}>{g.byCompetency[c] ?? "—"}</td>)}
                        <td><strong>{g.average ?? "—"}</strong></td>
                      </tr>
                    ))}
                  </tbody>
                </table>
                {r.groups.filter((g) => g.comments.length > 0).map((g) => (
                  <div key={g.relationship} className="text-muted" style={{ marginTop: 4 }}>
                    <b>{t.hr.cycleRelationships[g.relationship]}:</b> {g.comments.join(" — ")}
                  </div>
                ))}
              </div>
            ))}
          </div>
        </>
      )}
    </div>
  );
}
