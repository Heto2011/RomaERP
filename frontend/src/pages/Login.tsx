import { useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
import { getErrorMessage } from "../api/client";
import { useLanguage } from "../i18n/LanguageContext";

export default function Login() {
  const { login } = useAuth();
  const navigate = useNavigate();
  const { t, lang, setLang } = useLanguage();
  const [companyCode, setCompanyCode] = useState(() => localStorage.getItem("companyCode") ?? "demo");
  const [email, setEmail] = useState("admin@romaerp.local");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setLoading(true);
    try {
      await login(companyCode.trim().toLowerCase(), email, password);
      navigate("/");
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="login-page">
      <div className="login-card">
        <div style={{ display: "flex", justifyContent: "flex-end" }}>
          <button className="btn btn-secondary btn-sm" onClick={() => setLang(lang === "ar" ? "en" : "ar")}>
            {t.language}
          </button>
        </div>
        <h1>{t.login.title}</h1>
        <p>{t.login.subtitle}</p>
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
          <div className="form-field">
            <label>{t.login.password}</label>
            <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} required />
          </div>
          <button className="btn" type="submit" disabled={loading}>
            {loading ? t.login.submitting : t.login.submit}
          </button>
        </form>
      </div>
    </div>
  );
}
