import { useEffect, useState } from "react";
import { DepartmentsApi, RecruitmentApi } from "../../api/services";
import { CandidateStage, JobStatus, type Candidate, type Department, type JobPosting } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";

export default function Recruitment() {
  const { t, lang } = useLanguage();
  const [tab, setTab] = useState<"jobs" | "candidates">("jobs");
  const [jobs, setJobs] = useState<JobPosting[]>([]);
  const [departments, setDepartments] = useState<Department[]>([]);
  const [candidates, setCandidates] = useState<Candidate[]>([]);
  const [filterJob, setFilterJob] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  // job form
  const [showJobForm, setShowJobForm] = useState(false);
  const [editingJob, setEditingJob] = useState<JobPosting | null>(null);
  const [title, setTitle] = useState("");
  const [departmentId, setDepartmentId] = useState("");
  const [location, setLocation] = useState("");
  const [description, setDescription] = useState("");
  const [requirements, setRequirements] = useState("");
  const [status, setStatus] = useState<JobStatus>(JobStatus.Draft);
  const [closingDate, setClosingDate] = useState("");

  // candidate form + detail
  const [showCandForm, setShowCandForm] = useState(false);
  const [cJob, setCJob] = useState("");
  const [cName, setCName] = useState("");
  const [cEmail, setCEmail] = useState("");
  const [cPhone, setCPhone] = useState("");
  const [cResume, setCResume] = useState("");
  const [detail, setDetail] = useState<Candidate | null>(null);
  const [dStage, setDStage] = useState<CandidateStage>(CandidateStage.Applied);
  const [dNotes, setDNotes] = useState("");
  const [dInterview, setDInterview] = useState("");
  const [screening, setScreening] = useState(false);

  const careersLink = `${window.location.origin}/careers/${localStorage.getItem("companyCode") ?? ""}`;

  async function loadJobs() { setJobs((await RecruitmentApi.getJobs()).data); }
  async function loadCandidates() { setCandidates((await RecruitmentApi.getCandidates(filterJob || undefined)).data); }

  useEffect(() => {
    loadJobs().catch((e) => setError(getErrorMessage(e)));
    DepartmentsApi.getAll().then((r) => setDepartments(r.data)).catch(() => {});
  }, []);
  useEffect(() => {
    loadCandidates().catch((e) => setError(getErrorMessage(e)));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [filterJob]);

  function openJobForm(job: JobPosting | null) {
    setEditingJob(job);
    setTitle(job?.title ?? "");
    setDepartmentId(job?.departmentId ?? "");
    setLocation(job?.location ?? "");
    setDescription(job?.description ?? "");
    setRequirements(job?.requirements ?? "");
    setStatus(job?.status ?? JobStatus.Draft);
    setClosingDate(job?.closingDate?.slice(0, 10) ?? "");
    setShowJobForm(true);
  }

  async function saveJob(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    try {
      const payload = { title, departmentId: departmentId || null, location: location || null, description, requirements: requirements || null, status, closingDate: closingDate || null };
      if (editingJob) await RecruitmentApi.updateJob(editingJob.id, payload);
      else await RecruitmentApi.createJob(payload);
      setShowJobForm(false);
      await loadJobs();
    } catch (err) { setError(getErrorMessage(err)); }
  }

  async function deleteJob(job: JobPosting) {
    setError(null);
    try { await RecruitmentApi.deleteJob(job.id); await loadJobs(); } catch (err) { setError(getErrorMessage(err)); }
  }

  async function copyLink() {
    try { await navigator.clipboard.writeText(careersLink); setMessage(t.hr.jobLinkCopied); } catch { setMessage(careersLink); }
  }

  async function addCandidate(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    try {
      await RecruitmentApi.addCandidate({ jobPostingId: cJob, fullName: cName, email: cEmail, phone: cPhone || null, resumeText: cResume });
      setShowCandForm(false);
      setCName(""); setCEmail(""); setCPhone(""); setCResume("");
      await loadCandidates();
      await loadJobs();
    } catch (err) { setError(getErrorMessage(err)); }
  }

  function openDetail(c: Candidate) {
    setDetail(c);
    setDStage(c.stage);
    setDNotes(c.notes ?? "");
    setDInterview(c.interviewAtUtc ? c.interviewAtUtc.slice(0, 16) : "");
  }

  async function saveDetail() {
    if (!detail) return;
    setError(null);
    try {
      const res = await RecruitmentApi.updateCandidate(detail.id, { stage: dStage, notes: dNotes || null, interviewAtUtc: dInterview ? new Date(dInterview).toISOString() : null });
      setDetail(res.data);
      await loadCandidates();
    } catch (err) { setError(getErrorMessage(err)); }
  }

  async function screen() {
    if (!detail) return;
    setError(null);
    setScreening(true);
    try {
      const res = await RecruitmentApi.screen(detail.id);
      setDetail(res.data);
      await loadCandidates();
    } catch (err) { setError(getErrorMessage(err)); } finally { setScreening(false); }
  }

  async function deleteCandidate() {
    if (!detail) return;
    try { await RecruitmentApi.deleteCandidate(detail.id); setDetail(null); await loadCandidates(); await loadJobs(); } catch (err) { setError(getErrorMessage(err)); }
  }

  const scoreColor = (s: number | null) => (s == null ? undefined : s >= 75 ? "var(--success, #1b7f4b)" : s >= 50 ? "var(--warning, #b7791f)" : "var(--danger, #c0392b)");
  const fmt = (iso: string) => new Date(iso).toLocaleDateString(lang === "ar" ? "ar-EG" : "en-GB");

  return (
    <div>
      <div className="page-header"><h1>{t.hr.recruitmentTitle}</h1></div>
      <p className="text-muted">{t.hr.recruitmentIntro}</p>
      {error && <div className="alert-error">{error}</div>}
      {message && <div className="alert-success">{message}</div>}

      <div style={{ display: "flex", gap: 8, marginBottom: 12 }}>
        <button className={"btn " + (tab === "jobs" ? "" : "btn-secondary")} onClick={() => setTab("jobs")}>{t.hr.jobsTab}</button>
        <button className={"btn " + (tab === "candidates" ? "" : "btn-secondary")} onClick={() => setTab("candidates")}>{t.hr.candidatesTab}</button>
      </div>

      {tab === "jobs" && (
        <>
          <div className="card" style={{ display: "flex", gap: 10, flexWrap: "wrap", alignItems: "center" }}>
            <span className="text-muted">{t.hr.careersLinkHint}</span>
            <code dir="ltr">{careersLink}</code>
            <button className="btn btn-secondary btn-sm" onClick={copyLink}>{t.hr.jobCopyLink}</button>
            <button className="btn" style={{ marginInlineStart: "auto" }} onClick={() => (showJobForm ? setShowJobForm(false) : openJobForm(null))}>{showJobForm ? t.common.cancel : t.hr.jobNew}</button>
          </div>

          {showJobForm && (
            <div className="card">
              <form onSubmit={saveJob}>
                <div className="form-grid">
                  <div className="form-field"><label>{t.hr.jobPositionTitle}</label><input value={title} onChange={(e) => setTitle(e.target.value)} required /></div>
                  <div className="form-field">
                    <label>{t.nav.departments}</label>
                    <select value={departmentId} onChange={(e) => setDepartmentId(e.target.value)}>
                      <option value="">—</option>
                      {departments.map((d) => <option key={d.id} value={d.id}>{lang === "ar" ? d.nameAr : d.nameEn || d.nameAr}</option>)}
                    </select>
                  </div>
                  <div className="form-field"><label>{t.hr.jobLocation}</label><input value={location} onChange={(e) => setLocation(e.target.value)} /></div>
                  <div className="form-field">
                    <label>{t.hr.jobStatus}</label>
                    <select value={status} onChange={(e) => setStatus(Number(e.target.value) as JobStatus)}>
                      {[JobStatus.Draft, JobStatus.Open, JobStatus.Closed].map((s) => <option key={s} value={s}>{t.hr.jobStatuses[s]}</option>)}
                    </select>
                  </div>
                  <div className="form-field"><label>{t.hr.jobClosing}</label><input type="date" value={closingDate} onChange={(e) => setClosingDate(e.target.value)} /></div>
                </div>
                <div className="form-field"><label>{t.hr.jobDescription}</label><textarea rows={5} value={description} onChange={(e) => setDescription(e.target.value)} required /></div>
                <div className="form-field"><label>{t.hr.jobRequirements}</label><textarea rows={3} value={requirements} onChange={(e) => setRequirements(e.target.value)} /></div>
                <button className="btn" type="submit">{t.common.save}</button>
              </form>
            </div>
          )}

          <div className="card" style={{ overflowX: "auto" }}>
            <table>
              <tbody>
                {jobs.length === 0 && <tr><td className="text-muted">{t.common.noData}</td></tr>}
                {jobs.map((j) => (
                  <tr key={j.id}>
                    <td><strong>{j.title}</strong><div className="text-muted">{[j.departmentName, j.location].filter(Boolean).join(" · ")}</div></td>
                    <td>{t.hr.jobStatuses[j.status]}</td>
                    <td>{j.candidateCount} {t.hr.jobCandidates}</td>
                    <td style={{ whiteSpace: "nowrap" }}>
                      <button className="btn btn-secondary btn-sm" onClick={() => { setFilterJob(j.id); setTab("candidates"); }}>{t.hr.candidatesTab}</button>{" "}
                      <button className="btn btn-secondary btn-sm" onClick={() => openJobForm(j)}>{t.common.edit}</button>{" "}
                      <button className="btn btn-secondary btn-sm" onClick={() => deleteJob(j)}>{t.common.delete}</button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      )}

      {tab === "candidates" && (
        <>
          <div className="card toolbar" style={{ display: "flex", gap: 10, flexWrap: "wrap", alignItems: "center" }}>
            <select value={filterJob} onChange={(e) => setFilterJob(e.target.value)}>
              <option value="">{t.hr.candidateAllJobs}</option>
              {jobs.map((j) => <option key={j.id} value={j.id}>{j.title}</option>)}
            </select>
            <button className="btn" style={{ marginInlineStart: "auto" }} onClick={() => { setCJob(filterJob || jobs[0]?.id || ""); setShowCandForm((v) => !v); }}>{showCandForm ? t.common.cancel : t.hr.candidateAdd}</button>
          </div>

          {showCandForm && (
            <div className="card">
              <form onSubmit={addCandidate}>
                <div className="form-grid">
                  <div className="form-field">
                    <label>{t.hr.jobPositionTitle}</label>
                    <select value={cJob} onChange={(e) => setCJob(e.target.value)} required>
                      {jobs.map((j) => <option key={j.id} value={j.id}>{j.title}</option>)}
                    </select>
                  </div>
                  <div className="form-field"><label>{t.hr.candidateName}</label><input value={cName} onChange={(e) => setCName(e.target.value)} required /></div>
                  <div className="form-field"><label>{t.hr.careersEmail}</label><input type="email" dir="ltr" value={cEmail} onChange={(e) => setCEmail(e.target.value)} required /></div>
                  <div className="form-field"><label>{t.hr.careersPhone}</label><input dir="ltr" value={cPhone} onChange={(e) => setCPhone(e.target.value)} /></div>
                </div>
                <div className="form-field"><label>{t.hr.candidateResume}</label><textarea rows={6} value={cResume} onChange={(e) => setCResume(e.target.value)} required /></div>
                <button className="btn" type="submit">{t.common.save}</button>
              </form>
            </div>
          )}

          <div className="card" style={{ overflowX: "auto" }}>
            <table>
              <thead><tr><th>{t.hr.candidateName}</th><th>{t.hr.jobPositionTitle}</th><th>{t.hr.candidateStage}</th><th>{t.hr.candidateAiScore}</th><th>{t.hr.candidateSource}</th><th></th></tr></thead>
              <tbody>
                {candidates.length === 0 && <tr><td colSpan={6} className="text-muted">{t.hr.candidateNone}</td></tr>}
                {candidates.map((c) => (
                  <tr key={c.id} style={{ cursor: "pointer" }} onClick={() => openDetail(c)}>
                    <td><strong>{c.fullName}</strong><div className="text-muted" dir="ltr">{c.email}</div></td>
                    <td>{c.jobTitle}</td>
                    <td>{t.hr.candidateStages[c.stage]}</td>
                    <td style={{ color: scoreColor(c.aiScore), fontWeight: 700 }}>{c.aiScore ?? "—"}</td>
                    <td className="text-muted">{c.source} · {fmt(c.appliedAtUtc)}</td>
                    <td><button className="btn btn-secondary btn-sm">{t.common.edit}</button></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      )}

      {detail && (
        <div className="modal-overlay" onClick={() => setDetail(null)}>
          <div className="card" style={{ maxWidth: 720, margin: "4% auto", maxHeight: "88vh", overflowY: "auto" }} onClick={(e) => e.stopPropagation()}>
            <h3>{detail.fullName} — {detail.jobTitle}</h3>
            <p className="text-muted" dir="ltr">{detail.email} {detail.phone ? `· ${detail.phone}` : ""}</p>

            <div style={{ display: "flex", gap: 10, alignItems: "center", flexWrap: "wrap" }}>
              <button className="btn btn-secondary" onClick={screen} disabled={screening}>{screening ? t.common.loading : t.hr.candidateAiScreen}</button>
              {detail.aiScore != null && <strong style={{ color: scoreColor(detail.aiScore) }}>{t.hr.candidateAiScore}: {detail.aiScore}/100</strong>}
            </div>
            {detail.aiSummary && (
              <div className="card" style={{ marginTop: 10 }}>
                <div style={{ whiteSpace: "pre-wrap" }}>{detail.aiSummary}</div>
                <div className="text-muted" style={{ marginTop: 6 }}>{t.hr.candidateAiNote}</div>
              </div>
            )}

            <div className="form-grid" style={{ marginTop: 12 }}>
              <div className="form-field">
                <label>{t.hr.candidateStage}</label>
                <select value={dStage} onChange={(e) => setDStage(Number(e.target.value) as CandidateStage)}>
                  {[1, 2, 3, 4, 5, 6].map((s) => <option key={s} value={s}>{t.hr.candidateStages[s]}</option>)}
                </select>
              </div>
              <div className="form-field"><label>{t.hr.candidateInterview}</label><input type="datetime-local" value={dInterview} onChange={(e) => setDInterview(e.target.value)} /></div>
            </div>
            <div className="form-field"><label>{t.hr.candidateNotes}</label><textarea rows={3} value={dNotes} onChange={(e) => setDNotes(e.target.value)} /></div>

            {detail.coverNote && <><h4>{t.hr.candidateCover}</h4><p style={{ whiteSpace: "pre-wrap" }}>{detail.coverNote}</p></>}
            <h4>{t.hr.candidateResume}</h4>
            <div style={{ whiteSpace: "pre-wrap", maxHeight: 240, overflowY: "auto", border: "1px solid var(--border-color, #ddd)", padding: 10, borderRadius: 6 }}>{detail.resumeText}</div>

            <div style={{ display: "flex", gap: 10, marginTop: 14, flexWrap: "wrap" }}>
              <button className="btn" onClick={saveDetail}>{t.common.save}</button>
              <button className="btn btn-secondary" onClick={() => setDetail(null)}>{t.common.cancel}</button>
              <button className="btn btn-secondary" style={{ marginInlineStart: "auto" }} onClick={deleteCandidate}>{t.common.delete}</button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
