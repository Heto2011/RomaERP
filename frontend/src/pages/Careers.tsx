import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { CareersApi } from "../api/services";
import type { PublicJob } from "../api/types";
import { getErrorMessage } from "../api/client";
import { useLanguage } from "../i18n/LanguageContext";

/** The company's public careers page — no login. Lists open jobs, or one job with its application form. */
export default function Careers() {
  const { t, lang, setLang } = useLanguage();
  const { companyCode = "", jobId } = useParams();
  const [jobs, setJobs] = useState<PublicJob[] | null>(null);
  const [job, setJob] = useState<PublicJob | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [sent, setSent] = useState(false);

  const [fullName, setFullName] = useState("");
  const [email, setEmail] = useState("");
  const [phone, setPhone] = useState("");
  const [resume, setResume] = useState("");
  const [cover, setCover] = useState("");
  const [website, setWebsite] = useState(""); // hidden trap field
  const [sending, setSending] = useState(false);

  useEffect(() => {
    setError(null);
    if (jobId) CareersApi.getJob(companyCode, jobId).then((r) => setJob(r.data)).catch(() => setError(t.hr.careersClosed));
    else CareersApi.getJobs(companyCode).then((r) => setJobs(r.data)).catch((e) => setError(getErrorMessage(e)));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [companyCode, jobId]);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    if (!jobId) return;
    setError(null);
    setSending(true);
    try {
      await CareersApi.apply(companyCode, jobId, { fullName, email, phone: phone || null, resumeText: resume, coverNote: cover || null, website });
      setSent(true);
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setSending(false);
    }
  }

  return (
    <div style={{ maxWidth: 760, margin: "0 auto", padding: "24px 16px" }}>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 12 }}>
        <h1 style={{ margin: 0 }}>{t.hr.careersTitle}</h1>
        <button className="btn btn-secondary btn-sm" onClick={() => setLang(lang === "ar" ? "en" : "ar")}>{lang === "ar" ? "EN" : "عربي"}</button>
      </div>
      {error && <div className="alert-error">{error}</div>}

      {!jobId && jobs && jobs.length === 0 && <div className="card text-muted">{t.hr.careersNone}</div>}
      {!jobId && jobs?.map((j) => (
        <div key={j.id} className="card" style={{ marginBottom: 12 }}>
          <h3 style={{ marginTop: 0 }}>{j.title}</h3>
          <div className="text-muted">{[j.departmentName, j.location].filter(Boolean).join(" · ")}</div>
          <p style={{ whiteSpace: "pre-wrap" }}>{j.description.length > 280 ? j.description.slice(0, 280) + "…" : j.description}</p>
          <Link className="btn" to={`/careers/${companyCode}/${j.id}`}>{t.hr.careersApply}</Link>
        </div>
      ))}

      {jobId && job && (
        <>
          <Link to={`/careers/${companyCode}`}>← {t.hr.careersBack}</Link>
          <div className="card" style={{ marginTop: 12 }}>
            <h2 style={{ marginTop: 0 }}>{job.title}</h2>
            <div className="text-muted">{[job.departmentName, job.location].filter(Boolean).join(" · ")}</div>
            <p style={{ whiteSpace: "pre-wrap" }}>{job.description}</p>
            {job.requirements && (<><h4>{t.hr.jobRequirements}</h4><p style={{ whiteSpace: "pre-wrap" }}>{job.requirements}</p></>)}
          </div>

          {sent ? (
            <div className="alert-success">{t.hr.careersThanks}</div>
          ) : (
            <form className="card" onSubmit={submit}>
              <h3 style={{ marginTop: 0 }}>{t.hr.careersApplyTitle}</h3>
              <div className="form-grid">
                <div className="form-field"><label>{t.hr.candidateName}</label><input value={fullName} onChange={(e) => setFullName(e.target.value)} required maxLength={200} /></div>
                <div className="form-field"><label>{t.hr.careersEmail}</label><input type="email" dir="ltr" value={email} onChange={(e) => setEmail(e.target.value)} required maxLength={200} /></div>
                <div className="form-field"><label>{t.hr.careersPhone}</label><input dir="ltr" value={phone} onChange={(e) => setPhone(e.target.value)} maxLength={40} /></div>
              </div>
              {/* Real visitors never see this; a bot that fills every field gives itself away. */}
              <input tabIndex={-1} autoComplete="off" aria-hidden="true" value={website} onChange={(e) => setWebsite(e.target.value)} style={{ position: "absolute", insetInlineStart: "-9999px", width: 1, height: 1, opacity: 0 }} />
              <div className="form-field"><label>{t.hr.candidateResume}</label><textarea rows={8} value={resume} onChange={(e) => setResume(e.target.value)} placeholder={t.hr.careersResumeHint} required maxLength={20000} /></div>
              <div className="form-field"><label>{t.hr.candidateCover}</label><textarea rows={3} value={cover} onChange={(e) => setCover(e.target.value)} maxLength={2000} /></div>
              <button className="btn" type="submit" disabled={sending}>{t.hr.careersSend}</button>
            </form>
          )}
        </>
      )}
    </div>
  );
}
