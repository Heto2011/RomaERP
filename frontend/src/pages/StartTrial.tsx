import { useEffect, useRef, useState, type FormEvent } from "react";
import { useNavigate, useSearchParams, Link } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
import { TrialApi } from "../api/services";
import { BusinessType, Country, ProductScope } from "../api/types";
import { getErrorMessage } from "../api/client";
import { useLanguage } from "../i18n/LanguageContext";
import PasswordInput from "../components/PasswordInput";

const countryLabel: Record<Country, { ar: string; en: string }> = {
  [Country.Egypt]: { ar: "مصر", en: "Egypt" },
  [Country.SaudiArabia]: { ar: "السعودية", en: "Saudi Arabia" },
  [Country.UAE]: { ar: "الإمارات", en: "UAE" },
  [Country.Bahrain]: { ar: "البحرين", en: "Bahrain" },
  [Country.Oman]: { ar: "عُمان", en: "Oman" },
  [Country.Qatar]: { ar: "قطر", en: "Qatar" },
  [Country.Kuwait]: { ar: "الكويت", en: "Kuwait" },
  [Country.UnitedKingdom]: { ar: "بريطانيا", en: "United Kingdom" },
  [Country.Guernsey]: { ar: "جيرنزي", en: "Guernsey" },
};

export default function StartTrial() {
  const { loginWithToken } = useAuth();
  const navigate = useNavigate();
  const { t, lang, setLang } = useLanguage();
  const [searchParams] = useSearchParams();

  const [productScope] = useState<ProductScope>(
    searchParams.get("product") === "hr" ? ProductScope.PeopleOnly : ProductScope.Full
  );
  const [businessTypes, setBusinessTypes] = useState<number>(0);
  const [activityOpen, setActivityOpen] = useState(false);
  const activityRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!activityOpen) return;
    function onDown(e: MouseEvent) {
      if (activityRef.current && !activityRef.current.contains(e.target as Node)) setActivityOpen(false);
    }
    function onKey(e: KeyboardEvent) {
      if (e.key === "Escape") setActivityOpen(false);
    }
    document.addEventListener("mousedown", onDown);
    document.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("mousedown", onDown);
      document.removeEventListener("keydown", onKey);
    };
  }, [activityOpen]);
  const [companyNameAr, setCompanyNameAr] = useState("");
  const [companyNameEn, setCompanyNameEn] = useState("");
  const [country, setCountry] = useState<Country>(Country.SaudiArabia);
  const [adminFullName, setAdminFullName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [agreedToTerms, setAgreedToTerms] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  function toggleActivity(type: BusinessType) {
    setBusinessTypes((cur) => {
      if ((cur & type) !== 0) return cur & ~type;
      const picked = [1, 2, 4, 8, 16, 32].filter((bit) => (cur & bit) !== 0).length;
      return picked >= 3 ? cur : cur | type;
    });
  }

  const activities = [
    { type: BusinessType.Restaurant, label: t.trial.activityRestaurant, soon: false },
    { type: BusinessType.Retail, label: t.trial.activityRetail, soon: false },
    { type: BusinessType.Services, label: t.trial.activityServices, soon: false },
    { type: BusinessType.Wholesale, label: t.trial.activityWholesale, soon: false },
    { type: BusinessType.Manufacturing, label: t.trial.activityManufacturing, soon: false },
    { type: BusinessType.Contracting, label: t.trial.activityContracting, soon: true },
  ];

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
        productScope,
        businessTypes,
      });
      const { data } = res;
      loginWithToken(data.companyCode, data.token, data.email, data.fullName, data.roles, [], data.productScope, data.businessTypes);
      navigate(data.productScope === ProductScope.PeopleOnly ? "/people" : "/");
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
          {productScope === ProductScope.Full && (
            <div className="form-field" ref={activityRef} style={{ position: "relative" }}>
              <label>{t.trial.activityTitle}</label>
              <button
                type="button"
                className="btn btn-secondary"
                aria-haspopup="listbox"
                aria-expanded={activityOpen}
                onClick={() => setActivityOpen((v) => !v)}
                style={{ width: "100%", display: "flex", justifyContent: "space-between", alignItems: "center", fontWeight: 500 }}
              >
                <span style={{ overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                  {activities.filter((a) => (businessTypes & a.type) !== 0).map((a) => a.label).join(lang === "ar" ? "، " : ", ") || t.trial.activityPlaceholder}
                </span>
                <span aria-hidden="true">▾</span>
              </button>
              {activityOpen && (
                <div
                  role="listbox"
                  aria-multiselectable="true"
                  style={{ position: "absolute", insetInline: 0, top: "100%", zIndex: 20, marginTop: 4, background: "var(--color-surface)", border: "1px solid var(--color-border)", borderRadius: 8, boxShadow: "0 8px 24px rgba(0,0,0,.18)", padding: 6 }}
                >
                  {activities.map((a) => {
                    const selected = (businessTypes & a.type) !== 0;
                    return (
                      <label
                        key={a.type}
                        role="option"
                        aria-selected={selected}
                        style={{ display: "flex", alignItems: "center", gap: 10, padding: "9px 10px", borderRadius: 6, cursor: "pointer", fontSize: 14, color: "var(--color-text)" }}
                      >
                        <input type="checkbox" checked={selected} onChange={() => toggleActivity(a.type)} />
                        <span>{a.label}</span>
                        {a.soon && <span className="sidebar-soon-badge">{t.trial.activitySoon}</span>}
                      </label>
                    );
                  })}
                </div>
              )}
              <span className="text-muted" style={{ fontSize: 12 }}>
                {(businessTypes & BusinessType.Contracting) !== 0 ? t.trial.activityContractingNote : t.trial.activityHint}
              </span>
            </div>
          )}
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
            <PasswordInput value={password} onChange={setPassword} required minLength={8} />
            <span className="text-muted" style={{ fontSize: 12 }}>{t.trial.passwordHint}</span>
          </div>
          <label style={{ display: "flex", alignItems: "center", gap: 8, fontSize: 13 }}>
            <input type="checkbox" checked={agreedToTerms} onChange={(e) => setAgreedToTerms(e.target.checked)} required />
            <span>
              {t.trial.agreeToTermsPrefix}{" "}
              <a href="/terms.html" target="_blank" rel="noopener noreferrer">
                {t.trial.agreeToTermsLink}
              </a>
            </span>
          </label>
          <button className="btn" type="submit" disabled={loading || !agreedToTerms || (productScope === ProductScope.Full && businessTypes === 0)}>
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
