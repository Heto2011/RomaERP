import { useState, type FormEvent } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { AuthApi } from "../api/services";
import { getErrorMessage } from "../api/client";
import { useLanguage } from "../i18n/LanguageContext";
import PasswordInput from "../components/PasswordInput";

/// <summary>Step 2: the page the emailed link opens. The company code, email and one-time token all come from the
/// link itself; the person only chooses the new password.</summary>
export default function ResetPassword() {
  const { t, lang, setLang } = useLanguage();
  const [params] = useSearchParams();
  const companyCode = params.get("c") ?? "";
  const email = params.get("e") ?? "";
  const token = params.get("t") ?? "";
  const people = params.get("p") === "people";
  const [password, setPassword] = useState("");
  const [confirm, setConfirm] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [done, setDone] = useState(false);
  const linkOk = companyCode && email && token;

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    if (password !== confirm) {
      setError(t.recovery.mismatch);
      return;
    }
    setLoading(true);
    try {
      await AuthApi.resetPassword(companyCode, email, token, password);
      setDone(true);
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
        <h1>{t.recovery.resetTitle}</h1>
        {done ? (
          <>
            <p>{t.recovery.done}</p>
            <Link className="btn" to={people ? "/people-login" : "/login"}>{t.recovery.toLogin}</Link>
          </>
        ) : !linkOk ? (
          <>
            <div className="alert-error">{t.recovery.badLink}</div>
            <Link to={people ? "/forgot-password?p=people" : "/forgot-password"}>{t.recovery.forgotTitle}</Link>
          </>
        ) : (
          <>
            {error && <div className="alert-error">{error}</div>}
            <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: 14 }}>
              <div className="form-field">
                <label>{t.recovery.newPassword}</label>
                <PasswordInput value={password} onChange={setPassword} required minLength={8} />
                <small className="text-muted">{t.recovery.hint}</small>
              </div>
              <div className="form-field">
                <label>{t.recovery.confirmPassword}</label>
                <PasswordInput value={confirm} onChange={setConfirm} required minLength={8} />
              </div>
              <button className="btn" type="submit" disabled={loading}>
                {loading ? t.recovery.saving : t.recovery.save}
              </button>
            </form>
          </>
        )}
      </div>
    </div>
  );
}
