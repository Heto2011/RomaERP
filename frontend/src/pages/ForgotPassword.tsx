import { useState, type FormEvent } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { AuthApi } from "../api/services";
import { getErrorMessage } from "../api/client";
import { useLanguage } from "../i18n/LanguageContext";

/// <summary>Step 1 of "forgot my password": asks for the company code and email and always shows the same
/// confirmation, so it can't be used to discover which emails have accounts.</summary>
export default function ForgotPassword() {
  const { t, lang, setLang } = useLanguage();
  const [params] = useSearchParams();
  const people = params.get("p") === "people";
  const [companyCode, setCompanyCode] = useState(() => localStorage.getItem("companyCode") ?? "");
  const [email, setEmail] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [sent, setSent] = useState(false);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setLoading(true);
    try {
      await AuthApi.forgotPassword(companyCode.trim().toLowerCase(), email.trim());
      setSent(true);
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className={`login-page${people ? " people-theme" : ""}`}>
      <div className="login-card">
        <div style={{ display: "flex", justifyContent: "flex-end" }}>
          <button className="btn btn-secondary btn-sm" onClick={() => setLang(lang === "ar" ? "en" : "ar")}>
            {t.language}
          </button>
        </div>
        <h1>{t.recovery.forgotTitle}</h1>
        {sent ? (
          <>
            <h3>{t.recovery.sentTitle}</h3>
            <p>{t.recovery.sentBody}</p>
          </>
        ) : (
          <>
            <p>{t.recovery.forgotIntro}</p>
            {error && <div className="alert-error">{error}</div>}
            <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: 14 }}>
              <div className="form-field">
                <label>{t.login.companyCode}</label>
                <input type="text" value={companyCode} onChange={(e) => setCompanyCode(e.target.value)} required />
              </div>
              <div className="form-field">
                <label>{t.login.email}</label>
                <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} required />
              </div>
              <button className="btn" type="submit" disabled={loading}>
                {loading ? t.recovery.sending : t.recovery.send}
              </button>
            </form>
          </>
        )}
        <p style={{ marginTop: 16, fontSize: 13 }}>
          <Link to={people ? "/people-login" : "/login"}>{t.recovery.toLogin}</Link>
        </p>
      </div>
    </div>
  );
}
