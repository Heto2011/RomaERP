import { useState, type FormEvent } from "react";
import { useNavigate, Link } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
import { TrialApi } from "../api/services";
import { Country } from "../api/types";
import { getErrorMessage } from "../api/client";
import { useLanguage } from "../i18n/LanguageContext";

const countryLabel: Record<Country, { ar: string; en: string }> = {
  [Country.Egypt]: { ar: "مصر", en: "Egypt" },
  [Country.SaudiArabia]: { ar: "السعودية", en: "Saudi Arabia" },
  [Country.UAE]: { ar: "الإمارات", en: "UAE" },
  [Country.Bahrain]: { ar: "البحرين", en: "Bahrain" },
  [Country.Oman]: { ar: "عُمان", en: "Oman" },
  [Country.Qatar]: { ar: "قطر", en: "Qatar" },
  [Country.Kuwait]: { ar: "الكويت", en: "Kuwait" },
};

export default function StartTrial() {
  const { loginWithToken } = useAuth();
  const navigate = useNavigate();
  const { t, lang, setLang } = useLanguage();

  const [companyNameAr, setCompanyNameAr] = useState("");
  const [companyNameEn, setCompanyNameEn] = useState("");
  const [country, setCountry] = useState<Country>(Country.SaudiArabia);
  const [adminFullName, setAdminFullName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setLoading(true);
    try {
      const res = await TrialApi.signUp({
        companyNameAr,
        companyNameEn,
        country,
        adminFullName,
        adminEmail: email,
        adminPassword: password,
      });
      const { data } = res;
      loginWithToken(data.companyCode, data.token, data.email, data.fullName, data.roles);
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
        <h1>{t.trial.title}</h1>
        <p>{t.trial.subtitle}</p>
        {error && <div className="alert-error">{error}</div>}
        <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: 14 }}>
          <div className="form-field">
            <label>{t.trial.companyNameAr}</label>
            <input type="text" value={companyNameAr} onChange={(e) => setCompanyNameAr(e.target.value)} required />
          </div>
          <div className="form-field">
            <label>{t.trial.companyNameEn}</label>
            <input type="text" value={companyNameEn} onChange={(e) => setCompanyNameEn(e.target.value)} required />
          </div>
          <div className="form-field">
            <label>{t.trial.country}</label>
            <select value={country} onChange={(e) => setCountry(Number(e.target.value) as Country)}>
              {Object.entries(countryLabel).map(([value, label]) => (
                <option key={value} value={value}>{lang === "ar" ? label.ar : label.en}</option>
              ))}
            </select>
          </div>
          <div className="form-field">
            <label>{t.trial.adminFullName}</label>
            <input type="text" value={adminFullName} onChange={(e) => setAdminFullName(e.target.value)} required />
          </div>
          <div className="form-field">
            <label>{t.trial.email}</label>
            <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} required />
          </div>
          <div className="form-field">
            <label>{t.trial.password}</label>
            <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} required minLength={8} />
            <span className="text-muted" style={{ fontSize: 12 }}>{t.trial.passwordHint}</span>
          </div>
          <button className="btn" type="submit" disabled={loading}>
            {loading ? t.trial.submitting : t.trial.submit}
          </button>
        </form>
        <p style={{ marginTop: 16, fontSize: 13 }} className="text-muted">
          {t.trial.alreadyHaveAccount} <Link to="/login">{t.trial.loginLink}</Link>
        </p>
      </div>
    </div>
  );
}
